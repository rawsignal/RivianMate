namespace RivianMate.Core.Entities;

/// <summary>
/// Application settings stored in the database.
/// Key-value store for configuration that needs to persist.
/// </summary>
public class Setting
{
    public int Id { get; set; }
    
    /// <summary>
    /// Setting key (unique)
    /// </summary>
    public required string Key { get; set; }
    
    /// <summary>
    /// Setting value (stored as string, parse as needed)
    /// </summary>
    public string? Value { get; set; }
    
    /// <summary>
    /// Is this a sensitive value that should be encrypted?
    /// </summary>
    public bool IsEncrypted { get; set; }
    
    /// <summary>
    /// When this setting was last modified
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Well-known setting keys
/// </summary>
public static class SettingKeys
{
    // === Rivian Authentication ===
    public const string RivianAccessToken = "rivian:access_token";
    public const string RivianRefreshToken = "rivian:refresh_token";
    public const string RivianUserSessionToken = "rivian:user_session_token";
    public const string RivianCsrfToken = "rivian:csrf_token";
    public const string RivianAppSessionToken = "rivian:app_session_token";
    public const string RivianTokenExpiresAt = "rivian:token_expires_at";
    public const string RivianUserId = "rivian:user_id";
    
    // === Polling Configuration ===
    public const string PollingIntervalAwakeSeconds = "polling:interval_awake_seconds";
    public const string PollingIntervalAsleepSeconds = "polling:interval_asleep_seconds";
    public const string PollingEnabled = "polling:enabled";
    
    // === Units & Display ===
    public const string UnitDistance = "units:distance"; // "miles" or "km"
    public const string UnitTemperature = "units:temperature"; // "f" or "c"
    public const string UnitEnergy = "units:energy"; // "kwh" or "wh"
    
    // === Home Location (for "home charging" detection) ===
    public const string HomeLatitude = "home:latitude";
    public const string HomeLongitude = "home:longitude";
    public const string HomeRadiusMeters = "home:radius_meters";
    
    // === Battery Health ===
    public const string BatteryHealthCalculationDays = "battery_health:calculation_days";

    // === Data Retention ===
    public const string RetainRawStateDataDays = "retention:raw_state_days";
    public const string RetainPositionDataDays = "retention:position_days";

    // === Charging Costs ===
    /// <summary>
    /// Home electricity rate in $/kWh (e.g., "0.15" for $0.15/kWh)
    /// Used to estimate cost for home charging sessions when API doesn't provide cost.
    /// </summary>
    public const string HomeElectricityRate = "charging:home_electricity_rate";
}
