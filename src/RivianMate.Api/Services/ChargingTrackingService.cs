using Microsoft.EntityFrameworkCore;
using RivianMate.Core;
using RivianMate.Core.Entities;
using RivianMate.Core.Enums;
using RivianMate.Infrastructure.Data;

namespace RivianMate.Api.Services;

/// <summary>
/// Service for detecting and tracking charging sessions based on vehicle state changes.
/// </summary>
public class ChargingTrackingService
{
    private readonly RivianMateDbContext _db;
    private readonly UserPreferencesService _preferencesService;
    private readonly ILogger<ChargingTrackingService> _logger;

    // Fixed radius for home location detection (in meters)
    private const double HomeRadiusMeters = 100;

    public ChargingTrackingService(
        RivianMateDbContext db,
        UserPreferencesService preferencesService,
        ILogger<ChargingTrackingService> logger)
    {
        _db = db;
        _preferencesService = preferencesService;
        _logger = logger;
    }

    /// <summary>
    /// Process a vehicle state update for charging session tracking.
    /// Detects charge start/end and updates session data.
    /// </summary>
    public async Task ProcessStateForChargingTrackingAsync(
        int vehicleId,
        VehicleState currentState,
        CancellationToken cancellationToken = default)
    {
        var vehicle = await _db.Vehicles
            .FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken);

        if (vehicle == null)
            return;

        var activeSession = await _db.ChargingSessions
            .FirstOrDefaultAsync(s => s.VehicleId == vehicleId && s.IsActive, cancellationToken);

        var isCharging = IsCharging(currentState);

        if (isCharging)
        {
            if (activeSession == null)
            {
                // Start a new charging session
                await StartChargingSessionAsync(vehicleId, vehicle, currentState, cancellationToken);
            }
            else
            {
                // Update existing session with latest data
                await UpdateChargingSessionAsync(activeSession, vehicle, currentState, cancellationToken);
            }
        }
        else if (activeSession != null)
        {
            // End the charging session
            await EndChargingSessionAsync(activeSession, vehicle, currentState, cancellationToken);
        }
    }

    private static bool IsCharging(VehicleState state)
    {
        // Consider charging if ChargerState is Charging or if PowerState is Charging
        return state.ChargerState == ChargerState.Charging ||
               state.PowerState == PowerState.Charging;
    }

    private async Task StartChargingSessionAsync(
        int vehicleId,
        Vehicle vehicle,
        VehicleState state,
        CancellationToken cancellationToken)
    {
        var session = new ChargingSession
        {
            VehicleId = vehicleId,
            StartTime = state.Timestamp,
            IsActive = true,
            StartBatteryLevel = state.BatteryLevel ?? 0,
            ChargeLimit = state.BatteryLimit,
            StartRangeEstimate = state.RangeEstimate,
            Latitude = state.Latitude,
            Longitude = state.Longitude,
            OdometerAtStart = state.Odometer,
            TemperatureAtStart = state.CabinTemperature,
            DriveMode = state.DriveMode,
            ChargeType = DetermineChargeType(state, null)
        };

        // Determine if this is home charging by comparing to user's saved home location
        session.IsHomeCharging = await IsHomeLocationAsync(vehicle.OwnerId, state.Latitude, state.Longitude);

        _db.ChargingSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Started charging session {SessionId} for vehicle {VehicleId} at {Battery}%, location: ({Lat}, {Lng}), isHome: {IsHome}",
            session.Id, vehicleId, session.StartBatteryLevel, state.Latitude, state.Longitude, session.IsHomeCharging);
    }

    private async Task UpdateChargingSessionAsync(
        ChargingSession session,
        Vehicle vehicle,
        VehicleState state,
        CancellationToken cancellationToken)
    {
        // Track peak power
        // Note: We'd need actual charging power in VehicleState to track this properly
        // For now, we'll estimate based on battery gain rate if we have history

        // Update charge type if we can determine it more accurately
        var newChargeType = DetermineChargeType(state, session.ChargeType);
        if (newChargeType != ChargeType.Unknown && session.ChargeType == ChargeType.Unknown)
        {
            session.ChargeType = newChargeType;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task EndChargingSessionAsync(
        ChargingSession session,
        Vehicle vehicle,
        VehicleState state,
        CancellationToken cancellationToken)
    {
        session.EndTime = state.Timestamp;
        session.IsActive = false;
        session.EndBatteryLevel = state.BatteryLevel;
        session.EndRangeEstimate = state.RangeEstimate;

        // Calculate energy added
        if (session.EndBatteryLevel != null && session.StartBatteryLevel > 0)
        {
            var batteryGainPercent = session.EndBatteryLevel.Value - session.StartBatteryLevel;

            if (batteryGainPercent > 0)
            {
                var capacity = vehicle.OriginalCapacityKwh
                    ?? BatteryPackSpecs.GetOriginalCapacityKwh(vehicle.BatteryPack, vehicle.Year);

                if (capacity > 0)
                {
                    session.EnergyAddedKwh = capacity * (batteryGainPercent / 100.0);

                    // Calculate capacity from this session (for battery health tracking)
                    // Only if we have a good charge delta (at least 20%)
                    if (batteryGainPercent >= 20)
                    {
                        session.CalculatedCapacityKwh = session.EnergyAddedKwh / (batteryGainPercent / 100.0);
                        session.CapacityConfidence = Math.Min(1.0, batteryGainPercent / 50.0);
                    }
                }
            }
        }

        // Calculate range added
        if (session.EndRangeEstimate != null && session.StartRangeEstimate != null)
        {
            session.RangeAdded = session.EndRangeEstimate.Value - session.StartRangeEstimate.Value;
        }

        // Calculate average power
        if (session.EnergyAddedKwh > 0 && session.EndTime.HasValue)
        {
            var durationHours = (session.EndTime.Value - session.StartTime).TotalHours;
            if (durationHours > 0)
            {
                session.AveragePowerKw = session.EnergyAddedKwh.Value / durationHours;

                // Estimate peak power (typically ~20% higher than average for AC, ~50% for DC)
                if (session.ChargeType == ChargeType.DC_Fast)
                {
                    session.PeakPowerKw = session.AveragePowerKw * 1.5;
                }
                else
                {
                    session.PeakPowerKw = session.AveragePowerKw * 1.2;
                }
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Ended charging session {SessionId} for vehicle {VehicleId}: {StartSoC}% -> {EndSoC}%, +{Energy:F1} kWh, {Duration:F1} hours",
            session.Id, session.VehicleId,
            session.StartBatteryLevel, session.EndBatteryLevel,
            session.EnergyAddedKwh, (session.EndTime.Value - session.StartTime).TotalHours);
    }

    private static ChargeType DetermineChargeType(VehicleState state, ChargeType? existing)
    {
        // If we have TimeToEndOfCharge, we can estimate power and thus charge type
        // For now, we'll rely on existing value or return Unknown

        // Future: Look at charging power to determine type
        // DC Fast: > 50kW
        // AC Level 2: 3-20kW
        // AC Level 1: < 3kW

        return existing ?? ChargeType.Unknown;
    }

    /// <summary>
    /// Checks if the given location is within the home radius of the user's saved home location.
    /// Uses the Haversine formula to calculate distance between two coordinates.
    /// </summary>
    private async Task<bool> IsHomeLocationAsync(Guid? ownerId, double? latitude, double? longitude)
    {
        if (latitude == null || longitude == null || ownerId == null)
            return false;

        try
        {
            var preferences = await _preferencesService.GetPreferencesAsync(ownerId.Value);

            // Check if user has a home location configured
            if (preferences.HomeLatitude == null || preferences.HomeLongitude == null)
                return false;

            // Calculate distance using Haversine formula
            var distanceMeters = CalculateDistanceMeters(
                latitude.Value, longitude.Value,
                preferences.HomeLatitude.Value, preferences.HomeLongitude.Value);

            return distanceMeters <= HomeRadiusMeters;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking home location for owner {OwnerId}", ownerId);
            return false;
        }
    }

    /// <summary>
    /// Calculates the distance between two coordinates in meters using the Haversine formula.
    /// </summary>
    private static double CalculateDistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double EarthRadiusMeters = 6371000;

        var lat1Rad = DegreesToRadians(lat1);
        var lat2Rad = DegreesToRadians(lat2);
        var deltaLatRad = DegreesToRadians(lat2 - lat1);
        var deltaLonRad = DegreesToRadians(lon2 - lon1);

        var a = Math.Sin(deltaLatRad / 2) * Math.Sin(deltaLatRad / 2) +
                Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                Math.Sin(deltaLonRad / 2) * Math.Sin(deltaLonRad / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return EarthRadiusMeters * c;
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180;
    }
}
