using Microsoft.EntityFrameworkCore;
using RivianMate.Core.Entities;
using RivianMate.Core.Enums;
using RivianMate.Infrastructure.Data;

namespace RivianMate.Api.Services;

/// <summary>
/// Service for recording vehicle activity events with human-readable messages.
/// Detects state changes and creates ActivityFeedItem entries.
/// </summary>
public class ActivityFeedService
{
    private readonly RivianMateDbContext _db;
    private readonly ILogger<ActivityFeedService> _logger;

    public ActivityFeedService(RivianMateDbContext db, ILogger<ActivityFeedService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Detect changes between previous and current state and record activity events.
    /// </summary>
    public async Task RecordStateChangesAsync(
        Vehicle vehicle,
        VehicleState? previousState,
        VehicleState currentState,
        CancellationToken cancellationToken = default)
    {
        var vehicleName = vehicle.Name ?? vehicle.Model.ToString();
        var activities = new List<ActivityFeedItem>();
        var timestamp = currentState.Timestamp;

        // === Closures ===
        CheckClosureChange(activities, vehicle.Id, vehicleName, timestamp,
            "Liftgate", previousState?.LiftgateClosed, currentState.LiftgateClosed);
        CheckClosureChange(activities, vehicle.Id, vehicleName, timestamp,
            "Tailgate", previousState?.TailgateClosed, currentState.TailgateClosed);
        CheckClosureChange(activities, vehicle.Id, vehicleName, timestamp,
            "Frunk", previousState?.FrunkClosed, currentState.FrunkClosed);
        CheckClosureChange(activities, vehicle.Id, vehicleName, timestamp,
            "Tonneau", previousState?.TonneauClosed, currentState.TonneauClosed);
        CheckClosureChange(activities, vehicle.Id, vehicleName, timestamp,
            "Charge Port", previousState?.ChargePortOpen, currentState.ChargePortOpen, invertLogic: true);
        CheckClosureChange(activities, vehicle.Id, vehicleName, timestamp,
            "Left Gear Tunnel", previousState?.SideBinLeftClosed, currentState.SideBinLeftClosed);
        CheckClosureChange(activities, vehicle.Id, vehicleName, timestamp,
            "Right Gear Tunnel", previousState?.SideBinRightClosed, currentState.SideBinRightClosed);

        // Doors aggregate
        if (previousState?.AllDoorsClosed != currentState.AllDoorsClosed && currentState.AllDoorsClosed.HasValue)
        {
            var action = currentState.AllDoorsClosed.Value ? "closed" : "opened";
            activities.Add(CreateActivity(vehicle.Id, timestamp, ActivityType.Closure,
                $"{vehicleName}'s doors were {action}"));
        }

        // Windows aggregate
        if (previousState?.AllWindowsClosed != currentState.AllWindowsClosed && currentState.AllWindowsClosed.HasValue)
        {
            var action = currentState.AllWindowsClosed.Value ? "closed" : "opened";
            activities.Add(CreateActivity(vehicle.Id, timestamp, ActivityType.Closure,
                $"{vehicleName}'s windows were {action}"));
        }

        // Lock state
        if (previousState?.AllDoorsLocked != currentState.AllDoorsLocked && currentState.AllDoorsLocked.HasValue)
        {
            var action = currentState.AllDoorsLocked.Value ? "locked" : "unlocked";
            activities.Add(CreateActivity(vehicle.Id, timestamp, ActivityType.Security,
                $"{vehicleName} was {action}"));
        }

        // === Gear ===
        if (previousState?.GearStatus != currentState.GearStatus &&
            currentState.GearStatus != GearStatus.Unknown &&
            previousState?.GearStatus != GearStatus.Unknown)
        {
            var gearName = currentState.GearStatus switch
            {
                GearStatus.Park => "Park",
                GearStatus.Drive => "Drive",
                GearStatus.Reverse => "Reverse",
                GearStatus.Neutral => "Neutral",
                _ => currentState.GearStatus.ToString()
            };
            activities.Add(CreateActivity(vehicle.Id, timestamp, ActivityType.Gear,
                $"{vehicleName} shifted to {gearName}"));
        }

        // === Power State ===
        if (previousState?.PowerState != currentState.PowerState &&
            currentState.PowerState != PowerState.Unknown &&
            previousState?.PowerState != PowerState.Unknown)
        {
            var message = (previousState?.PowerState, currentState.PowerState) switch
            {
                (PowerState.Sleep, _) => $"{vehicleName} woke up",
                (_, PowerState.Sleep) => $"{vehicleName} went to sleep",
                (_, PowerState.Ready) => $"{vehicleName} is ready",
                (_, PowerState.Go) => $"{vehicleName} started driving",
                (_, PowerState.Charging) => $"{vehicleName} started charging",
                _ => $"{vehicleName}'s power state changed to {currentState.PowerState}"
            };
            activities.Add(CreateActivity(vehicle.Id, timestamp, ActivityType.Power, message));
        }

        // === Charging ===
        if (previousState?.ChargerState != currentState.ChargerState &&
            currentState.ChargerState != ChargerState.Unknown)
        {
            var message = currentState.ChargerState switch
            {
                ChargerState.Charging => $"{vehicleName} started charging",
                ChargerState.Complete => $"{vehicleName} finished charging",
                ChargerState.Disconnected when previousState?.ChargerState == ChargerState.Charging =>
                    $"{vehicleName} stopped charging",
                ChargerState.Connected => $"{vehicleName}'s charger was connected",
                ChargerState.Disconnected => $"{vehicleName}'s charger was disconnected",
                _ => null
            };

            if (message != null)
            {
                activities.Add(CreateActivity(vehicle.Id, timestamp, ActivityType.Charging, message));
            }
        }

        // === Climate ===
        if (previousState?.IsPreconditioningActive != currentState.IsPreconditioningActive &&
            currentState.IsPreconditioningActive.HasValue)
        {
            var action = currentState.IsPreconditioningActive.Value ? "started" : "stopped";
            activities.Add(CreateActivity(vehicle.Id, timestamp, ActivityType.Climate,
                $"{vehicleName}'s climate preconditioning {action}"));
        }

        if (previousState?.IsPetModeActive != currentState.IsPetModeActive &&
            currentState.IsPetModeActive.HasValue)
        {
            var action = currentState.IsPetModeActive.Value ? "enabled" : "disabled";
            activities.Add(CreateActivity(vehicle.Id, timestamp, ActivityType.Climate,
                $"{vehicleName}'s Pet Mode was {action}"));
        }

        // === Software Updates ===
        if (previousState?.OtaCurrentVersion != currentState.OtaCurrentVersion &&
            !string.IsNullOrEmpty(currentState.OtaCurrentVersion) &&
            !string.IsNullOrEmpty(previousState?.OtaCurrentVersion))
        {
            activities.Add(CreateActivity(vehicle.Id, timestamp, ActivityType.Software,
                $"{vehicleName} updated to software version {currentState.OtaCurrentVersion}"));
        }

        // === Gear Guard ===
        if (previousState?.GearGuardStatus != currentState.GearGuardStatus &&
            !string.IsNullOrEmpty(currentState.GearGuardStatus))
        {
            var status = currentState.GearGuardStatus.ToLower();
            if (status == "engaged")
            {
                activities.Add(CreateActivity(vehicle.Id, timestamp, ActivityType.Security,
                    $"{vehicleName}'s Gear Guard was triggered"));
            }
        }

        // Save all activities
        if (activities.Count > 0)
        {
            _db.ActivityFeed.AddRange(activities);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Recorded {Count} activity events for vehicle {VehicleId}",
                activities.Count, vehicle.Id);
        }
    }

    private void CheckClosureChange(
        List<ActivityFeedItem> activities,
        int vehicleId,
        string vehicleName,
        DateTime timestamp,
        string closureName,
        bool? previousClosed,
        bool? currentClosed,
        bool invertLogic = false)
    {
        if (previousClosed == currentClosed || !currentClosed.HasValue)
            return;

        // For most closures: true = closed, false = open
        // For charge port: true = open, false = closed (invertLogic = true)
        var isOpen = invertLogic ? currentClosed.Value : !currentClosed.Value;
        var action = isOpen ? "opened" : "closed";

        activities.Add(CreateActivity(vehicleId, timestamp, ActivityType.Closure,
            $"{vehicleName}'s {closureName} was {action}"));
    }

    private static ActivityFeedItem CreateActivity(
        int vehicleId,
        DateTime timestamp,
        ActivityType type,
        string message)
    {
        return new ActivityFeedItem
        {
            VehicleId = vehicleId,
            Timestamp = timestamp,
            Type = type,
            Message = message
        };
    }

    /// <summary>
    /// Get recent activity for a vehicle (simple count-based).
    /// </summary>
    public async Task<List<ActivityFeedItem>> GetRecentActivityAsync(
        int vehicleId,
        int count = 50,
        ActivityType? filterType = null,
        DateTime? since = null,
        DateTime? until = null,
        CancellationToken cancellationToken = default)
    {
        var query = BuildActivityQuery(vehicleId, filterType, since, until);

        return await query
            .OrderByDescending(a => a.Timestamp)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Get paginated activity for a vehicle with total count.
    /// </summary>
    public async Task<(List<ActivityFeedItem> Items, int TotalCount)> GetActivityPagedAsync(
        int vehicleId,
        int page = 1,
        int pageSize = 10,
        ActivityType? filterType = null,
        DateTime? since = null,
        DateTime? until = null,
        CancellationToken cancellationToken = default)
    {
        var query = BuildActivityQuery(vehicleId, filterType, since, until);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    private IQueryable<ActivityFeedItem> BuildActivityQuery(
        int vehicleId,
        ActivityType? filterType,
        DateTime? since,
        DateTime? until)
    {
        var query = _db.ActivityFeed
            .Where(a => a.VehicleId == vehicleId);

        if (filterType.HasValue)
        {
            query = query.Where(a => a.Type == filterType.Value);
        }

        if (since.HasValue)
        {
            query = query.Where(a => a.Timestamp >= since.Value);
        }

        if (until.HasValue)
        {
            query = query.Where(a => a.Timestamp <= until.Value);
        }

        return query;
    }
}
