using Microsoft.EntityFrameworkCore;
using RivianMate.Core;
using RivianMate.Core.Entities;
using RivianMate.Core.Enums;
using RivianMate.Infrastructure.Data;

namespace RivianMate.Api.Services;

/// <summary>
/// Service for detecting and tracking drives based on vehicle state changes.
/// </summary>
public class DriveTrackingService
{
    private readonly RivianMateDbContext _db;
    private readonly ILogger<DriveTrackingService> _logger;

    public DriveTrackingService(
        RivianMateDbContext db,
        ILogger<DriveTrackingService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Process a vehicle state update for drive tracking.
    /// Detects drive start/end and records positions during drives.
    /// </summary>
    public async Task ProcessStateForDriveTrackingAsync(
        int vehicleId,
        VehicleState currentState,
        CancellationToken cancellationToken = default)
    {
        var vehicle = await _db.Vehicles
            .FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken);

        if (vehicle == null)
            return;

        var activeDrive = await _db.Drives
            .FirstOrDefaultAsync(d => d.VehicleId == vehicleId && d.IsActive, cancellationToken);

        var isDriving = IsDriving(currentState);

        if (isDriving)
        {
            if (activeDrive == null)
            {
                // Start a new drive
                await StartDriveAsync(vehicleId, vehicle, currentState, cancellationToken);
            }
            else
            {
                // Continue existing drive - record position
                await RecordPositionAsync(activeDrive, currentState, cancellationToken);
            }
        }
        else if (activeDrive != null)
        {
            // End the drive
            await EndDriveAsync(activeDrive, vehicle, currentState, cancellationToken);
        }
    }

    private bool IsDriving(VehicleState state)
    {
        // Consider driving if gear is in Drive or Reverse
        return state.GearStatus == GearStatus.Drive || state.GearStatus == GearStatus.Reverse;
    }

    private async Task StartDriveAsync(
        int vehicleId,
        Vehicle vehicle,
        VehicleState state,
        CancellationToken cancellationToken)
    {
        var drive = new Drive
        {
            VehicleId = vehicleId,
            StartTime = state.Timestamp,
            IsActive = true,
            StartOdometer = state.Odometer ?? 0,
            StartBatteryLevel = state.BatteryLevel ?? 0,
            StartRangeEstimate = state.RangeEstimate,
            StartLatitude = state.Latitude,
            StartLongitude = state.Longitude,
            StartElevation = state.Altitude,
            DriveMode = state.DriveMode
        };

        _db.Drives.Add(drive);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Started drive {DriveId} for vehicle {VehicleId} at {Odometer} miles, {Battery}%",
            drive.Id, vehicleId, drive.StartOdometer, drive.StartBatteryLevel);

        // Record initial position
        await RecordPositionAsync(drive, state, cancellationToken);
    }

    private async Task RecordPositionAsync(
        Drive drive,
        VehicleState state,
        CancellationToken cancellationToken)
    {
        // Only record if we have valid coordinates
        if (state.Latitude == null || state.Longitude == null)
            return;

        // Check if we should record this position (avoid duplicates)
        var lastPosition = await _db.Positions
            .Where(p => p.DriveId == drive.Id)
            .OrderByDescending(p => p.Timestamp)
            .FirstOrDefaultAsync(cancellationToken);

        // Skip if too close in time (less than 5 seconds) or same location
        if (lastPosition != null)
        {
            var timeDiff = (state.Timestamp - lastPosition.Timestamp).TotalSeconds;
            if (timeDiff < 5)
                return;

            // Skip if position hasn't changed significantly (about 10 meters)
            var latDiff = Math.Abs(state.Latitude.Value - lastPosition.Latitude);
            var lonDiff = Math.Abs(state.Longitude.Value - lastPosition.Longitude);
            if (latDiff < 0.0001 && lonDiff < 0.0001 && timeDiff < 30)
                return;
        }

        var position = new Position
        {
            DriveId = drive.Id,
            Timestamp = state.Timestamp,
            Latitude = state.Latitude.Value,
            Longitude = state.Longitude.Value,
            Altitude = state.Altitude,
            Speed = state.Speed,
            Heading = state.Heading,
            BatteryLevel = state.BatteryLevel,
            Odometer = state.Odometer
        };

        _db.Positions.Add(position);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task EndDriveAsync(
        Drive drive,
        Vehicle vehicle,
        VehicleState state,
        CancellationToken cancellationToken)
    {
        // Record final position
        await RecordPositionAsync(drive, state, cancellationToken);

        // Update drive end data
        drive.EndTime = state.Timestamp;
        drive.IsActive = false;
        drive.EndOdometer = state.Odometer;
        drive.EndBatteryLevel = state.BatteryLevel;
        drive.EndRangeEstimate = state.RangeEstimate;
        drive.EndLatitude = state.Latitude;
        drive.EndLongitude = state.Longitude;
        drive.EndElevation = state.Altitude;

        // Calculate distance
        if (drive.EndOdometer != null && drive.StartOdometer > 0)
        {
            drive.DistanceMiles = drive.EndOdometer.Value - drive.StartOdometer;
        }

        // Calculate energy used
        if (drive.EndBatteryLevel != null && drive.StartBatteryLevel > 0)
        {
            var batteryUsedPercent = drive.StartBatteryLevel - drive.EndBatteryLevel.Value;

            // Get usable capacity - prefer current reported capacity, fall back to original
            var capacity = vehicle.OriginalCapacityKwh
                ?? BatteryPackSpecs.GetOriginalCapacityKwh(vehicle.BatteryPack, vehicle.Year);

            if (capacity > 0 && batteryUsedPercent > 0)
            {
                drive.EnergyUsedKwh = capacity * (batteryUsedPercent / 100.0);
            }
        }

        // Calculate efficiency
        if (drive.DistanceMiles > 0 && drive.EnergyUsedKwh > 0)
        {
            drive.EfficiencyMilesPerKwh = drive.DistanceMiles.Value / drive.EnergyUsedKwh.Value;
            drive.EfficiencyWhPerMile = (drive.EnergyUsedKwh.Value * 1000) / drive.DistanceMiles.Value;
        }

        // Calculate average and max speed from positions
        var positions = await _db.Positions
            .Where(p => p.DriveId == drive.Id && p.Speed != null && p.Speed > 0)
            .Select(p => p.Speed!.Value)
            .ToListAsync(cancellationToken);

        if (positions.Count > 0)
        {
            drive.AverageSpeedMph = positions.Average();
            drive.MaxSpeedMph = positions.Max();
        }

        // Calculate elevation gain
        var elevations = await _db.Positions
            .Where(p => p.DriveId == drive.Id && p.Altitude != null)
            .OrderBy(p => p.Timestamp)
            .Select(p => p.Altitude!.Value)
            .ToListAsync(cancellationToken);

        if (elevations.Count > 1)
        {
            double elevationGain = 0;
            for (int i = 1; i < elevations.Count; i++)
            {
                var diff = elevations[i] - elevations[i - 1];
                if (diff > 0)
                    elevationGain += diff;
            }
            drive.ElevationGain = elevationGain;
        }

        // Calculate average temperature if we have cabin temp data
        // (Would need to store temp in positions or query states)

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Ended drive {DriveId} for vehicle {VehicleId}: {Distance:F1} miles, {Energy:F1} kWh, {Efficiency:F2} mi/kWh",
            drive.Id, drive.VehicleId, drive.DistanceMiles, drive.EnergyUsedKwh, drive.EfficiencyMilesPerKwh);
    }
}
