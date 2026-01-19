using System.Collections.Concurrent;
using RivianMate.Core.Entities;
using RivianMate.Core.Enums;

namespace RivianMate.Api.Services;

/// <summary>
/// In-memory buffer for vehicle states to avoid storing duplicate data.
/// Only saves a new state when something meaningful has changed.
/// </summary>
public class VehicleStateBuffer
{
    private readonly ConcurrentDictionary<int, BufferedState> _lastStates = new();
    private readonly ILogger<VehicleStateBuffer> _logger;

    // How often to force a save even if nothing changed (heartbeat)
    private readonly TimeSpan _maxTimeBetweenSaves = TimeSpan.FromHours(1);

    // Thresholds for what constitutes a "meaningful" change
    private const double BatteryLevelThreshold = 0.5;      // 0.5% change
    private const double LocationThresholdMeters = 50;     // 50 meters movement
    private const double SpeedThreshold = 0.5;             // 0.5 m/s (~1 mph)
    private const double TemperatureThreshold = 1.0;       // 1°C change
    private const double TirePressureThreshold = 1.0;      // 1 PSI change
    private const double OdometerThreshold = 0.1;          // 0.1 miles

    public VehicleStateBuffer(ILogger<VehicleStateBuffer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Check if the new state should be saved (has meaningful changes from the last saved state).
    /// </summary>
    /// <returns>True if the state should be saved, false if it's a duplicate.</returns>
    public bool ShouldSaveState(VehicleState newState)
    {
        if (!_lastStates.TryGetValue(newState.VehicleId, out var buffered))
        {
            // First state for this vehicle - always save
            return true;
        }

        var lastState = buffered.State;
        var timeSinceLastSave = newState.Timestamp - buffered.SavedAt;

        // Force save after max interval (heartbeat)
        if (timeSinceLastSave >= _maxTimeBetweenSaves)
        {
            _logger.LogDebug("Vehicle {VehicleId}: Forcing save after {Minutes} minutes (heartbeat)",
                newState.VehicleId, timeSinceLastSave.TotalMinutes);
            return true;
        }

        // Check for meaningful changes
        if (HasMeaningfulChange(lastState, newState))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Update the buffer with the newly saved state.
    /// Call this AFTER successfully saving to the database.
    /// </summary>
    public void UpdateBuffer(VehicleState savedState)
    {
        _lastStates[savedState.VehicleId] = new BufferedState(savedState, DateTime.UtcNow);
    }

    /// <summary>
    /// Get statistics about what's currently buffered.
    /// </summary>
    public Dictionary<int, DateTime> GetBufferStats()
    {
        return _lastStates.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.SavedAt);
    }

    /// <summary>
    /// Clear the buffer for a specific vehicle (e.g., when vehicle is removed).
    /// </summary>
    public void ClearVehicle(int vehicleId)
    {
        _lastStates.TryRemove(vehicleId, out _);
    }

    private bool HasMeaningfulChange(VehicleState last, VehicleState current)
    {
        // === Critical state changes (always save) ===

        // Power state changed (sleep/wake/driving)
        if (last.PowerState != current.PowerState)
        {
            _logger.LogDebug("Vehicle {Id}: PowerState changed {Old} -> {New}",
                current.VehicleId, last.PowerState, current.PowerState);
            return true;
        }

        // Gear changed
        if (last.GearStatus != current.GearStatus)
        {
            _logger.LogDebug("Vehicle {Id}: GearStatus changed {Old} -> {New}",
                current.VehicleId, last.GearStatus, current.GearStatus);
            return true;
        }

        // Charging state changed
        if (last.ChargerState != current.ChargerState)
        {
            _logger.LogDebug("Vehicle {Id}: ChargerState changed {Old} -> {New}",
                current.VehicleId, last.ChargerState, current.ChargerState);
            return true;
        }

        // Drive mode changed
        if (last.DriveMode != current.DriveMode)
        {
            _logger.LogDebug("Vehicle {Id}: DriveMode changed {Old} -> {New}",
                current.VehicleId, last.DriveMode, current.DriveMode);
            return true;
        }

        // === Battery changes ===

        if (HasSignificantChange(last.BatteryLevel, current.BatteryLevel, BatteryLevelThreshold))
        {
            _logger.LogDebug("Vehicle {Id}: BatteryLevel changed {Old} -> {New}",
                current.VehicleId, last.BatteryLevel, current.BatteryLevel);
            return true;
        }

        if (last.BatteryLimit != current.BatteryLimit)
        {
            _logger.LogDebug("Vehicle {Id}: BatteryLimit changed {Old} -> {New}",
                current.VehicleId, last.BatteryLimit, current.BatteryLimit);
            return true;
        }

        // === Movement ===

        if (HasSignificantChange(last.Speed, current.Speed, SpeedThreshold))
        {
            return true;
        }

        if (HasLocationChange(last, current))
        {
            return true;
        }

        if (HasSignificantChange(last.Odometer, current.Odometer, OdometerThreshold))
        {
            _logger.LogDebug("Vehicle {Id}: Odometer changed {Old} -> {New}",
                current.VehicleId, last.Odometer, current.Odometer);
            return true;
        }

        // === Climate ===

        if (HasSignificantChange(last.CabinTemperature, current.CabinTemperature, TemperatureThreshold))
        {
            return true;
        }

        if (last.IsPreconditioningActive != current.IsPreconditioningActive ||
            last.IsPetModeActive != current.IsPetModeActive ||
            last.IsDefrostActive != current.IsDefrostActive)
        {
            _logger.LogDebug("Vehicle {Id}: Climate feature state changed", current.VehicleId);
            return true;
        }

        // === Closures (doors, windows, etc.) ===

        if (last.AllDoorsClosed != current.AllDoorsClosed ||
            last.AllDoorsLocked != current.AllDoorsLocked ||
            last.AllWindowsClosed != current.AllWindowsClosed ||
            last.FrunkClosed != current.FrunkClosed ||
            last.FrunkLocked != current.FrunkLocked ||
            last.LiftgateClosed != current.LiftgateClosed ||
            last.TonneauClosed != current.TonneauClosed ||
            last.ChargePortOpen != current.ChargePortOpen)
        {
            _logger.LogDebug("Vehicle {Id}: Closure state changed", current.VehicleId);
            return true;
        }

        // === Gear Guard ===

        if (last.GearGuardEnabled != current.GearGuardEnabled)
        {
            return true;
        }

        // === Tire Pressure Status ===

        if (last.TirePressureStatusFrontLeft != current.TirePressureStatusFrontLeft ||
            last.TirePressureStatusFrontRight != current.TirePressureStatusFrontRight ||
            last.TirePressureStatusRearLeft != current.TirePressureStatusRearLeft ||
            last.TirePressureStatusRearRight != current.TirePressureStatusRearRight)
        {
            return true;
        }

        // Tire pressure values (only if significant change)
        if (HasSignificantChange(last.TirePressureFrontLeft, current.TirePressureFrontLeft, TirePressureThreshold) ||
            HasSignificantChange(last.TirePressureFrontRight, current.TirePressureFrontRight, TirePressureThreshold) ||
            HasSignificantChange(last.TirePressureRearLeft, current.TirePressureRearLeft, TirePressureThreshold) ||
            HasSignificantChange(last.TirePressureRearRight, current.TirePressureRearRight, TirePressureThreshold))
        {
            return true;
        }

        // === Software Updates ===

        if (last.OtaCurrentVersion != current.OtaCurrentVersion ||
            last.OtaAvailableVersion != current.OtaAvailableVersion ||
            last.OtaStatus != current.OtaStatus)
        {
            _logger.LogDebug("Vehicle {Id}: OTA status changed", current.VehicleId);
            return true;
        }

        // === Cold Weather Limits ===

        if (last.LimitedAccelCold != current.LimitedAccelCold ||
            last.LimitedRegenCold != current.LimitedRegenCold)
        {
            return true;
        }

        // === 12V Battery ===

        if (last.TwelveVoltBatteryHealth != current.TwelveVoltBatteryHealth)
        {
            return true;
        }

        // No meaningful changes detected
        return false;
    }

    private static bool HasSignificantChange(double? oldValue, double? newValue, double threshold)
    {
        if (oldValue == null && newValue == null) return false;
        if (oldValue == null || newValue == null) return true;
        return Math.Abs(oldValue.Value - newValue.Value) >= threshold;
    }

    private bool HasLocationChange(VehicleState last, VehicleState current)
    {
        // If either doesn't have location, check if that changed
        if (last.Latitude == null && current.Latitude != null) return true;
        if (last.Latitude != null && current.Latitude == null) return true;
        if (last.Latitude == null || current.Latitude == null) return false;

        // Calculate approximate distance in meters using Haversine formula
        var distance = CalculateDistanceMeters(
            last.Latitude.Value, last.Longitude!.Value,
            current.Latitude.Value, current.Longitude!.Value);

        return distance >= LocationThresholdMeters;
    }

    private static double CalculateDistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000; // Earth's radius in meters

        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return R * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;

    private record BufferedState(VehicleState State, DateTime SavedAt);
}
