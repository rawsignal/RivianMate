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
    private readonly UserLocationService _locationService;
    private readonly ILogger<ChargingTrackingService> _logger;

    public ChargingTrackingService(
        RivianMateDbContext db,
        UserLocationService locationService,
        ILogger<ChargingTrackingService> logger)
    {
        _db = db;
        _locationService = locationService;
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
        var vehicle = await _db.Vehicles.FindWithoutImageAsync(vehicleId, cancellationToken);

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

        // Determine if this is at a saved location by checking against user's locations
        var matchingLocation = await GetMatchingLocationAsync(vehicle.OwnerId, state.Latitude, state.Longitude);
        if (matchingLocation != null)
        {
            session.UserLocationId = matchingLocation.Id;
            session.LocationName = matchingLocation.Name;
            session.IsHomeCharging = matchingLocation.Name.Equals("Home", StringComparison.OrdinalIgnoreCase) || matchingLocation.IsDefault;
        }

        _db.ChargingSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Started charging session {SessionId} for vehicle {VehicleId} at {Battery}%, location: ({Lat}, {Lng}), locationName: {LocationName}",
            session.Id, vehicleId, session.StartBatteryLevel, state.Latitude, state.Longitude, session.LocationName ?? "Unknown");
    }

    private async Task UpdateChargingSessionAsync(
        ChargingSession session,
        Vehicle vehicle,
        VehicleState state,
        CancellationToken cancellationToken)
    {
        bool hasChanges = false;

        // Update charge type if we can determine it more accurately
        var newChargeType = DetermineChargeType(state, session.ChargeType);
        if (newChargeType != ChargeType.Unknown && session.ChargeType == ChargeType.Unknown)
        {
            session.ChargeType = newChargeType;
            hasChanges = true;
        }

        // Track current battery level for live progress display
        if (state.BatteryLevel != null && state.BatteryLevel != session.CurrentBatteryLevel)
        {
            session.CurrentBatteryLevel = state.BatteryLevel;
            hasChanges = true;
        }

        // Track current range estimate
        if (state.RangeEstimate != null && state.RangeEstimate != session.CurrentRangeEstimate)
        {
            session.CurrentRangeEstimate = state.RangeEstimate;
            hasChanges = true;
        }

        // Update charge limit if it changed
        if (state.BatteryLimit != null && state.BatteryLimit != session.ChargeLimit)
        {
            session.ChargeLimit = state.BatteryLimit;
            hasChanges = true;
        }

        // Always update the last updated timestamp when we have changes
        if (hasChanges)
        {
            session.LastUpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogDebug(
                "Updated charging session {SessionId}: Battery={Battery}%, Range={Range}mi, ChargeLimit={Limit}%",
                session.Id, session.CurrentBatteryLevel, session.CurrentRangeEstimate, session.ChargeLimit);
        }
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

        // If location wasn't matched at start, try again now
        // (user may have added their home location during or after the charging session)
        if (session.UserLocationId == null && session.Latitude.HasValue && session.Longitude.HasValue)
        {
            var matchingLocation = await GetMatchingLocationAsync(vehicle.OwnerId, session.Latitude, session.Longitude);
            if (matchingLocation != null)
            {
                session.UserLocationId = matchingLocation.Id;
                session.LocationName = matchingLocation.Name;
                session.IsHomeCharging = matchingLocation.Name.Equals("Home", StringComparison.OrdinalIgnoreCase) || matchingLocation.IsDefault;
                _logger.LogInformation(
                    "Matched charging session {SessionId} to location '{LocationName}' at session end",
                    session.Id, matchingLocation.Name);
            }
        }

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
            "Ended charging session {SessionId} for vehicle {VehicleId}: {StartSoC}% -> {EndSoC}%, +{Energy:F1} kWh, {Duration:F1} hours, location: {Location}, isHome: {IsHome}",
            session.Id, session.VehicleId,
            session.StartBatteryLevel, session.EndBatteryLevel,
            session.EnergyAddedKwh, (session.EndTime.Value - session.StartTime).TotalHours,
            session.LocationName ?? "Unknown", session.IsHomeCharging);
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
    /// Gets a matching user location if the given coordinates are within 100m of any saved location.
    /// Returns null if no match is found.
    /// </summary>
    private async Task<UserLocation?> GetMatchingLocationAsync(Guid? ownerId, double? latitude, double? longitude)
    {
        if (ownerId == null)
        {
            _logger.LogDebug("Cannot match location: vehicle has no owner");
            return null;
        }

        if (latitude == null || longitude == null)
        {
            _logger.LogDebug("Cannot match location: charging session has no coordinates");
            return null;
        }

        try
        {
            var result = await _locationService.GetMatchingLocationAsync(latitude.Value, longitude.Value, ownerId.Value);
            if (result != null)
            {
                _logger.LogDebug(
                    "Matched charging location ({Lat}, {Lng}) to saved location '{Name}' for owner {OwnerId}",
                    latitude, longitude, result.Name, ownerId);
            }
            else
            {
                _logger.LogDebug(
                    "No saved location within 100m of charging location ({Lat}, {Lng}) for owner {OwnerId}",
                    latitude, longitude, ownerId);
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking saved locations for owner {OwnerId}", ownerId);
            return null;
        }
    }

    /// <summary>
    /// Retroactively matches charging sessions to user locations.
    /// Call this when a user adds a new saved location to update past sessions.
    /// </summary>
    public async Task<int> RematchSessionLocationsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Get all vehicles for this user
        var vehicles = await _db.Vehicles
            .Where(v => v.OwnerId == userId)
            .Select(v => v.Id)
            .ToListAsync(cancellationToken);

        if (!vehicles.Any())
            return 0;

        // Get sessions without a linked location that have coordinates
        var unmatchedSessions = await _db.ChargingSessions
            .Where(s => vehicles.Contains(s.VehicleId))
            .Where(s => s.UserLocationId == null && s.Latitude != null && s.Longitude != null)
            .ToListAsync(cancellationToken);

        if (!unmatchedSessions.Any())
            return 0;

        int matched = 0;
        foreach (var session in unmatchedSessions)
        {
            var matchingLocation = await _locationService.GetMatchingLocationAsync(
                session.Latitude!.Value, session.Longitude!.Value, userId);

            if (matchingLocation != null)
            {
                session.UserLocationId = matchingLocation.Id;
                session.LocationName = matchingLocation.Name;
                session.IsHomeCharging = matchingLocation.Name.Equals("Home", StringComparison.OrdinalIgnoreCase)
                    || matchingLocation.IsDefault;
                matched++;

                _logger.LogInformation(
                    "Retroactively matched charging session {SessionId} to location '{LocationName}'",
                    session.Id, matchingLocation.Name);
            }
        }

        if (matched > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Retroactively matched {Count} charging sessions for user {UserId}", matched, userId);
        }

        return matched;
    }

    /// <summary>
    /// Updates cached location data on charging sessions when a UserLocation is modified.
    /// Call this when a location is renamed or its IsDefault flag changes.
    /// </summary>
    public async Task<int> SyncLocationToSessionsAsync(int userLocationId, CancellationToken cancellationToken = default)
    {
        var location = await _db.UserLocations
            .FirstOrDefaultAsync(l => l.Id == userLocationId, cancellationToken);

        if (location == null)
            return 0;

        var linkedSessions = await _db.ChargingSessions
            .Where(s => s.UserLocationId == userLocationId)
            .ToListAsync(cancellationToken);

        if (!linkedSessions.Any())
            return 0;

        var isHome = location.Name.Equals("Home", StringComparison.OrdinalIgnoreCase) || location.IsDefault;

        foreach (var session in linkedSessions)
        {
            session.LocationName = location.Name;
            session.IsHomeCharging = isHome;
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Synced {Count} charging sessions to location '{Name}' (IsHome: {IsHome})",
            linkedSessions.Count, location.Name, isHome);

        return linkedSessions.Count;
    }
}
