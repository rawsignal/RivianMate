using Microsoft.EntityFrameworkCore;
using RivianMate.Core;
using RivianMate.Core.Entities;
using RivianMate.Core.Enums;
using RivianMate.Infrastructure.Data;
using DriveType = RivianMate.Core.Enums.DriveType;

namespace RivianMate.Api.Services;

/// <summary>
/// Service for calculating and tracking battery health over time.
/// Uses the batteryCapacity field from the Rivian API for accurate health estimation.
/// </summary>
public class BatteryHealthService
{
    private readonly RivianMateDbContext _db;
    private readonly ILogger<BatteryHealthService> _logger;

    public BatteryHealthService(RivianMateDbContext db, ILogger<BatteryHealthService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // Number of recent snapshots to use for smoothing
    private const int SmoothingWindowSize = 10;

    // Temperature range considered optimal for capacity readings (Celsius)
    private const double OptimalTempMin = 15.0;
    private const double OptimalTempMax = 30.0;

    // SoC threshold - readings above this are considered more reliable
    private const double HighSocThreshold = 70.0;

    /// <summary>
    /// Calculate and store a battery health snapshot from the current vehicle state.
    /// </summary>
    public async Task<BatteryHealthSnapshot?> RecordHealthSnapshotAsync(
        int vehicleId,
        CancellationToken cancellationToken = default)
    {
        var vehicle = await _db.Vehicles.FindWithoutImageAsync(vehicleId, cancellationToken);

        if (vehicle == null)
        {
            _logger.LogWarning("Vehicle {VehicleId} not found", vehicleId);
            return null;
        }

        // Get the most recent vehicle state with battery capacity data
        var latestState = await _db.VehicleStates
            .Where(s => s.VehicleId == vehicleId && s.BatteryCapacityKwh != null)
            .OrderByDescending(s => s.Timestamp)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestState?.BatteryCapacityKwh == null)
        {
            _logger.LogDebug("No battery capacity data available for vehicle {VehicleId}", vehicleId);
            return null;
        }

        var reportedCapacity = latestState.BatteryCapacityKwh.Value;
        var originalCapacity = vehicle.OriginalCapacityKwh
            ?? BatteryPackSpecs.GetOriginalCapacityKwh(vehicle.BatteryPack, vehicle.Year);

        var healthPercent = BatteryPackSpecs.CalculateHealthPercent(reportedCapacity, originalCapacity);
        var capacityLost = originalCapacity - reportedCapacity;
        var degradationPercent = 100.0 - healthPercent;

        // Calculate reading confidence based on conditions
        var readingConfidence = CalculateReadingConfidence(
            latestState.BatteryLevel,
            latestState.CabinTemperature);

        var snapshot = new BatteryHealthSnapshot
        {
            VehicleId = vehicleId,
            Timestamp = latestState.Timestamp,
            Odometer = latestState.Odometer ?? 0,
            ReportedCapacityKwh = reportedCapacity,
            StateOfCharge = latestState.BatteryLevel,
            Temperature = latestState.CabinTemperature,
            OriginalCapacityKwh = originalCapacity,
            HealthPercent = healthPercent,
            CapacityLostKwh = capacityLost,
            DegradationPercent = degradationPercent,
            ReadingConfidence = readingConfidence
        };

        // Calculate smoothed capacity from recent readings
        await CalculateSmoothedValuesAsync(snapshot, cancellationToken);

        // Calculate trend-based projections if we have enough historical data
        await CalculateTrendProjectionsAsync(snapshot, cancellationToken);

        _db.BatteryHealthSnapshots.Add(snapshot);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Recorded battery health snapshot for vehicle {VehicleId}: {HealthPercent:F1}% raw, {SmoothedHealth:F1}% smoothed ({ReportedCapacity:F1} kWh, confidence: {Confidence:F2}) at {Odometer:F0} miles",
            vehicleId, healthPercent, snapshot.SmoothedHealthPercent ?? healthPercent,
            reportedCapacity, readingConfidence, snapshot.Odometer);

        return snapshot;
    }

    /// <summary>
    /// Calculate confidence score for a reading based on SoC and temperature.
    /// Returns value between 0 and 1.
    /// </summary>
    private static double CalculateReadingConfidence(double? soc, double? temperature)
    {
        var confidence = 0.5; // Base confidence

        // SoC factor: higher SoC readings are more reliable
        if (soc.HasValue)
        {
            if (soc.Value >= 90) confidence += 0.3;
            else if (soc.Value >= HighSocThreshold) confidence += 0.2;
            else if (soc.Value >= 50) confidence += 0.1;
            else if (soc.Value < 20) confidence -= 0.2; // Low SoC readings are less reliable
        }

        // Temperature factor: moderate temps give more reliable readings
        if (temperature.HasValue)
        {
            if (temperature.Value >= OptimalTempMin && temperature.Value <= OptimalTempMax)
                confidence += 0.2;
            else if (temperature.Value < 5 || temperature.Value > 40)
                confidence -= 0.2; // Extreme temps affect readings
        }

        return Math.Clamp(confidence, 0.1, 1.0);
    }

    /// <summary>
    /// Calculate smoothed capacity using weighted average of recent readings.
    /// Weights readings by their confidence score.
    /// </summary>
    private async Task CalculateSmoothedValuesAsync(
        BatteryHealthSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        // Get recent snapshots for this vehicle
        var recentSnapshots = await _db.BatteryHealthSnapshots
            .Where(s => s.VehicleId == snapshot.VehicleId)
            .OrderByDescending(s => s.Timestamp)
            .Take(SmoothingWindowSize)
            .ToListAsync(cancellationToken);

        // Include current reading
        var allReadings = new List<(double Capacity, double Confidence)>
        {
            (snapshot.ReportedCapacityKwh, snapshot.ReadingConfidence ?? 0.5)
        };

        foreach (var s in recentSnapshots)
        {
            allReadings.Add((s.ReportedCapacityKwh, s.ReadingConfidence ?? 0.5));
        }

        if (allReadings.Count == 1)
        {
            // No historical data, use raw value
            snapshot.SmoothedCapacityKwh = snapshot.ReportedCapacityKwh;
            snapshot.SmoothedHealthPercent = snapshot.HealthPercent;
            return;
        }

        // Calculate weighted average using confidence scores
        var totalWeight = allReadings.Sum(r => r.Confidence);
        var weightedSum = allReadings.Sum(r => r.Capacity * r.Confidence);
        var smoothedCapacity = weightedSum / totalWeight;

        // Also apply IQR-based outlier filtering for additional robustness
        var capacities = allReadings.Select(r => r.Capacity).OrderBy(c => c).ToList();
        var q1Index = capacities.Count / 4;
        var q3Index = (capacities.Count * 3) / 4;
        var q1 = capacities[q1Index];
        var q3 = capacities[q3Index];
        var iqr = q3 - q1;
        var lowerBound = q1 - 1.5 * iqr;
        var upperBound = q3 + 1.5 * iqr;

        // Filter outliers and recalculate if we have enough data
        var filteredReadings = allReadings
            .Where(r => r.Capacity >= lowerBound && r.Capacity <= upperBound)
            .ToList();

        if (filteredReadings.Count >= 3)
        {
            totalWeight = filteredReadings.Sum(r => r.Confidence);
            weightedSum = filteredReadings.Sum(r => r.Capacity * r.Confidence);
            smoothedCapacity = weightedSum / totalWeight;

            var outlierCount = allReadings.Count - filteredReadings.Count;
            if (outlierCount > 0)
            {
                _logger.LogDebug(
                    "Filtered {OutlierCount} outlier readings from smoothing calculation (bounds: {Lower:F1} - {Upper:F1} kWh)",
                    outlierCount, lowerBound, upperBound);
            }
        }

        snapshot.SmoothedCapacityKwh = smoothedCapacity;
        snapshot.SmoothedHealthPercent = BatteryPackSpecs.CalculateHealthPercent(
            smoothedCapacity, snapshot.OriginalCapacityKwh);
    }

    /// <summary>
    /// Calculate trend-based projections from historical snapshots.
    /// </summary>
    private async Task CalculateTrendProjectionsAsync(
        BatteryHealthSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        // Always calculate remaining warranty capacity - doesn't need trend data
        var warrantyThresholdCapacity = snapshot.OriginalCapacityKwh * 0.70;
        snapshot.RemainingWarrantyCapacityKwh = snapshot.ReportedCapacityKwh - warrantyThresholdCapacity;

        // Get historical snapshots for this vehicle
        // For meaningful trend analysis, we need either:
        // - At least 7 days of data history, OR
        // - At least 500 miles driven between first and current snapshot
        var historicalSnapshots = await _db.BatteryHealthSnapshots
            .Where(s => s.VehicleId == snapshot.VehicleId)
            .OrderBy(s => s.Odometer)
            .ToListAsync(cancellationToken);

        if (historicalSnapshots.Count < 2)
        {
            _logger.LogDebug("Not enough historical snapshots for trend calculation (need at least 2)");
            return;
        }

        var firstSnapshot = historicalSnapshots.First();
        var daysSinceFirst = (DateTime.UtcNow - firstSnapshot.Timestamp).TotalDays;
        var milesSinceFirst = snapshot.Odometer - firstSnapshot.Odometer;

        // Require minimum data span for reliable projections
        if (daysSinceFirst < 7 && milesSinceFirst < 500)
        {
            _logger.LogDebug(
                "Not enough data span for trend calculation: {Days:F1} days, {Miles:F0} miles",
                daysSinceFirst, milesSinceFirst);
            return;
        }

        // Calculate degradation rate per 10k miles using linear regression
        // Use smoothed health values for more reliable trend analysis
        var dataPoints = historicalSnapshots
            .Where(s => s.Odometer > 0)
            .Select(s => (
                Odometer: s.Odometer,
                Health: s.SmoothedHealthPercent ?? s.HealthPercent, // Prefer smoothed values
                Confidence: s.ReadingConfidence ?? 0.5
            ))
            .ToList();

        // Add current snapshot if not already in list
        if (snapshot.Odometer > 0 && !dataPoints.Any(p => Math.Abs(p.Odometer - snapshot.Odometer) < 1))
        {
            dataPoints.Add((
                snapshot.Odometer,
                snapshot.SmoothedHealthPercent ?? snapshot.HealthPercent,
                snapshot.ReadingConfidence ?? 0.5
            ));
        }

        if (dataPoints.Count < 2)
            return;

        // Need some odometer spread to calculate meaningful slope
        var odometerSpread = dataPoints.Max(p => p.Odometer) - dataPoints.Min(p => p.Odometer);
        if (odometerSpread < 100)
        {
            _logger.LogDebug("Insufficient odometer spread for trend calculation: {Spread:F0} miles", odometerSpread);
            return;
        }

        // Weighted linear regression: Health = a + b * Odometer
        // Weight by confidence score for more reliable trend estimation
        var (slope, intercept) = CalculateWeightedLinearRegression(dataPoints);

        // slope is degradation per mile, convert to per 10k miles
        var degradationPer10kMiles = -slope * 10000; // negative because health decreases

        if (degradationPer10kMiles > 0 && degradationPer10kMiles < 10) // Sanity check
        {
            snapshot.DegradationRatePer10kMiles = degradationPer10kMiles;

            // Project health at 100k miles
            var healthAt100k = intercept + (slope * 100000);
            snapshot.ProjectedHealthAt100kMiles = Math.Max(0, Math.Min(100, healthAt100k));

            // Project miles until 70% (warranty threshold)
            // Only show projection if currently above 70% and degrading
            if (slope < 0 && snapshot.HealthPercent > 70)
            {
                var milesTo70 = (70 - intercept) / slope;
                if (milesTo70 > snapshot.Odometer)
                {
                    snapshot.ProjectedMilesTo70Percent = milesTo70;
                }
            }

            _logger.LogDebug(
                "Trend analysis: {DegradationRate:F2}% per 10k miles, projected {HealthAt100k:F1}% at 100k miles",
                degradationPer10kMiles, healthAt100k);
        }
        else if (degradationPer10kMiles <= 0)
        {
            // Battery appears to be stable or improving (possible with temperature/calibration variations)
            _logger.LogDebug("No measurable degradation detected (rate: {Rate:F3}% per 10k miles)", degradationPer10kMiles);
        }
    }

    /// <summary>
    /// Weighted linear regression calculation.
    /// Returns (slope, intercept) for y = intercept + slope * x
    /// Uses weights (confidence scores) to favor more reliable readings.
    /// </summary>
    private static (double Slope, double Intercept) CalculateWeightedLinearRegression(
        List<(double Odometer, double Health, double Confidence)> points)
    {
        // Weighted linear regression formulas
        var sumW = points.Sum(p => p.Confidence);
        var sumWX = points.Sum(p => p.Confidence * p.Odometer);
        var sumWY = points.Sum(p => p.Confidence * p.Health);
        var sumWXY = points.Sum(p => p.Confidence * p.Odometer * p.Health);
        var sumWX2 = points.Sum(p => p.Confidence * p.Odometer * p.Odometer);

        var denominator = sumW * sumWX2 - sumWX * sumWX;
        if (Math.Abs(denominator) < 1e-10)
        {
            // Fallback to simple average if regression fails
            return (0, points.Average(p => p.Health));
        }

        var slope = (sumW * sumWXY - sumWX * sumWY) / denominator;
        var intercept = (sumWY - slope * sumWX) / sumW;

        return (slope, intercept);
    }

    /// <summary>
    /// Get the latest battery health for a vehicle.
    /// </summary>
    public async Task<BatteryHealthSnapshot?> GetLatestHealthAsync(
        int vehicleId, 
        CancellationToken cancellationToken = default)
    {
        return await _db.BatteryHealthSnapshots
            .Where(s => s.VehicleId == vehicleId)
            .OrderByDescending(s => s.Timestamp)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Get battery health history for a vehicle.
    /// </summary>
    public async Task<List<BatteryHealthSnapshot>> GetHealthHistoryAsync(
        int vehicleId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.BatteryHealthSnapshots
            .Where(s => s.VehicleId == vehicleId);

        if (startDate.HasValue)
            query = query.Where(s => s.Timestamp >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(s => s.Timestamp <= endDate.Value);

        return await query
            .OrderBy(s => s.Timestamp)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Get a summary of battery health for display.
    /// </summary>
    public async Task<BatteryHealthSummary?> GetHealthSummaryAsync(
        int vehicleId,
        CancellationToken cancellationToken = default)
    {
        var vehicle = await _db.Vehicles
            .FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken);

        if (vehicle == null)
            return null;

        var latestSnapshot = await GetLatestHealthAsync(vehicleId, cancellationToken);
        
        if (latestSnapshot == null)
            return null;

        var firstSnapshot = await _db.BatteryHealthSnapshots
            .Where(s => s.VehicleId == vehicleId)
            .OrderBy(s => s.Timestamp)
            .FirstOrDefaultAsync(cancellationToken);

        // Calculate vehicle-specific warranty miles
        var warrantyMiles = vehicle.BatteryPack != BatteryPackType.Unknown && vehicle.DriveType != DriveType.Unknown
            ? BatteryPackSpecs.GetWarrantyMiles(vehicle.BatteryPack, vehicle.DriveType, vehicle.Year)
            : BatteryPackSpecs.DefaultWarrantyMiles;

        return new BatteryHealthSummary
        {
            VehicleId = vehicleId,
            VehicleName = vehicle.Name ?? $"{vehicle.Model} {vehicle.Year}",
            CurrentHealthPercent = latestSnapshot.SmoothedHealthPercent ?? latestSnapshot.HealthPercent,
            CurrentCapacityKwh = latestSnapshot.SmoothedCapacityKwh ?? latestSnapshot.ReportedCapacityKwh,
            RawHealthPercent = latestSnapshot.HealthPercent,
            RawCapacityKwh = latestSnapshot.ReportedCapacityKwh,
            OriginalCapacityKwh = latestSnapshot.OriginalCapacityKwh,
            CapacityLostKwh = latestSnapshot.OriginalCapacityKwh - (latestSnapshot.SmoothedCapacityKwh ?? latestSnapshot.ReportedCapacityKwh),
            CurrentOdometer = latestSnapshot.Odometer,
            ReadingConfidence = latestSnapshot.ReadingConfidence,
            DegradationRatePer10kMiles = latestSnapshot.DegradationRatePer10kMiles,
            ProjectedHealthAt100kMiles = latestSnapshot.ProjectedHealthAt100kMiles,
            ProjectedMilesTo70Percent = latestSnapshot.ProjectedMilesTo70Percent,
            RemainingWarrantyCapacityKwh = latestSnapshot.RemainingWarrantyCapacityKwh,
            FirstRecordedDate = firstSnapshot?.Timestamp,
            FirstRecordedOdometer = firstSnapshot?.Odometer,
            FirstRecordedHealth = firstSnapshot?.SmoothedHealthPercent ?? firstSnapshot?.HealthPercent,
            LastUpdated = latestSnapshot.Timestamp,
            WarrantyThresholdPercent = BatteryPackSpecs.WarrantyThresholdPercent,
            WarrantyMiles = warrantyMiles
        };
    }
}

/// <summary>
/// Summary DTO for battery health display
/// </summary>
public class BatteryHealthSummary
{
    public int VehicleId { get; set; }
    public string VehicleName { get; set; } = string.Empty;

    // Current state (smoothed values for display)
    public double CurrentHealthPercent { get; set; }
    public double CurrentCapacityKwh { get; set; }
    public double OriginalCapacityKwh { get; set; }
    public double CapacityLostKwh { get; set; }
    public double CurrentOdometer { get; set; }

    // Raw values (before smoothing)
    public double RawHealthPercent { get; set; }
    public double RawCapacityKwh { get; set; }

    // Reading quality
    public double? ReadingConfidence { get; set; }

    // Projections
    public double? DegradationRatePer10kMiles { get; set; }
    public double? ProjectedHealthAt100kMiles { get; set; }
    public double? ProjectedMilesTo70Percent { get; set; }
    public double? RemainingWarrantyCapacityKwh { get; set; }

    // Historical context
    public DateTime? FirstRecordedDate { get; set; }
    public double? FirstRecordedOdometer { get; set; }
    public double? FirstRecordedHealth { get; set; }
    public DateTime LastUpdated { get; set; }
    
    // Warranty info
    public double WarrantyThresholdPercent { get; set; }
    public int WarrantyMiles { get; set; }
    
    // Computed properties for display
    public string HealthStatus => CurrentHealthPercent switch
    {
        >= 95 => "Excellent",
        >= 90 => "Very Good",
        >= 85 => "Good",
        >= 80 => "Fair",
        >= 70 => "Below Average",
        _ => "Poor"
    };
    
    public string HealthStatusColor => CurrentHealthPercent switch
    {
        >= 95 => "success",
        >= 90 => "success",
        >= 85 => "info",
        >= 80 => "warning",
        >= 70 => "warning",
        _ => "danger"
    };
}
