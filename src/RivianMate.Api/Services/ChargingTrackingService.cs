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
    private readonly ILogger<ChargingTrackingService> _logger;

    public ChargingTrackingService(
        RivianMateDbContext db,
        ILogger<ChargingTrackingService> logger)
    {
        _db = db;
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

        // Determine if this is home charging by comparing to saved home location
        session.IsHomeCharging = IsHomeLocation(vehicle, state.Latitude, state.Longitude);

        _db.ChargingSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Started charging session {SessionId} for vehicle {VehicleId} at {Battery}%, location: ({Lat}, {Lng})",
            session.Id, vehicleId, session.StartBatteryLevel, state.Latitude, state.Longitude);
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

    private static bool IsHomeLocation(Vehicle vehicle, double? latitude, double? longitude)
    {
        // TODO: Compare to user's saved home location
        // For now, we'll return null to indicate unknown
        if (latitude == null || longitude == null)
            return false;

        // Future: Get user's home location from settings and compare
        // A location within ~100 meters of home would be considered "home charging"

        return false;
    }
}
