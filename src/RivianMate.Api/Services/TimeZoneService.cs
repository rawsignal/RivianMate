using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace RivianMate.Api.Services;

/// <summary>
/// Service for managing user timezone.
/// Uses localStorage for persistence across page refreshes, with browser detection as fallback.
/// </summary>
public class TimeZoneService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<TimeZoneService> _logger;
    private TimeZoneInfo _userTimeZone = TimeZoneInfo.Utc;
    private string _userTimeZoneId = "UTC";
    private bool _initialized;

    private const string StorageKey = "rivianmate_timezone";


    public TimeZoneService(IJSRuntime jsRuntime, ILogger<TimeZoneService> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    /// <summary>
    /// Event raised when the timezone changes. Subscribe to re-render components that display times.
    /// </summary>
    public event Action? OnTimeZoneChanged;

    /// <summary>
    /// Initialize the service. Reads from localStorage first, then falls back to browser detection.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized)
            return;

        try
        {
            // First try localStorage (persists across page refreshes)
            var storedTz = await _jsRuntime.InvokeAsync<string?>("rivianMate.storage.get", StorageKey);

            if (!string.IsNullOrEmpty(storedTz))
            {
                ApplyTimeZone(storedTz);
                _initialized = true;
                OnTimeZoneChanged?.Invoke();
                return;
            }

            // Fall back to browser detection
            var browserTz = await _jsRuntime.InvokeAsync<string>("rivianMate.getTimeZone");
            ApplyTimeZone(browserTz);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Timezone initialization failed, using UTC");
        }

        _initialized = true;
        OnTimeZoneChanged?.Invoke();
    }

    /// <summary>
    /// Set the user's preferred timezone. Saves to localStorage for persistence.
    /// Pass null/empty to clear preference and use browser detection.
    /// </summary>
    public async Task SetPreferredTimeZoneAsync(string? timeZoneId)
    {
        try
        {
            if (string.IsNullOrEmpty(timeZoneId))
            {
                // Clear preference, revert to browser detection
                await _jsRuntime.InvokeVoidAsync("rivianMate.storage.remove", StorageKey);
                _logger.LogDebug("Cleared timezone preference, reverting to browser detection");

                try
                {
                    var browserTz = await _jsRuntime.InvokeAsync<string>("rivianMate.getTimeZone");
                    ApplyTimeZone(browserTz);
                }
                catch
                {
                    ApplyTimeZone("UTC");
                }

                OnTimeZoneChanged?.Invoke();
                return;
            }

            // Save to localStorage and apply
            await _jsRuntime.InvokeVoidAsync("rivianMate.storage.set", StorageKey, timeZoneId);
            _logger.LogDebug("Saved timezone to localStorage: {TimeZoneId}", timeZoneId);

            var changed = _userTimeZoneId != timeZoneId;
            ApplyTimeZone(timeZoneId);

            if (changed)
            {
                OnTimeZoneChanged?.Invoke();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set timezone preference");
        }
    }

    /// <summary>
    /// Synchronous method to set timezone (for backward compatibility).
    /// Prefer SetPreferredTimeZoneAsync which persists to localStorage.
    /// </summary>
    public void SetUserPreferredTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrEmpty(timeZoneId))
            return;

        var changed = _userTimeZoneId != timeZoneId;
        ApplyTimeZone(timeZoneId);

        if (changed)
        {
            OnTimeZoneChanged?.Invoke();
        }
    }

    private void ApplyTimeZone(string ianaId)
    {
        _userTimeZone = GetTimeZoneFromIana(ianaId);
        _userTimeZoneId = ianaId;
    }

    /// <summary>
    /// Common timezones for user selection in settings.
    /// </summary>
    public static IReadOnlyList<(string Id, string DisplayName)> CommonTimeZones { get; } = new List<(string, string)>
    {
        ("Pacific/Honolulu", "Hawaii (HST)"),
        ("America/Anchorage", "Alaska (AKST)"),
        ("America/Los_Angeles", "Pacific Time (PST)"),
        ("America/Phoenix", "Arizona (MST)"),
        ("America/Denver", "Mountain Time (MST)"),
        ("America/Chicago", "Central Time (CST)"),
        ("America/New_York", "Eastern Time (EST)"),
        ("America/Halifax", "Atlantic Time (AST)"),
        ("America/St_Johns", "Newfoundland (NST)"),
        ("America/Sao_Paulo", "São Paulo (BRT)"),
        ("Atlantic/Azores", "Azores (AZOT)"),
        ("UTC", "UTC"),
        ("Europe/London", "London (GMT)"),
        ("Europe/Paris", "Paris (CET)"),
        ("Europe/Berlin", "Berlin (CET)"),
        ("Europe/Helsinki", "Helsinki (EET)"),
        ("Europe/Moscow", "Moscow (MSK)"),
        ("Asia/Dubai", "Dubai (GST)"),
        ("Asia/Kolkata", "India (IST)"),
        ("Asia/Bangkok", "Bangkok (ICT)"),
        ("Asia/Singapore", "Singapore (SGT)"),
        ("Asia/Shanghai", "China (CST)"),
        ("Asia/Tokyo", "Tokyo (JST)"),
        ("Asia/Seoul", "Seoul (KST)"),
        ("Australia/Perth", "Perth (AWST)"),
        ("Australia/Adelaide", "Adelaide (ACST)"),
        ("Australia/Sydney", "Sydney (AEST)"),
        ("Pacific/Auckland", "New Zealand (NZST)")
    };

    /// <summary>
    /// Get the current timezone ID (IANA format).
    /// </summary>
    public string TimeZoneId => _userTimeZoneId;

    /// <summary>
    /// Get the current timezone info.
    /// </summary>
    public TimeZoneInfo TimeZone => _userTimeZone;

    /// <summary>
    /// Convert a UTC DateTime to the user's local time.
    /// All timestamps from the database are stored as UTC.
    /// </summary>
    public DateTime ToLocalTime(DateTime utcDateTime)
    {
        var utc = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, _userTimeZone);
    }

    /// <summary>
    /// Convert a local DateTime to UTC.
    /// </summary>
    public DateTime ToUtc(DateTime localDateTime)
    {
        if (localDateTime.Kind == DateTimeKind.Utc)
            return localDateTime;

        return TimeZoneInfo.ConvertTimeToUtc(localDateTime, _userTimeZone);
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
    /// </summary>
    public (string Relative, string Full) FormatRelativeWithFull(DateTime utcDateTime)
    {
        return (FormatRelative(utcDateTime), FormatLocal(utcDateTime, "f"));
    }

    /// <summary>
    /// Convert IANA timezone ID to TimeZoneInfo.
    /// </summary>
    private static TimeZoneInfo GetTimeZoneFromIana(string? ianaTimeZoneId)
    {
        if (string.IsNullOrEmpty(ianaTimeZoneId))
            return TimeZoneInfo.Utc;

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

            return TimeZoneInfo.Utc;
        }
    }

    /// <summary>
    /// Convert common IANA timezone IDs to Windows IDs.
    /// </summary>
    private static bool TryConvertIanaToWindows(string ianaId, out string windowsId)
    {
        var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Americas
            ["Pacific/Honolulu"] = "Hawaiian Standard Time",
            ["America/Anchorage"] = "Alaskan Standard Time",
            ["America/Los_Angeles"] = "Pacific Standard Time",
            ["America/Phoenix"] = "US Mountain Standard Time",
            ["America/Denver"] = "Mountain Standard Time",
            ["America/Chicago"] = "Central Standard Time",
            ["America/New_York"] = "Eastern Standard Time",
            ["America/Halifax"] = "Atlantic Standard Time",
            ["America/St_Johns"] = "Newfoundland Standard Time",
            ["America/Sao_Paulo"] = "E. South America Standard Time",

            // Atlantic / UTC
            ["Atlantic/Azores"] = "Azores Standard Time",
            ["UTC"] = "UTC",

            // Europe
            ["Europe/London"] = "GMT Standard Time",
            ["Europe/Paris"] = "Romance Standard Time",
            ["Europe/Berlin"] = "W. Europe Standard Time",
            ["Europe/Helsinki"] = "FLE Standard Time",
            ["Europe/Moscow"] = "Russian Standard Time",

            // Middle East / Asia
            ["Asia/Dubai"] = "Arabian Standard Time",
            ["Asia/Kolkata"] = "India Standard Time",
            ["Asia/Bangkok"] = "SE Asia Standard Time",
            ["Asia/Singapore"] = "Singapore Standard Time",
            ["Asia/Shanghai"] = "China Standard Time",
            ["Asia/Tokyo"] = "Tokyo Standard Time",
            ["Asia/Seoul"] = "Korea Standard Time",

            // Australia / Pacific
            ["Australia/Perth"] = "W. Australia Standard Time",
            ["Australia/Adelaide"] = "Cen. Australia Standard Time",
            ["Australia/Sydney"] = "AUS Eastern Standard Time",
            ["Pacific/Auckland"] = "New Zealand Standard Time"
        };

        return mappings.TryGetValue(ianaId, out windowsId!);
    }
}
