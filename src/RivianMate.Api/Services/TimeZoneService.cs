using Microsoft.JSInterop;

namespace RivianMate.Api.Services;

/// <summary>
/// Service for managing user timezone detection and time conversions.
/// Uses JavaScript interop to detect the browser's timezone.
/// </summary>
public class TimeZoneService
{
    private readonly IJSRuntime _jsRuntime;
    private TimeZoneInfo? _userTimeZone;
    private string? _userTimeZoneId;
    private bool _initialized;

    public TimeZoneService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// Initialize the service by detecting the user's timezone.
    /// Call this once when the app loads (e.g., in MainLayout).
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized) return;

        try
        {
            _userTimeZoneId = await _jsRuntime.InvokeAsync<string>("rivianMate.getTimeZone");
            _userTimeZone = GetTimeZoneFromIana(_userTimeZoneId);
            _initialized = true;
        }
        catch (Exception)
        {
            // JS interop might fail during prerendering
            _userTimeZone = TimeZoneInfo.Local;
            _userTimeZoneId = _userTimeZone.Id;
        }
    }

    /// <summary>
    /// Get the user's detected timezone ID (IANA format, e.g., "America/New_York").
    /// </summary>
    public string TimeZoneId => _userTimeZoneId ?? TimeZoneInfo.Local.Id;

    /// <summary>
    /// Get the user's detected timezone info.
    /// </summary>
    public TimeZoneInfo TimeZone => _userTimeZone ?? TimeZoneInfo.Local;

    /// <summary>
    /// Convert a UTC DateTime to the user's local time.
    /// </summary>
    public DateTime ToLocalTime(DateTime utcDateTime)
    {
        if (utcDateTime.Kind == DateTimeKind.Local)
            return utcDateTime;

        var utc = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZone);
    }

    /// <summary>
    /// Convert a local DateTime to UTC.
    /// </summary>
    public DateTime ToUtc(DateTime localDateTime)
    {
        if (localDateTime.Kind == DateTimeKind.Utc)
            return localDateTime;

        return TimeZoneInfo.ConvertTimeToUtc(localDateTime, TimeZone);
    }

    /// <summary>
    /// Format a UTC DateTime to a localized string.
    /// </summary>
    public string FormatLocal(DateTime utcDateTime, string? format = null)
    {
        var local = ToLocalTime(utcDateTime);
        return format != null ? local.ToString(format) : local.ToString("g");
    }

    /// <summary>
    /// Format a DateTime as relative time (e.g., "5 minutes ago").
    /// </summary>
    public string FormatRelative(DateTime utcDateTime)
    {
        var utc = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        var now = DateTime.UtcNow;
        var diff = now - utc;

        if (diff.TotalSeconds < 60) return "just now";
        if (diff.TotalMinutes < 60)
        {
            var mins = (int)diff.TotalMinutes;
            return mins == 1 ? "1 minute ago" : $"{mins} minutes ago";
        }
        if (diff.TotalHours < 24)
        {
            var hours = (int)diff.TotalHours;
            return hours == 1 ? "1 hour ago" : $"{hours} hours ago";
        }
        if (diff.TotalDays < 7)
        {
            var days = (int)diff.TotalDays;
            return days == 1 ? "yesterday" : $"{days} days ago";
        }

        return FormatLocal(utcDateTime, "MMM d, yyyy");
    }

    /// <summary>
    /// Format a DateTime as relative time, with full date on hover.
    /// Returns both the relative string and the full local date string.
    /// </summary>
    public (string Relative, string Full) FormatRelativeWithFull(DateTime utcDateTime)
    {
        return (FormatRelative(utcDateTime), FormatLocal(utcDateTime, "f"));
    }

    /// <summary>
    /// Convert IANA timezone ID to TimeZoneInfo.
    /// Handles both Windows and IANA timezone IDs.
    /// </summary>
    private static TimeZoneInfo GetTimeZoneFromIana(string? ianaTimeZoneId)
    {
        if (string.IsNullOrEmpty(ianaTimeZoneId))
            return TimeZoneInfo.Local;

        try
        {
            // .NET 6+ supports IANA timezone IDs directly on most platforms
            return TimeZoneInfo.FindSystemTimeZoneById(ianaTimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            // Fallback: try to convert IANA to Windows timezone
            if (TryConvertIanaToWindows(ianaTimeZoneId, out var windowsId))
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
                }
                catch
                {
                    // Ignore
                }
            }

            return TimeZoneInfo.Local;
        }
    }

    /// <summary>
    /// Try to convert common IANA timezone IDs to Windows IDs.
    /// </summary>
    private static bool TryConvertIanaToWindows(string ianaId, out string windowsId)
    {
        // Common mappings - extend as needed
        var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["America/New_York"] = "Eastern Standard Time",
            ["America/Chicago"] = "Central Standard Time",
            ["America/Denver"] = "Mountain Standard Time",
            ["America/Los_Angeles"] = "Pacific Standard Time",
            ["America/Phoenix"] = "US Mountain Standard Time",
            ["America/Anchorage"] = "Alaskan Standard Time",
            ["Pacific/Honolulu"] = "Hawaiian Standard Time",
            ["Europe/London"] = "GMT Standard Time",
            ["Europe/Paris"] = "Romance Standard Time",
            ["Europe/Berlin"] = "W. Europe Standard Time",
            ["Asia/Tokyo"] = "Tokyo Standard Time",
            ["Asia/Shanghai"] = "China Standard Time",
            ["Australia/Sydney"] = "AUS Eastern Standard Time",
            ["UTC"] = "UTC"
        };

        return mappings.TryGetValue(ianaId, out windowsId!);
    }
}
