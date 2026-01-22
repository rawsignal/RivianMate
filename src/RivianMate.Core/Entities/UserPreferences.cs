using RivianMate.Core.Enums;
using RivianMate.Core.Interfaces;

namespace RivianMate.Core.Entities;

/// <summary>
/// Stores user preferences for display units, home charging rate, and home location.
/// Single row per user.
/// </summary>
public class UserPreferences : IUserOwnedEntity
{
    public int Id { get; set; }

    /// <summary>
    /// The user who owns these preferences
    /// </summary>
    public Guid UserId { get; set; }

    // === Display Units ===
    // Distance and speed are linked: Miles = mph, Kilometers = km/h

    /// <summary>
    /// Unit for distance and speed display (Miles/mph or Kilometers/km/h)
    /// </summary>
    public DistanceUnit DistanceUnit { get; set; } = DistanceUnit.Miles;

    /// <summary>
    /// Unit for temperature display (Fahrenheit or Celsius)
    /// </summary>
    public TemperatureUnit TemperatureUnit { get; set; } = TemperatureUnit.Fahrenheit;

    /// <summary>
    /// Unit for tire pressure display (PSI, bar, or kPa)
    /// </summary>
    public TirePressureUnit TirePressureUnit { get; set; } = TirePressureUnit.Psi;

    // === Home Charging ===

    /// <summary>
    /// Electricity rate per kWh for home charging cost estimation
    /// </summary>
    public double? HomeElectricityRate { get; set; }

    /// <summary>
    /// Currency code for electricity rate display (USD, CAD, EUR, GBP, AUD)
    /// </summary>
    public string CurrencyCode { get; set; } = "USD";

    // === Home Location (DEPRECATED) ===
    // These fields are deprecated and will be removed in a future version.
    // Use UserLocation entities instead for multi-location support.

    /// <summary>
    /// Home location latitude.
    /// DEPRECATED: Use UserLocation entities instead.
    /// </summary>
    [Obsolete("Use UserLocation entities instead")]
    public double? HomeLatitude { get; set; }

    /// <summary>
    /// Home location longitude.
    /// DEPRECATED: Use UserLocation entities instead.
    /// </summary>
    [Obsolete("Use UserLocation entities instead")]
    public double? HomeLongitude { get; set; }

    // === Timezone ===

    /// <summary>
    /// User's preferred timezone ID (IANA format, e.g., "America/New_York").
    /// Used as fallback when browser timezone detection fails.
    /// </summary>
    public string? TimeZoneId { get; set; }

    // === Timestamps ===

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public ApplicationUser? User { get; set; }
}
