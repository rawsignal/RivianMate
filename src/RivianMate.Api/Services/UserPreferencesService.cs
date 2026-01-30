using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RivianMate.Core.Entities;
using RivianMate.Core.Enums;
using RivianMate.Infrastructure.Data;

namespace RivianMate.Api.Services;

/// <summary>
/// Supported currencies for home charging rate display
/// </summary>
public static class SupportedCurrencies
{
    public static readonly Dictionary<string, string> Codes = new()
    {
        { "USD", "US Dollar" },
        { "CAD", "Canadian Dollar" },
        { "EUR", "Euro" },
        { "GBP", "British Pound" },
        { "AUD", "Australian Dollar" }
    };

    public static readonly Dictionary<string, string> Symbols = new()
    {
        { "USD", "$" },
        { "CAD", "CA$" },
        { "EUR", "\u20ac" },
        { "GBP", "\u00a3" },
        { "AUD", "A$" }
    };
}

/// <summary>
/// Service for managing user display preferences and home charging settings.
/// Uses IDbContextFactory to create short-lived contexts for Blazor Server compatibility.
/// </summary>
public class UserPreferencesService
{
    private readonly IDbContextFactory<RivianMateDbContext> _dbFactory;
    private readonly ILogger<UserPreferencesService> _logger;
    private readonly IMemoryCache _cache;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(2);

    public UserPreferencesService(IDbContextFactory<RivianMateDbContext> dbFactory, ILogger<UserPreferencesService> logger, IMemoryCache cache)
    {
        _dbFactory = dbFactory;
        _logger = logger;
        _cache = cache;
    }

    private static string CacheKey(Guid userId) => $"prefs:{userId}";

    /// <summary>
    /// Gets the user's preferences. Returns default values if no preferences exist yet.
    /// </summary>
    public async Task<UserPreferences> GetPreferencesAsync(Guid userId)
    {
        var key = CacheKey(userId);
        if (_cache.TryGetValue(key, out UserPreferences? cached) && cached != null)
            return cached;

        await using var db = await _dbFactory.CreateDbContextAsync();

        var preferences = await db.UserPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId);

        // Return existing preferences or create new defaults
        var result = preferences ?? new UserPreferences
        {
            UserId = userId,
            DistanceUnit = DistanceUnit.Miles,
            TemperatureUnit = TemperatureUnit.Fahrenheit,
            TirePressureUnit = TirePressureUnit.Psi,
            CurrencyCode = "USD"
        };

        _cache.Set(key, result, CacheDuration);
        return result;
    }

    /// <summary>
    /// Saves the user's preferences. Creates a new record if none exists, otherwise updates.
    /// </summary>
    public async Task SavePreferencesAsync(UserPreferences preferences)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var existing = await db.UserPreferences
            .FirstOrDefaultAsync(p => p.UserId == preferences.UserId);

        if (existing != null)
        {
            // Update existing preferences
            existing.DistanceUnit = preferences.DistanceUnit;
            existing.TemperatureUnit = preferences.TemperatureUnit;
            existing.TirePressureUnit = preferences.TirePressureUnit;
            existing.HomeElectricityRate = preferences.HomeElectricityRate;
            existing.CurrencyCode = preferences.CurrencyCode;
            existing.TimeZoneId = preferences.TimeZoneId;
            existing.UpdatedAt = DateTime.UtcNow;
            // Note: HomeLatitude/HomeLongitude are deprecated - use UserLocation entities
        }
        else
        {
            // Create new preferences record
            preferences.CreatedAt = DateTime.UtcNow;
            preferences.UpdatedAt = DateTime.UtcNow;
            db.UserPreferences.Add(preferences);
        }

        await db.SaveChangesAsync();

        // Invalidate cache so next read picks up the new values
        _cache.Remove(CacheKey(preferences.UserId));

        _logger.LogInformation("Saved preferences for user {UserId}", preferences.UserId);
    }

    /// <summary>
    /// Updates only the display unit preferences
    /// </summary>
    public async Task UpdateUnitsAsync(Guid userId, DistanceUnit distanceUnit, TemperatureUnit temperatureUnit, TirePressureUnit tirePressureUnit)
    {
        var preferences = await GetPreferencesAsync(userId);
        preferences.DistanceUnit = distanceUnit;
        preferences.TemperatureUnit = temperatureUnit;
        preferences.TirePressureUnit = tirePressureUnit;
        await SavePreferencesAsync(preferences);
    }

    /// <summary>
    /// Updates only the home charging settings
    /// </summary>
    public async Task UpdateHomeChargingAsync(Guid userId, double? electricityRate, string currencyCode)
    {
        if (!SupportedCurrencies.Codes.ContainsKey(currencyCode))
        {
            throw new ArgumentException($"Unsupported currency code: {currencyCode}", nameof(currencyCode));
        }

        var preferences = await GetPreferencesAsync(userId);
        preferences.HomeElectricityRate = electricityRate;
        preferences.CurrencyCode = currencyCode;
        await SavePreferencesAsync(preferences);
    }

    /// <summary>
    /// Updates only the home location.
    /// DEPRECATED: Use UserLocationService instead.
    /// </summary>
    [Obsolete("Use UserLocationService instead for multi-location support")]
    public async Task UpdateHomeLocationAsync(Guid userId, double? latitude, double? longitude)
    {
        #pragma warning disable CS0618 // Obsolete warning
        var preferences = await GetPreferencesAsync(userId);
        preferences.HomeLatitude = latitude;
        preferences.HomeLongitude = longitude;
        await SavePreferencesAsync(preferences);
        #pragma warning restore CS0618
    }

    /// <summary>
    /// Clears the home location.
    /// DEPRECATED: Use UserLocationService instead.
    /// </summary>
    [Obsolete("Use UserLocationService instead for multi-location support")]
    public async Task ClearHomeLocationAsync(Guid userId)
    {
        #pragma warning disable CS0618 // Obsolete warning
        await UpdateHomeLocationAsync(userId, null, null);
        #pragma warning restore CS0618
    }
}
