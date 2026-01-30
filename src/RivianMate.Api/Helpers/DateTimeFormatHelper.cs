using RivianMate.Api.Services;

namespace RivianMate.Api.Helpers;

/// <summary>
/// Centralized date/time formatting methods used across pages and dashboard cards.
/// </summary>
public static class DateTimeFormatHelper
{
    /// <summary>
    /// Format a duration between two timestamps (e.g., "45 min", "2h 15m", "In progress").
    /// </summary>
    public static string FormatDuration(DateTime start, DateTime? end)
    {
        if (end == null) return "In progress";
        var duration = end.Value - start;
        if (duration.TotalMinutes < 60) return $"{(int)duration.TotalMinutes} min";
        return $"{(int)duration.TotalHours}h {duration.Minutes}m";
    }

    /// <summary>
    /// Format a date as "Today", "Yesterday", or "MMM d, yyyy" (e.g., "Jan 5, 2026").
    /// Used for session/drive list headers.
    /// </summary>
    public static string FormatRelativeDate(DateTime utcTime, TimeZoneService tz)
    {
        var localTime = tz.ToLocalTime(utcTime);
        var today = tz.ToLocalTime(DateTime.UtcNow).Date;
        if (localTime.Date == today) return "Today";
        if (localTime.Date == today.AddDays(-1)) return "Yesterday";
        return localTime.ToString("MMM d, yyyy");
    }

    /// <summary>
    /// Format a full date+time (e.g., "Jan 5, 2026 3:00 PM").
    /// Used for session/drive detail views.
    /// </summary>
    public static string FormatDateTime(DateTime utcTime, TimeZoneService tz)
    {
        var localTime = tz.ToLocalTime(utcTime);
        return localTime.ToString("MMM d, yyyy h:mm tt");
    }

    /// <summary>
    /// Format a timestamp as relative time (e.g., "Just now", "5m ago", "2h ago", "Yesterday, 3:00 PM").
    /// Used for activity feeds and last-updated displays.
    /// </summary>
    public static string FormatRelativeTime(DateTime utcTime, TimeZoneService? tz)
    {
        var localTime = tz?.ToLocalTime(utcTime) ?? utcTime;
        var now = tz?.ToLocalTime(DateTime.UtcNow) ?? DateTime.UtcNow;
        var diff = now - localTime;

        if (diff.TotalMinutes < 1) return "Just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        if (localTime.Date == now.Date.AddDays(-1)) return $"Yesterday, {localTime:h:mm tt}";
        return localTime.ToString("MMM d, h:mm tt");
    }

    /// <summary>
    /// Format a timestamp for dashboard card display (e.g., "Today, 3:00 PM", "Yesterday, 3:00 PM", "Jan 5, 3:00 PM").
    /// Used in ChargingCard and DrivesCard.
    /// </summary>
    public static string FormatCardTime(DateTime utcTime, TimeZoneService? tz)
    {
        var localTime = tz?.ToLocalTime(utcTime) ?? utcTime;
        var today = (tz?.ToLocalTime(DateTime.UtcNow) ?? DateTime.UtcNow).Date;
        if (localTime.Date == today) return $"Today, {localTime:h:mm tt}";
        if (localTime.Date == today.AddDays(-1)) return $"Yesterday, {localTime:h:mm tt}";
        return localTime.ToString("MMM d, h:mm tt");
    }

    /// <summary>
    /// Format a date group header (e.g., "Today", "Yesterday", "Monday", "January 5, 2026").
    /// Used in ActivityFeed full-page date grouping.
    /// </summary>
    public static string FormatDateHeader(DateTime localDate, DateTime localToday)
    {
        if (localDate == localToday) return "Today";
        if (localDate == localToday.AddDays(-1)) return "Yesterday";
        if (localDate > localToday.AddDays(-7)) return localDate.ToString("dddd");
        return localDate.ToString("MMMM d, yyyy");
    }

    /// <summary>
    /// Format as time-only (e.g., "3:00 PM").
    /// Used in ActivityFeed full-page per-item time display.
    /// </summary>
    public static string FormatTimeOnly(DateTime utcTime, TimeZoneService tz)
    {
        var localTime = tz.ToLocalTime(utcTime);
        return localTime.ToString("h:mm tt");
    }
}
