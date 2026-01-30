using RivianMate.Core.Entities;

namespace RivianMate.Api.Helpers;

/// <summary>
/// Centralized activity type display mappings used across ActivityFeed page and ActivityFeedCard.
/// </summary>
public static class ActivityTypeHelper
{
    public static string GetIcon(ActivityType type) => type switch
    {
        ActivityType.Closure => "door-open",
        ActivityType.Gear => "settings",
        ActivityType.Power => "power",
        ActivityType.Charging => "zap",
        ActivityType.Drive => "navigation",
        ActivityType.Climate => "thermometer",
        ActivityType.Location => "map-pin",
        ActivityType.Software => "download",
        ActivityType.Security => "shield",
        _ => "activity"
    };

    public static string GetCssClass(ActivityType type) => type switch
    {
        ActivityType.Closure => "activity-closure",
        ActivityType.Gear => "activity-gear",
        ActivityType.Power => "activity-power",
        ActivityType.Charging => "activity-charging",
        ActivityType.Drive => "activity-drive",
        ActivityType.Climate => "activity-climate",
        ActivityType.Security => "activity-security",
        _ => ""
    };
}
