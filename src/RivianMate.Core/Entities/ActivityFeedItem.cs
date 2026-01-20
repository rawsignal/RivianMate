using RivianMate.Core.Interfaces;

namespace RivianMate.Core.Entities;

/// <summary>
/// A human-readable activity log entry for a vehicle.
/// Records state changes like door opens/closes, gear changes, etc.
/// </summary>
public class ActivityFeedItem : IVehicleOwnedEntity
{
    public int Id { get; set; }

    public int VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;

    /// <summary>
    /// When this activity occurred.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Human-readable message describing the activity.
    /// e.g., "R1S's Liftgate was opened"
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Category of activity for filtering/grouping.
    /// </summary>
    public ActivityType Type { get; set; }
}

/// <summary>
/// Categories of activities for filtering and display.
/// </summary>
public enum ActivityType
{
    Unknown = 0,

    /// <summary>Door, window, frunk, liftgate, etc. opened or closed</summary>
    Closure,

    /// <summary>Gear change (park, drive, reverse)</summary>
    Gear,

    /// <summary>Power state change (sleep, wake, ready)</summary>
    Power,

    /// <summary>Charging started, stopped, completed</summary>
    Charging,

    /// <summary>Drive started or ended</summary>
    Drive,

    /// <summary>Climate control changes</summary>
    Climate,

    /// <summary>Location-based events (arrived home, left work, etc.)</summary>
    Location,

    /// <summary>Software update events</summary>
    Software,

    /// <summary>Security events (alarm, gear guard)</summary>
    Security
}
