using Microsoft.EntityFrameworkCore;
using RivianMate.Core;
using RivianMate.Core.Entities;
using RivianMate.Core.Enums;
using RivianMate.Infrastructure.Data;
using RivianMate.Infrastructure.Nhtsa;
using RivianMate.Infrastructure.Rivian;
using RivianMate.Infrastructure.Rivian.Models;
using DriveType = RivianMate.Core.Enums.DriveType;

namespace RivianMate.Api.Services;

/// <summary>
/// Service for managing vehicles and processing vehicle state data.
/// </summary>
public class VehicleService
{
    private readonly RivianMateDbContext _db;
    private readonly VehicleStateBuffer _stateBuffer;
    private readonly ActivityFeedService _activityFeed;
    private readonly ILogger<VehicleService> _logger;

    public VehicleService(
        RivianMateDbContext db,
        VehicleStateBuffer stateBuffer,
        ActivityFeedService activityFeed,
        ILogger<VehicleService> logger)
    {
        _db = db;
        _stateBuffer = stateBuffer;
        _activityFeed = activityFeed;
        _logger = logger;
    }

    /// <summary>
    /// Sync vehicles from Rivian API to local database.
    /// </summary>
    /// <param name="user">The Rivian API user data</param>
    /// <param name="ownerId">Optional owner ID to assign to synced vehicles</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<List<Vehicle>> SyncVehiclesAsync(
        CurrentUser user,
        Guid? ownerId = null,
        CancellationToken cancellationToken = default)
    {
        if (user.Vehicles == null || !user.Vehicles.Any())
        {
            _logger.LogWarning("No vehicles found in Rivian account");
            return new List<Vehicle>();
        }

        var syncedVehicles = new List<Vehicle>();

        foreach (var rivianVehicle in user.Vehicles)
        {
            if (string.IsNullOrEmpty(rivianVehicle.Id))
                continue;

            var vehicle = await _db.Vehicles
                .FirstOrDefaultAsync(v => v.RivianVehicleId == rivianVehicle.Id, cancellationToken);

            if (vehicle == null)
            {
                vehicle = new Vehicle
                {
                    RivianVehicleId = rivianVehicle.Id,
                    CreatedAt = DateTime.UtcNow,
                    OwnerId = ownerId
                };
                _db.Vehicles.Add(vehicle);
            }
            else if (ownerId.HasValue && !vehicle.OwnerId.HasValue)
            {
                // Assign owner if vehicle doesn't have one yet
                vehicle.OwnerId = ownerId;
            }

            // Update vehicle details from Rivian
            vehicle.Vin = rivianVehicle.Vin ?? vehicle.Vin;
            vehicle.Name = rivianVehicle.Name ?? vehicle.Name;
            vehicle.LastSeenAt = DateTime.UtcNow;

            if (rivianVehicle.Vehicle != null)
            {
                vehicle.Year = rivianVehicle.Vehicle.ModelYear ?? vehicle.Year;
                vehicle.Model = ParseVehicleModel(rivianVehicle.Vehicle.Model);

                if (rivianVehicle.Vehicle.MobileConfiguration != null)
                {
                    var config = rivianVehicle.Vehicle.MobileConfiguration;
                    
                    vehicle.ExteriorColor = config.ExteriorColorOption?.OptionName ?? vehicle.ExteriorColor;
                    vehicle.InteriorColor = config.InteriorColorOption?.OptionName ?? vehicle.InteriorColor;
                    vehicle.WheelConfig = config.WheelOption?.OptionName ?? vehicle.WheelConfig;
                    vehicle.DriveType = ParseDriveType(config.DriveSystemOption?.OptionName);
                    vehicle.BatteryPack = ParseBatteryPack(config.BatteryOption?.OptionName);
                    
                    // Set original capacity based on pack type and year
                    if (vehicle.BatteryPack != BatteryPackType.Unknown)
                    {
                        vehicle.OriginalCapacityKwh = BatteryPackSpecs.GetOriginalCapacityKwh(
                            vehicle.BatteryPack, vehicle.Year);
                        vehicle.EpaRangeMiles = BatteryPackSpecs.GetEpaRangeMiles(
                            vehicle.Model, vehicle.BatteryPack, vehicle.DriveType, vehicle.Year);
                    }
                }
            }

            syncedVehicles.Add(vehicle);
        }

        await _db.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation("Synced {Count} vehicles from Rivian account", syncedVehicles.Count);
        return syncedVehicles;
    }

    /// <summary>
    /// Process and store vehicle state from Rivian API.
    /// Updates the single VehicleState record for this vehicle (upsert pattern).
    /// Records meaningful changes to the ActivityFeed for history.
    /// </summary>
    /// <param name="vehicleId">The vehicle ID</param>
    /// <param name="rivianState">The state data from Rivian</param>
    /// <param name="rawJson">Optional raw JSON for debugging</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <param name="isPartialUpdate">True if this is a partial WebSocket update that should be merged with previous state</param>
    public async Task<VehicleState?> ProcessVehicleStateAsync(
        int vehicleId,
        RivianVehicleState rivianState,
        string? rawJson = null,
        CancellationToken cancellationToken = default,
        bool isPartialUpdate = false)
    {
        var vehicle = await _db.Vehicles
            .FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken);

        if (vehicle == null)
        {
            _logger.LogWarning("Vehicle {VehicleId} not found", vehicleId);
            return null;
        }

        // Get the existing state record or create one (single record per vehicle)
        var existingState = await _db.VehicleStates
            .FirstOrDefaultAsync(s => s.VehicleId == vehicleId, cancellationToken);

        // Get previous state from buffer for change detection and merging
        var previousState = _stateBuffer.GetCurrentState(vehicleId) ?? existingState;

        // Map the incoming data
        var incomingState = MapToVehicleState(vehicleId, rivianState, rawJson);

        // For partial updates, merge with previous state to fill in missing fields
        if (isPartialUpdate && previousState != null)
        {
            incomingState = MergeWithPreviousState(incomingState, previousState, rivianState);
        }

        // Calculate projected range at 100%
        if (incomingState.BatteryLevel > 0 && incomingState.RangeEstimate > 0)
        {
            incomingState.ProjectedRangeAt100 = incomingState.RangeEstimate / (incomingState.BatteryLevel / 100.0);
        }

        // Always update the buffer with the latest merged state
        _stateBuffer.UpdateCurrentState(incomingState);

        // Check if we should persist to DB (throttle frequent updates)
        var shouldPersist = _stateBuffer.ShouldSaveState(incomingState);

        if (!shouldPersist)
        {
            _logger.LogDebug("Vehicle {VehicleId}: Throttling update (no meaningful changes)", vehicleId);
            return null;
        }

        // Record activity feed events for meaningful changes
        await _activityFeed.RecordStateChangesAsync(vehicle, previousState, incomingState, cancellationToken);

        // Upsert: update existing record or create new one
        if (existingState != null)
        {
            // Update existing record with new values
            UpdateStateRecord(existingState, incomingState);
            existingState.RawJson = rawJson;
        }
        else
        {
            // First state for this vehicle - create the record
            _db.VehicleStates.Add(incomingState);
            existingState = incomingState;
        }

        // Update vehicle last seen
        vehicle.LastSeenAt = incomingState.Timestamp;

        // Update software version if changed
        if (!string.IsNullOrEmpty(incomingState.OtaCurrentVersion) &&
            vehicle.SoftwareVersion != incomingState.OtaCurrentVersion)
        {
            _logger.LogInformation("Vehicle {VehicleId} software updated: {OldVersion} -> {NewVersion}",
                vehicleId, vehicle.SoftwareVersion, incomingState.OtaCurrentVersion);
            vehicle.SoftwareVersion = incomingState.OtaCurrentVersion;
        }

        // Update battery cell type if not already set
        if (!string.IsNullOrEmpty(incomingState.BatteryCellType) &&
            string.IsNullOrEmpty(vehicle.BatteryCellType))
        {
            _logger.LogInformation("Vehicle {VehicleId} battery cell type detected: {CellType}",
                vehicleId, incomingState.BatteryCellType);
            vehicle.BatteryCellType = incomingState.BatteryCellType;
        }

        // Detect battery pack type if not already set
        if (vehicle.BatteryPack == BatteryPackType.Unknown)
        {
            // First try VIN decoding
            var (vinBatteryPack, vinDriveType) = RivianVinDecoder.DecodeFromVin(vehicle.Vin);
            if (vinBatteryPack != BatteryPackType.Unknown)
            {
                vehicle.BatteryPack = vinBatteryPack;
                _logger.LogInformation("Vehicle {VehicleId} battery pack detected from VIN: {BatteryPack}",
                    vehicleId, vinBatteryPack);

                // Also update drive type if we got it and it's unknown
                if (vehicle.DriveType == DriveType.Unknown && vinDriveType != DriveType.Unknown)
                {
                    vehicle.DriveType = vinDriveType;
                    _logger.LogInformation("Vehicle {VehicleId} drive type detected from VIN: {DriveType}",
                        vehicleId, vinDriveType);
                }
            }
            // Fall back to capacity-based inference
            else if (incomingState.BatteryCapacityKwh != null)
            {
                var inferredPack = RivianVinDecoder.InferFromCapacity(incomingState.BatteryCapacityKwh);
                if (inferredPack != BatteryPackType.Unknown)
                {
                    vehicle.BatteryPack = inferredPack;
                    _logger.LogInformation("Vehicle {VehicleId} battery pack inferred from capacity ({Capacity} kWh): {BatteryPack}",
                        vehicleId, incomingState.BatteryCapacityKwh, inferredPack);
                }
            }

            // Update original capacity and EPA range if we now know the pack type
            if (vehicle.BatteryPack != BatteryPackType.Unknown)
            {
                vehicle.OriginalCapacityKwh = BatteryPackSpecs.GetOriginalCapacityKwh(
                    vehicle.BatteryPack, vehicle.Year);
                vehicle.EpaRangeMiles = BatteryPackSpecs.GetEpaRangeMiles(
                    vehicle.Model, vehicle.BatteryPack, vehicle.DriveType, vehicle.Year);
            }
        }

        // Set cell type based on pack type if not already set
        if (string.IsNullOrEmpty(vehicle.BatteryCellType))
        {
            string? cellType = null;

            if (vehicle.BatteryPack != BatteryPackType.Unknown)
            {
                cellType = vehicle.BatteryPack switch
                {
                    BatteryPackType.Large => "50g",
                    BatteryPackType.Max => "53g",
                    BatteryPackType.Standard when vehicle.Year >= 2025 => "LFP",
                    BatteryPackType.Standard => "50g",
                    _ => null
                };
            }
            else if (!string.IsNullOrEmpty(vehicle.Vin) && vehicle.Vin.Length >= 10)
            {
                var vinCode = char.ToUpperInvariant(vehicle.Vin[5]);
                var modelYear = RivianVinDecoder.GetModelYearFromVin(vehicle.Vin);

                cellType = vinCode switch
                {
                    'G' when modelYear >= 2025 => "LFP",
                    'G' => "50g",
                    'C' => "53g",
                    _ => "50g"
                };
            }

            if (!string.IsNullOrEmpty(cellType))
            {
                vehicle.BatteryCellType = cellType;
                _logger.LogInformation("Vehicle {VehicleId} battery cell type set: {CellType}",
                    vehicleId, vehicle.BatteryCellType);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        // Update the buffer with the persisted state
        _stateBuffer.UpdateBuffer(existingState);
        _logger.LogDebug(
            "Updated state for vehicle {VehicleId}: {BatteryLevel}% @ {Odometer} miles",
            vehicleId, existingState.BatteryLevel, existingState.Odometer);

        return existingState;
    }

    /// <summary>
    /// Map Rivian API response to our VehicleState entity.
    /// </summary>
    private VehicleState MapToVehicleState(int vehicleId, RivianVehicleState rs, string? rawJson)
    {
        var state = new VehicleState
        {
            VehicleId = vehicleId,
            Timestamp = DateTime.UtcNow,
            RawJson = rawJson,

            // Location
            Latitude = rs.GnssLocation?.Latitude,
            Longitude = rs.GnssLocation?.Longitude,
            Altitude = rs.GnssAltitude?.Value,
            Speed = rs.GnssSpeed?.Value,
            Heading = rs.GnssBearing?.Value,

            // Driver
            ActiveDriverName = rs.ActiveDriverName?.Value,

            // Battery
            BatteryLevel = rs.BatteryLevel?.Value,
            BatteryLimit = rs.BatteryLimit?.Value,
            BatteryCapacityKwh = rs.BatteryCapacity?.Value,
            RangeEstimate = KmToMiles(rs.DistanceToEmpty?.Value),  // API returns km
            TwelveVoltBatteryHealth = rs.TwelveVoltBatteryHealth?.Value,
            BatteryCellType = rs.BatteryCellType?.Value,
            BatteryNeedsLfpCalibration = ParseBoolFromString(rs.BatteryNeedsLfpCalibration?.Value),

            // Odometer (API returns in meters, convert to miles)
            Odometer = rs.VehicleMileage?.Value != null
                ? rs.VehicleMileage.Value / 1609.344  // Convert meters to miles
                : null,

            // Power & Drive
            PowerState = ParsePowerState(rs.PowerState?.Value),
            GearStatus = ParseGearStatus(rs.GearStatus?.Value),
            DriveMode = rs.DriveMode?.Value,
            IsInServiceMode = rs.ServiceMode?.Value?.ToLower() == "on",

            // Charging
            ChargerState = ParseChargerState(rs.ChargerState?.Value ?? rs.ChargerStatus?.Value),
            TimeToEndOfCharge = rs.TimeToEndOfCharge?.Value,
            ChargePortOpen = rs.ChargePortState?.Value?.ToLower() == "open",
            ChargerDerateStatus = rs.ChargerDerateStatus?.Value,

            // Cold Weather (0 = not limited, >0 = limited)
            LimitedAccelCold = rs.LimitedAccelCold?.Value > 0,
            LimitedRegenCold = rs.LimitedRegenCold?.Value > 0,

            // Climate
            CabinTemperature = rs.CabinClimateInteriorTemperature?.Value,
            ClimateTargetTemp = rs.CabinClimateDriverTemperature?.Value,
            IsPreconditioningActive = !string.IsNullOrEmpty(rs.CabinPreconditioningStatus?.Value)
                && rs.CabinPreconditioningStatus.Value.ToLower() != "undefined"
                && rs.CabinPreconditioningStatus.Value.ToLower() != "off",
            IsPetModeActive = rs.PetModeStatus?.Value?.ToLower() == "on",
            IsDefrostActive = !string.IsNullOrEmpty(rs.DefrostDefogStatus?.Value)
                && rs.DefrostDefogStatus.Value.ToLower() != "off",  // Active when not "off"

            // Closures
            AllDoorsClosed = AreAllClosed(
                rs.DoorFrontLeftClosed?.Value,
                rs.DoorFrontRightClosed?.Value,
                rs.DoorRearLeftClosed?.Value,
                rs.DoorRearRightClosed?.Value),
            AllDoorsLocked = AreAllLocked(
                rs.DoorFrontLeftLocked?.Value,
                rs.DoorFrontRightLocked?.Value,
                rs.DoorRearLeftLocked?.Value,
                rs.DoorRearRightLocked?.Value),
            AllWindowsClosed = AreAllClosed(
                rs.WindowFrontLeftClosed?.Value,
                rs.WindowFrontRightClosed?.Value,
                rs.WindowRearLeftClosed?.Value,
                rs.WindowRearRightClosed?.Value),
            FrunkClosed = ParseClosedState(rs.ClosureFrunkClosed?.Value),
            FrunkLocked = ParseLockedState(rs.ClosureFrunkLocked?.Value),
            LiftgateClosed = ParseClosedState(rs.ClosureLiftgateClosed?.Value),       // R1S
            TailgateClosed = ParseClosedState(rs.ClosureTailgateClosed?.Value),     // R1T
            TonneauClosed = ParseClosedState(rs.ClosureTonneauClosed?.Value),       // R1T
            SideBinLeftClosed = ParseClosedState(rs.ClosureSideBinLeftClosed?.Value),   // R1T gear tunnel
            SideBinLeftLocked = ParseLockedState(rs.ClosureSideBinLeftLocked?.Value),
            SideBinRightClosed = ParseClosedState(rs.ClosureSideBinRightClosed?.Value),
            SideBinRightLocked = ParseLockedState(rs.ClosureSideBinRightLocked?.Value),
            GearGuardStatus = FormatGearGuardStatus(rs.GearGuardVideoStatus?.Value),

            // Tires - Status
            TirePressureStatusFrontLeft = ParseTirePressure(rs.TirePressureStatusFrontLeft?.Value),
            TirePressureStatusFrontRight = ParseTirePressure(rs.TirePressureStatusFrontRight?.Value),
            TirePressureStatusRearLeft = ParseTirePressure(rs.TirePressureStatusRearLeft?.Value),
            TirePressureStatusRearRight = ParseTirePressure(rs.TirePressureStatusRearRight?.Value),

            // Tires - Actual PSI values (converted from bar, may be null on some vehicles/firmware)
            TirePressureFrontLeft = BarToPsi(rs.TirePressureFrontLeft?.Value),
            TirePressureFrontRight = BarToPsi(rs.TirePressureFrontRight?.Value),
            TirePressureRearLeft = BarToPsi(rs.TirePressureRearLeft?.Value),
            TirePressureRearRight = BarToPsi(rs.TirePressureRearRight?.Value),

            // OTA - prefer full version string if available
            OtaCurrentVersion = rs.OtaCurrentVersion?.Value ?? FormatOtaVersion(
                rs.OtaCurrentVersionYear?.Value,
                rs.OtaCurrentVersionWeek?.Value,
                rs.OtaCurrentVersionNumber?.Value),
            OtaAvailableVersion = rs.OtaAvailableVersion?.Value ?? FormatOtaVersion(
                rs.OtaAvailableVersionYear?.Value,
                rs.OtaAvailableVersionWeek?.Value,
                rs.OtaAvailableVersionNumber?.Value),
            OtaStatus = rs.OtaStatus?.Value ?? rs.OtaCurrentStatus?.Value,
            OtaInstallProgress = rs.OtaInstallProgress?.Value
        };

        return state;
    }

    private static bool? ParseBoolFromString(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        var lower = value.ToLower();
        if (lower == "true" || lower == "yes" || lower == "on" || lower == "1") return true;
        if (lower == "false" || lower == "no" || lower == "off" || lower == "0") return false;
        return null;
    }

    /// <summary>
    /// Get all active vehicles (legacy: no user filter).
    /// </summary>
    public async Task<List<Vehicle>> GetAllVehiclesAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Vehicles
            .Where(v => v.IsActive)
            .OrderBy(v => v.Name ?? v.RivianVehicleId)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Get all vehicles for a specific user.
    /// </summary>
    public async Task<List<Vehicle>> GetVehiclesForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.Vehicles
            .Where(v => v.IsActive && v.OwnerId == userId)
            .OrderBy(v => v.Name ?? v.RivianVehicleId)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Get all vehicles for a specific Rivian account.
    /// </summary>
    public async Task<List<Vehicle>> GetVehiclesForAccountAsync(int accountId, CancellationToken cancellationToken = default)
    {
        return await _db.Vehicles
            .Where(v => v.RivianAccountId == accountId)
            .OrderBy(v => v.Name ?? v.RivianVehicleId)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Get a vehicle by ID.
    /// </summary>
    public async Task<Vehicle?> GetVehicleAsync(int vehicleId, CancellationToken cancellationToken = default)
    {
        return await _db.Vehicles
            .FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken);
    }

    /// <summary>
    /// Get a vehicle by ID, verifying ownership.
    /// </summary>
    public async Task<Vehicle?> GetVehicleForUserAsync(int vehicleId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.Vehicles
            .FirstOrDefaultAsync(v => v.Id == vehicleId && v.OwnerId == userId, cancellationToken);
    }

    /// <summary>
    /// Get a vehicle by PublicId, verifying ownership.
    /// </summary>
    public async Task<Vehicle?> GetVehicleByPublicIdAsync(Guid publicId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.Vehicles
            .FirstOrDefaultAsync(v => v.PublicId == publicId && v.OwnerId == userId, cancellationToken);
    }

    /// <summary>
    /// Get vehicle ID from PublicId, verifying ownership.
    /// Returns null if not found or not owned by user.
    /// </summary>
    public async Task<int?> GetVehicleIdByPublicIdAsync(Guid publicId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.Vehicles
            .Where(v => v.PublicId == publicId && v.OwnerId == userId)
            .Select(v => (int?)v.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Assign ownership of a vehicle to a user.
    /// </summary>
    public async Task<bool> AssignVehicleToUserAsync(int vehicleId, Guid userId, CancellationToken cancellationToken = default)
    {
        var vehicle = await _db.Vehicles
            .FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken);

        if (vehicle == null)
            return false;

        vehicle.OwnerId = userId;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Vehicle {VehicleId} assigned to user {UserId}", vehicleId, userId);
        return true;
    }

    /// <summary>
    /// Get the current state for a vehicle.
    /// With the single-record-per-vehicle model, this is a simple lookup.
    /// </summary>
    public async Task<VehicleState?> GetLatestStateAsync(int vehicleId, CancellationToken cancellationToken = default)
    {
        return await _db.VehicleStates
            .FirstOrDefaultAsync(s => s.VehicleId == vehicleId, cancellationToken);
    }

    /// <summary>
    /// Get all charging sessions for a vehicle.
    /// </summary>
    public async Task<List<ChargingSession>> GetChargingSessionsAsync(
        int vehicleId,
        CancellationToken cancellationToken = default)
    {
        return await _db.ChargingSessions
            .Where(s => s.VehicleId == vehicleId && !s.IsActive)
            .OrderByDescending(s => s.StartTime)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Get paginated charging sessions for a vehicle.
    /// </summary>
    public async Task<(List<ChargingSession> Items, int TotalCount)> GetChargingSessionsPagedAsync(
        int vehicleId,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = _db.ChargingSessions
            .Where(s => s.VehicleId == vehicleId && !s.IsActive)
            .OrderByDescending(s => s.StartTime);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    /// <summary>
    /// Get recent charging sessions for a vehicle.
    /// </summary>
    public async Task<List<ChargingSession>> GetRecentChargingSessionsAsync(
        int vehicleId,
        int count = 10,
        CancellationToken cancellationToken = default)
    {
        return await _db.ChargingSessions
            .Where(s => s.VehicleId == vehicleId)
            .OrderByDescending(s => s.StartTime)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Get a single charging session by ID.
    /// </summary>
    public async Task<ChargingSession?> GetChargingSessionAsync(
        int sessionId,
        CancellationToken cancellationToken = default)
    {
        return await _db.ChargingSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
    }

    /// <summary>
    /// Get all completed drives for a vehicle.
    /// </summary>
    public async Task<List<Drive>> GetDrivesAsync(
        int vehicleId,
        CancellationToken cancellationToken = default)
    {
        return await _db.Drives
            .Where(d => d.VehicleId == vehicleId && !d.IsActive)
            .OrderByDescending(d => d.StartTime)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Get paginated completed drives for a vehicle.
    /// </summary>
    public async Task<(List<Drive> Items, int TotalCount)> GetDrivesPagedAsync(
        int vehicleId,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Drives
            .Where(d => d.VehicleId == vehicleId && !d.IsActive)
            .OrderByDescending(d => d.StartTime);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    /// <summary>
    /// Get recent completed drives for a vehicle.
    /// </summary>
    public async Task<List<Drive>> GetRecentDrivesAsync(
        int vehicleId,
        int count = 10,
        CancellationToken cancellationToken = default)
    {
        return await _db.Drives
            .Where(d => d.VehicleId == vehicleId && !d.IsActive)
            .OrderByDescending(d => d.StartTime)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Get a drive by ID with its positions for map display.
    /// </summary>
    public async Task<Drive?> GetDriveWithPositionsAsync(
        int driveId,
        CancellationToken cancellationToken = default)
    {
        return await _db.Drives
            .Include(d => d.Positions.OrderBy(p => p.Timestamp))
            .FirstOrDefaultAsync(d => d.Id == driveId, cancellationToken);
    }

    /// <summary>
    /// Get positions for a drive (for map plotting).
    /// </summary>
    public async Task<List<Position>> GetDrivePositionsAsync(
        int driveId,
        CancellationToken cancellationToken = default)
    {
        return await _db.Positions
            .Where(p => p.DriveId == driveId)
            .OrderBy(p => p.Timestamp)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Calculate average efficiency (mi/kWh) from recent vehicle states.
    /// </summary>
    public async Task<double?> GetAverageEfficiencyAsync(
        int vehicleId,
        CancellationToken cancellationToken = default)
    {
        // Calculate efficiency from actual drive history: total miles / total kWh
        var totals = await _db.Drives
            .Where(d => d.VehicleId == vehicleId
                && !d.IsActive
                && d.DistanceMiles != null
                && d.DistanceMiles > 0
                && d.EnergyUsedKwh != null
                && d.EnergyUsedKwh > 0)
            .GroupBy(d => 1)
            .Select(g => new
            {
                TotalMiles = g.Sum(d => d.DistanceMiles),
                TotalKwh = g.Sum(d => d.EnergyUsedKwh)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (totals == null || totals.TotalMiles == null || totals.TotalKwh == null || totals.TotalKwh <= 0)
            return null;

        return totals.TotalMiles.Value / totals.TotalKwh.Value;
    }

    // === Helper parsing methods ===

    private static VehicleModel ParseVehicleModel(string? model)
    {
        if (string.IsNullOrEmpty(model)) return VehicleModel.Unknown;
        
        return model.ToUpper() switch
        {
            "R1T" => VehicleModel.R1T,
            "R1S" => VehicleModel.R1S,
            "R2" => VehicleModel.R2,
            "R3" => VehicleModel.R3,
            _ => VehicleModel.Unknown
        };
    }

    private static DriveType ParseDriveType(string? driveSystem)
    {
        if (string.IsNullOrEmpty(driveSystem)) return DriveType.Unknown;
        
        var lower = driveSystem.ToLower();
        if (lower.Contains("quad")) return DriveType.QuadMotor;
        if (lower.Contains("tri")) return DriveType.TriMotor;
        if (lower.Contains("dual")) return DriveType.DualMotor;
        
        return DriveType.Unknown;
    }

    private static BatteryPackType ParseBatteryPack(string? batteryOption)
    {
        if (string.IsNullOrEmpty(batteryOption)) return BatteryPackType.Unknown;
        
        var lower = batteryOption.ToLower();
        if (lower.Contains("max")) return BatteryPackType.Max;
        if (lower.Contains("large")) return BatteryPackType.Large;
        if (lower.Contains("standard")) return BatteryPackType.Standard;
        
        return BatteryPackType.Unknown;
    }

    private static PowerState ParsePowerState(string? value)
    {
        if (string.IsNullOrEmpty(value)) return PowerState.Unknown;
        
        return value.ToLower() switch
        {
            "sleep" => PowerState.Sleep,
            "standby" => PowerState.Standby,
            "ready" => PowerState.Ready,
            "go" => PowerState.Go,
            "charging" => PowerState.Charging,
            _ => PowerState.Unknown
        };
    }

    private static GearStatus ParseGearStatus(string? value)
    {
        if (string.IsNullOrEmpty(value)) return GearStatus.Unknown;
        
        return value.ToLower() switch
        {
            "park" => GearStatus.Park,
            "reverse" => GearStatus.Reverse,
            "neutral" => GearStatus.Neutral,
            "drive" => GearStatus.Drive,
            _ => GearStatus.Unknown
        };
    }

    private static ChargerState ParseChargerState(string? value)
    {
        if (string.IsNullOrEmpty(value)) return ChargerState.Unknown;
        
        var lower = value.ToLower();
        if (lower.Contains("not_connected") || lower.Contains("disconnected")) return ChargerState.Disconnected;
        if (lower.Contains("charging_ready") || lower.Contains("ready")) return ChargerState.ReadyToCharge;
        if (lower.Contains("charging") && !lower.Contains("ready")) return ChargerState.Charging;
        if (lower.Contains("complete")) return ChargerState.Complete;
        if (lower.Contains("connected")) return ChargerState.Connected;
        if (lower.Contains("fault") || lower.Contains("error")) return ChargerState.Fault;
        
        return ChargerState.Unknown;
    }

    private static TirePressureStatus ParseTirePressure(string? value)
    {
        if (string.IsNullOrEmpty(value)) return TirePressureStatus.Unknown;
        
        return value.ToUpper() switch
        {
            "OK" => TirePressureStatus.Ok,
            "LOW" => TirePressureStatus.Low,
            "HIGH" => TirePressureStatus.High,
            "CRITICAL" => TirePressureStatus.Critical,
            _ => TirePressureStatus.Unknown
        };
    }

    private static bool? AreAllClosed(params string?[] values)
    {
        if (values.All(v => v == null)) return null;
        // Return false only if any value is explicitly "open"
        // Ignore null/unknown values - they don't mean "open"
        var hasOpen = values.Any(v => v?.ToLower() == "open");
        if (hasOpen) return false;
        // If we have at least one "closed" and no "open", consider all closed
        var hasClosed = values.Any(v => v?.ToLower() == "closed");
        return hasClosed ? true : null;
    }

    private static bool? AreAllLocked(params string?[] values)
    {
        if (values.All(v => v == null)) return null;
        // Return false only if any value is explicitly "unlocked"
        var hasUnlocked = values.Any(v => v?.ToLower() == "unlocked");
        if (hasUnlocked) return false;
        var hasLocked = values.Any(v => v?.ToLower() == "locked");
        return hasLocked ? true : null;
    }

    /// <summary>
    /// Parse closure state from API response.
    /// Returns true if "closed", false if "open", null otherwise.
    /// Unknown/undefined values are treated as null (unknown) rather than open.
    /// </summary>
    private static bool? ParseClosedState(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        var lower = value.ToLower();
        if (lower == "closed") return true;
        if (lower == "open") return false;
        // Unknown values (e.g., "undefined", "unknown") treated as null
        return null;
    }

    /// <summary>
    /// Parse locked state from API response.
    /// Returns true if "locked", false if "unlocked", null otherwise.
    /// </summary>
    private static bool? ParseLockedState(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        var lower = value.ToLower();
        if (lower == "locked") return true;
        if (lower == "unlocked") return false;
        return null;
    }

    /// <summary>
    /// Format Gear Guard status from API response.
    /// API returns values like "disabled", "enabled", "engaged" (with underscores replaced by spaces).
    /// </summary>
    private static string? FormatGearGuardStatus(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        // Convert snake_case to Title Case (e.g., "away_from_home" -> "Away From Home")
        return string.Join(" ", value.Split('_').Select(word =>
            char.ToUpper(word[0]) + word[1..].ToLower()));
    }

    /// <summary>
    /// Convert kilometers to miles. Rivian API returns distance in km.
    /// </summary>
    private static double? KmToMiles(double? km)
    {
        if (km == null) return null;
        return km.Value * 0.621371;
    }

    private static string? FormatOtaVersion(int? year, int? week, int? number)
    {
        if (year == null || year == 0) return null;
        return $"{year}.{week ?? 0}.{number ?? 0}";
    }

    /// <summary>
    /// Merge a partial state update with the previous state.
    /// WebSocket updates from Rivian often send different fields in separate messages,
    /// so we need to combine them to get a complete picture.
    /// </summary>
    private static VehicleState MergeWithPreviousState(VehicleState newState, VehicleState previousState, RivianVehicleState rivianState)
    {
        // Keep the new timestamp
        // For each field, if the new value is null/default, use the previous value

        // Location
        newState.Latitude ??= previousState.Latitude;
        newState.Longitude ??= previousState.Longitude;
        newState.Altitude ??= previousState.Altitude;
        newState.Speed ??= previousState.Speed;
        newState.Heading ??= previousState.Heading;

        // Driver
        if (string.IsNullOrEmpty(newState.ActiveDriverName))
            newState.ActiveDriverName = previousState.ActiveDriverName;

        // Battery
        newState.BatteryLevel ??= previousState.BatteryLevel;
        newState.BatteryLimit ??= previousState.BatteryLimit;
        newState.BatteryCapacityKwh ??= previousState.BatteryCapacityKwh;
        if (string.IsNullOrEmpty(newState.BatteryCellType))
            newState.BatteryCellType = previousState.BatteryCellType;
        if (string.IsNullOrEmpty(newState.TwelveVoltBatteryHealth))
            newState.TwelveVoltBatteryHealth = previousState.TwelveVoltBatteryHealth;

        // Range
        newState.RangeEstimate ??= previousState.RangeEstimate;
        newState.ProjectedRangeAt100 ??= previousState.ProjectedRangeAt100;

        // Charging (ChargerState is enum, use Unknown as default check)
        if (newState.ChargerState == ChargerState.Unknown)
            newState.ChargerState = previousState.ChargerState;
        newState.ChargePortOpen ??= previousState.ChargePortOpen;
        newState.TimeToEndOfCharge ??= previousState.TimeToEndOfCharge;
        if (string.IsNullOrEmpty(newState.ChargerDerateStatus))
            newState.ChargerDerateStatus = previousState.ChargerDerateStatus;

        // Power & Drive (enums, use Unknown as default check)
        if (newState.PowerState == PowerState.Unknown)
            newState.PowerState = previousState.PowerState;
        if (newState.GearStatus == GearStatus.Unknown)
            newState.GearStatus = previousState.GearStatus;
        if (string.IsNullOrEmpty(newState.DriveMode))
            newState.DriveMode = previousState.DriveMode;
        // Only update IsInServiceMode if the API actually provided a value
        if (rivianState.ServiceMode == null)
            newState.IsInServiceMode = previousState.IsInServiceMode;

        // Odometer
        newState.Odometer ??= previousState.Odometer;

        // Climate
        newState.CabinTemperature ??= previousState.CabinTemperature;
        newState.ClimateTargetTemp ??= previousState.ClimateTargetTemp;
        newState.IsPreconditioningActive ??= previousState.IsPreconditioningActive;
        newState.IsPetModeActive ??= previousState.IsPetModeActive;
        newState.IsDefrostActive ??= previousState.IsDefrostActive;

        // Cold weather
        newState.LimitedAccelCold ??= previousState.LimitedAccelCold;
        newState.LimitedRegenCold ??= previousState.LimitedRegenCold;

        // Doors & Windows
        newState.AllDoorsLocked ??= previousState.AllDoorsLocked;
        newState.AllDoorsClosed ??= previousState.AllDoorsClosed;
        newState.AllWindowsClosed ??= previousState.AllWindowsClosed;
        newState.FrunkClosed ??= previousState.FrunkClosed;
        newState.FrunkLocked ??= previousState.FrunkLocked;
        newState.LiftgateClosed ??= previousState.LiftgateClosed;
        newState.TailgateClosed ??= previousState.TailgateClosed;
        newState.TonneauClosed ??= previousState.TonneauClosed;
        newState.SideBinLeftClosed ??= previousState.SideBinLeftClosed;
        newState.SideBinLeftLocked ??= previousState.SideBinLeftLocked;
        newState.SideBinRightClosed ??= previousState.SideBinRightClosed;
        newState.SideBinRightLocked ??= previousState.SideBinRightLocked;

        // Gear Guard
        if (string.IsNullOrEmpty(newState.GearGuardStatus))
            newState.GearGuardStatus = previousState.GearGuardStatus;

        // Tires - Status (use previous if new is Unknown)
        if (newState.TirePressureStatusFrontLeft == TirePressureStatus.Unknown)
            newState.TirePressureStatusFrontLeft = previousState.TirePressureStatusFrontLeft;
        if (newState.TirePressureStatusFrontRight == TirePressureStatus.Unknown)
            newState.TirePressureStatusFrontRight = previousState.TirePressureStatusFrontRight;
        if (newState.TirePressureStatusRearLeft == TirePressureStatus.Unknown)
            newState.TirePressureStatusRearLeft = previousState.TirePressureStatusRearLeft;
        if (newState.TirePressureStatusRearRight == TirePressureStatus.Unknown)
            newState.TirePressureStatusRearRight = previousState.TirePressureStatusRearRight;

        // Tires - PSI values
        newState.TirePressureFrontLeft ??= previousState.TirePressureFrontLeft;
        newState.TirePressureFrontRight ??= previousState.TirePressureFrontRight;
        newState.TirePressureRearLeft ??= previousState.TirePressureRearLeft;
        newState.TirePressureRearRight ??= previousState.TirePressureRearRight;

        // OTA
        if (string.IsNullOrEmpty(newState.OtaCurrentVersion))
            newState.OtaCurrentVersion = previousState.OtaCurrentVersion;
        if (string.IsNullOrEmpty(newState.OtaAvailableVersion))
            newState.OtaAvailableVersion = previousState.OtaAvailableVersion;
        if (string.IsNullOrEmpty(newState.OtaStatus))
            newState.OtaStatus = previousState.OtaStatus;
        newState.OtaInstallProgress ??= previousState.OtaInstallProgress;

        return newState;
    }

    /// <summary>
    /// Update an existing VehicleState record with values from an incoming state.
    /// Used for upsert pattern where we maintain a single record per vehicle.
    /// </summary>
    private static void UpdateStateRecord(VehicleState existing, VehicleState incoming)
    {
        existing.Timestamp = incoming.Timestamp;

        // Location
        existing.Latitude = incoming.Latitude;
        existing.Longitude = incoming.Longitude;
        existing.Altitude = incoming.Altitude;
        existing.Speed = incoming.Speed;
        existing.Heading = incoming.Heading;

        // Driver
        existing.ActiveDriverName = incoming.ActiveDriverName;

        // Battery
        existing.BatteryLevel = incoming.BatteryLevel;
        existing.BatteryLimit = incoming.BatteryLimit;
        existing.BatteryCapacityKwh = incoming.BatteryCapacityKwh;
        existing.BatteryCellType = incoming.BatteryCellType;
        existing.BatteryNeedsLfpCalibration = incoming.BatteryNeedsLfpCalibration;
        existing.TwelveVoltBatteryHealth = incoming.TwelveVoltBatteryHealth;

        // Range
        existing.RangeEstimate = incoming.RangeEstimate;
        existing.ProjectedRangeAt100 = incoming.ProjectedRangeAt100;

        // Charging
        existing.ChargerState = incoming.ChargerState;
        existing.ChargePortOpen = incoming.ChargePortOpen;
        existing.TimeToEndOfCharge = incoming.TimeToEndOfCharge;
        existing.ChargerDerateStatus = incoming.ChargerDerateStatus;

        // Power & Drive
        existing.PowerState = incoming.PowerState;
        existing.GearStatus = incoming.GearStatus;
        existing.DriveMode = incoming.DriveMode;
        existing.IsInServiceMode = incoming.IsInServiceMode;

        // Odometer
        existing.Odometer = incoming.Odometer;

        // Climate
        existing.CabinTemperature = incoming.CabinTemperature;
        existing.ClimateTargetTemp = incoming.ClimateTargetTemp;
        existing.IsPreconditioningActive = incoming.IsPreconditioningActive;
        existing.IsPetModeActive = incoming.IsPetModeActive;
        existing.IsDefrostActive = incoming.IsDefrostActive;

        // Cold weather
        existing.LimitedAccelCold = incoming.LimitedAccelCold;
        existing.LimitedRegenCold = incoming.LimitedRegenCold;

        // Doors & Windows
        existing.AllDoorsLocked = incoming.AllDoorsLocked;
        existing.AllDoorsClosed = incoming.AllDoorsClosed;
        existing.AllWindowsClosed = incoming.AllWindowsClosed;
        existing.FrunkClosed = incoming.FrunkClosed;
        existing.FrunkLocked = incoming.FrunkLocked;
        existing.LiftgateClosed = incoming.LiftgateClosed;
        existing.TailgateClosed = incoming.TailgateClosed;
        existing.TonneauClosed = incoming.TonneauClosed;
        existing.SideBinLeftClosed = incoming.SideBinLeftClosed;
        existing.SideBinLeftLocked = incoming.SideBinLeftLocked;
        existing.SideBinRightClosed = incoming.SideBinRightClosed;
        existing.SideBinRightLocked = incoming.SideBinRightLocked;

        // Gear Guard
        existing.GearGuardStatus = incoming.GearGuardStatus;

        // Tires - Status
        existing.TirePressureStatusFrontLeft = incoming.TirePressureStatusFrontLeft;
        existing.TirePressureStatusFrontRight = incoming.TirePressureStatusFrontRight;
        existing.TirePressureStatusRearLeft = incoming.TirePressureStatusRearLeft;
        existing.TirePressureStatusRearRight = incoming.TirePressureStatusRearRight;

        // Tires - PSI
        existing.TirePressureFrontLeft = incoming.TirePressureFrontLeft;
        existing.TirePressureFrontRight = incoming.TirePressureFrontRight;
        existing.TirePressureRearLeft = incoming.TirePressureRearLeft;
        existing.TirePressureRearRight = incoming.TirePressureRearRight;

        // OTA
        existing.OtaCurrentVersion = incoming.OtaCurrentVersion;
        existing.OtaAvailableVersion = incoming.OtaAvailableVersion;
        existing.OtaStatus = incoming.OtaStatus;
        existing.OtaInstallProgress = incoming.OtaInstallProgress;
    }

    /// <summary>
    /// Convert tire pressure from bar to PSI.
    /// Rivian API returns pressure in bar, but PSI is more common in the US.
    /// </summary>
    private static double? BarToPsi(double? bar)
    {
        if (bar == null) return null;
        return bar.Value * 14.5038;
    }
}
