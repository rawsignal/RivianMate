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

    /// <summary>
    /// Calculate and store a battery health snapshot from the current vehicle state.
    /// </summary>
    public async Task<BatteryHealthSnapshot?> RecordHealthSnapshotAsync(
        int vehicleId, 
        CancellationToken cancellationToken = default)
    {
        var vehicle = await _db.Vehicles
            .FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken);

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
            DegradationPercent = degradationPercent
        };

        // Calculate trend-based projections if we have enough historical data
        await CalculateTrendProjectionsAsync(snapshot, cancellationToken);

        _db.BatteryHealthSnapshots.Add(snapshot);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Recorded battery health snapshot for vehicle {VehicleId}: {HealthPercent:F1}% ({ReportedCapacity:F1} / {OriginalCapacity:F1} kWh) at {Odometer:F0} miles",
            vehicleId, healthPercent, reportedCapacity, originalCapacity, snapshot.Odometer);

        return snapshot;
    }

    /// <summary>
    /// Calculate trend-based projections from historical snapshots.
    /// </summary>
    private async Task CalculateTrendProjectionsAsync(
        BatteryHealthSnapshot snapshot, 
        CancellationToken cancellationToken)
    {
        // Get historical snapshots for this vehicle (at least 30 days old for meaningful trend)
        var historicalSnapshots = await _db.BatteryHealthSnapshots
            .Where(s => s.VehicleId == snapshot.VehicleId 
                && s.Timestamp < DateTime.UtcNow.AddDays(-30))
            .OrderBy(s => s.Odometer)
            .ToListAsync(cancellationToken);

        if (historicalSnapshots.Count < 2)
        {
            _logger.LogDebug("Not enough historical data for trend calculation");
            return;
        }

        // Calculate degradation rate per 10k miles using linear regression
        var dataPoints = historicalSnapshots
            .Where(s => s.Odometer > 0)
            .Select(s => (Odometer: s.Odometer, Health: s.HealthPercent))
            .ToList();

        // Add current snapshot
        if (snapshot.Odometer > 0)
        {
            dataPoints.Add((snapshot.Odometer, snapshot.HealthPercent));
        }

        if (dataPoints.Count < 2)
            return;

        // Simple linear regression: Health = a + b * Odometer
        var (slope, intercept) = CalculateLinearRegression(dataPoints);

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

            // Calculate remaining warranty capacity
            var warrantyThresholdCapacity = snapshot.OriginalCapacityKwh * 0.70;
            snapshot.RemainingWarrantyCapacityKwh = snapshot.ReportedCapacityKwh - warrantyThresholdCapacity;

            _logger.LogDebug(
                "Trend analysis: {DegradationRate:F2}% per 10k miles, projected {HealthAt100k:F1}% at 100k miles",
                degradationPer10kMiles, healthAt100k);
        }
    }

    /// <summary>
    /// Simple linear regression calculation.
    /// Returns (slope, intercept) for y = intercept + slope * x
    /// </summary>
    private static (double Slope, double Intercept) CalculateLinearRegression(
        List<(double X, double Y)> points)
    {
        var n = points.Count;
        var sumX = points.Sum(p => p.X);
        var sumY = points.Sum(p => p.Y);
        var sumXY = points.Sum(p => p.X * p.Y);
        var sumX2 = points.Sum(p => p.X * p.X);

        var slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
        var intercept = (sumY - slope * sumX) / n;

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
            CurrentHealthPercent = latestSnapshot.HealthPercent,
            CurrentCapacityKwh = latestSnapshot.ReportedCapacityKwh,
            OriginalCapacityKwh = latestSnapshot.OriginalCapacityKwh,
            CapacityLostKwh = latestSnapshot.CapacityLostKwh,
            CurrentOdometer = latestSnapshot.Odometer,
            DegradationRatePer10kMiles = latestSnapshot.DegradationRatePer10kMiles,
            ProjectedHealthAt100kMiles = latestSnapshot.ProjectedHealthAt100kMiles,
            ProjectedMilesTo70Percent = latestSnapshot.ProjectedMilesTo70Percent,
            RemainingWarrantyCapacityKwh = latestSnapshot.RemainingWarrantyCapacityKwh,
            FirstRecordedDate = firstSnapshot?.Timestamp,
            FirstRecordedOdometer = firstSnapshot?.Odometer,
            FirstRecordedHealth = firstSnapshot?.HealthPercent,
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
    
    // Current state
    public double CurrentHealthPercent { get; set; }
    public double CurrentCapacityKwh { get; set; }
    public double OriginalCapacityKwh { get; set; }
    public double CapacityLostKwh { get; set; }
    public double CurrentOdometer { get; set; }
    
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
