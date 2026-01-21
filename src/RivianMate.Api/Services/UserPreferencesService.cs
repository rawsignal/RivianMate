using Microsoft.EntityFrameworkCore;
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

    public UserPreferencesService(IDbContextFactory<RivianMateDbContext> dbFactory, ILogger<UserPreferencesService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    /// <summary>
    /// Gets the user's preferences. Returns default values if no preferences exist yet.
    /// </summary>
    public async Task<UserPreferences> GetPreferencesAsync(Guid userId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var preferences = await db.UserPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId);

        // Return existing preferences or create new defaults
        return preferences ?? new UserPreferences
        {
            UserId = userId,
            DistanceUnit = DistanceUnit.Miles,
            TemperatureUnit = TemperatureUnit.Fahrenheit,
            TirePressureUnit = TirePressureUnit.Psi,
            CurrencyCode = "USD"
        };
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
            existing.HomeLatitude = preferences.HomeLatitude;
            existing.HomeLongitude = preferences.HomeLongitude;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            // Create new preferences record
            preferences.CreatedAt = DateTime.UtcNow;
            preferences.UpdatedAt = DateTime.UtcNow;
            db.UserPreferences.Add(preferences);
        }

        await db.SaveChangesAsync();
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
    /// Updates only the home location
    /// </summary>
    public async Task UpdateHomeLocationAsync(Guid userId, double? latitude, double? longitude)
    {
        var preferences = await GetPreferencesAsync(userId);
        preferences.HomeLatitude = latitude;
        preferences.HomeLongitude = longitude;
        await SavePreferencesAsync(preferences);
    }

    /// <summary>
    /// Clears the home location
    /// </summary>
    public async Task ClearHomeLocationAsync(Guid userId)
    {
        await UpdateHomeLocationAsync(userId, null, null);
    }
}
