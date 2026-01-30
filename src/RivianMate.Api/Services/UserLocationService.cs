using Microsoft.EntityFrameworkCore;
using RivianMate.Core.Entities;
using RivianMate.Infrastructure.Data;

namespace RivianMate.Api.Services;

/// <summary>
/// Service for managing user charging locations (Home, Work, etc.).
/// Uses IDbContextFactory to create short-lived contexts for Blazor Server compatibility.
/// </summary>
public class UserLocationService
{
    private readonly IDbContextFactory<RivianMateDbContext> _dbFactory;
    private readonly ILogger<UserLocationService> _logger;

    // Fixed radius for location detection (in meters)
    // 150m accounts for GPS drift and larger properties
    private const double LocationRadiusMeters = 150;

    public UserLocationService(IDbContextFactory<RivianMateDbContext> dbFactory, ILogger<UserLocationService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    /// <summary>
    /// Gets all locations for a user.
    /// </summary>
    public async Task<List<UserLocation>> GetLocationsAsync(Guid userId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.UserLocations
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.IsDefault)
            .ThenBy(l => l.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Gets a specific location by ID for a user.
    /// </summary>
    public async Task<UserLocation?> GetLocationAsync(int locationId, Guid userId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.UserLocations
            .FirstOrDefaultAsync(l => l.Id == locationId && l.UserId == userId);
    }

    /// <summary>
    /// Adds a new location for a user.
    /// </summary>
    public async Task<UserLocation> AddLocationAsync(Guid userId, string name, double latitude, double longitude, bool isDefault = false, double? costPerKwh = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        // If this is being set as default, clear the default flag from other locations
        if (isDefault)
        {
            var existingLocations = await db.UserLocations
                .Where(l => l.UserId == userId && l.IsDefault)
                .ToListAsync();

            foreach (var loc in existingLocations)
            {
                loc.IsDefault = false;
            }
        }

        var location = new UserLocation
        {
            UserId = userId,
            Name = name.Trim(),
            Latitude = latitude,
            Longitude = longitude,
            IsDefault = isDefault,
            CostPerKwh = costPerKwh,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.UserLocations.Add(location);
        await db.SaveChangesAsync();

        _logger.LogInformation("Added location '{Name}' for user {UserId} at ({Lat}, {Lon})",
            name, userId, latitude, longitude);

        return location;
    }

    /// <summary>
    /// Updates an existing location.
    /// </summary>
    public async Task<UserLocation?> UpdateLocationAsync(int locationId, Guid userId, string name, double latitude, double longitude, bool? isDefault = null, double? costPerKwh = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var location = await db.UserLocations
            .FirstOrDefaultAsync(l => l.Id == locationId && l.UserId == userId);

        if (location == null)
        {
            _logger.LogWarning("Location {LocationId} not found for user {UserId}", locationId, userId);
            return null;
        }

        // If this is being set as default, clear the default flag from other locations
        if (isDefault == true && !location.IsDefault)
        {
            var existingDefaults = await db.UserLocations
                .Where(l => l.UserId == userId && l.IsDefault && l.Id != locationId)
                .ToListAsync();

            foreach (var loc in existingDefaults)
            {
                loc.IsDefault = false;
            }
        }

        location.Name = name.Trim();
        location.Latitude = latitude;
        location.Longitude = longitude;
        if (isDefault.HasValue)
        {
            location.IsDefault = isDefault.Value;
        }
        location.CostPerKwh = costPerKwh;
        location.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        _logger.LogInformation("Updated location '{Name}' ({LocationId}) for user {UserId}",
            name, locationId, userId);

        return location;
    }

    /// <summary>
    /// Deletes a location.
    /// </summary>
    public async Task<bool> DeleteLocationAsync(int locationId, Guid userId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var location = await db.UserLocations
            .FirstOrDefaultAsync(l => l.Id == locationId && l.UserId == userId);

        if (location == null)
        {
            _logger.LogWarning("Location {LocationId} not found for user {UserId}", locationId, userId);
            return false;
        }

        db.UserLocations.Remove(location);
        await db.SaveChangesAsync();

        _logger.LogInformation("Deleted location '{Name}' ({LocationId}) for user {UserId}",
            location.Name, locationId, userId);

        return true;
    }

    /// <summary>
    /// Gets a matching location if the given coordinates are within 100m of any saved location.
    /// Returns null if no match is found.
    /// </summary>
    public async Task<UserLocation?> GetMatchingLocationAsync(double latitude, double longitude, Guid userId)
    {
        var locations = await GetLocationsAsync(userId);

        if (!locations.Any())
        {
            _logger.LogDebug("User {UserId} has no saved locations", userId);
            return null;
        }

        UserLocation? closestLocation = null;
        double closestDistance = double.MaxValue;

        foreach (var location in locations)
        {
            var distance = CalculateDistanceMeters(latitude, longitude, location.Latitude, location.Longitude);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestLocation = location;
            }

            if (distance <= LocationRadiusMeters)
            {
                _logger.LogDebug(
                    "Location ({Lat}, {Lng}) matched '{Name}' at {Distance:F0}m for user {UserId}",
                    latitude, longitude, location.Name, distance, userId);
                return location;
            }
        }

        // Log the closest location even if not within radius (helps with debugging)
        if (closestLocation != null)
        {
            _logger.LogDebug(
                "Location ({Lat}, {Lng}) not matched - closest is '{Name}' at {Distance:F0}m (radius is {Radius}m) for user {UserId}",
                latitude, longitude, closestLocation.Name, closestDistance, LocationRadiusMeters, userId);
        }

        return null;
    }

    /// <summary>
    /// Checks if the given coordinates are within 100m of any saved location.
    /// </summary>
    public async Task<bool> IsAtSavedLocationAsync(double latitude, double longitude, Guid userId)
    {
        var matchingLocation = await GetMatchingLocationAsync(latitude, longitude, userId);
        return matchingLocation != null;
    }

    /// <summary>
    /// Gets the location name if coordinates match a saved location, otherwise returns null.
    /// </summary>
    public async Task<string?> GetLocationNameAsync(double latitude, double longitude, Guid userId)
    {
        var matchingLocation = await GetMatchingLocationAsync(latitude, longitude, userId);
        return matchingLocation?.Name;
    }

    /// <summary>
    /// Migrates existing home location from UserPreferences to UserLocations.
    /// Call this during data migration to preserve existing home location data.
    /// </summary>
    public async Task MigrateFromPreferencesAsync(Guid userId, double latitude, double longitude)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        // Check if user already has a "Home" location
        var existingHome = await db.UserLocations
            .FirstOrDefaultAsync(l => l.UserId == userId && l.Name == "Home");

        if (existingHome != null)
        {
            _logger.LogDebug("User {UserId} already has a Home location, skipping migration", userId);
            return;
        }

        // Create the migrated home location
        var location = new UserLocation
        {
            UserId = userId,
            Name = "Home",
            Latitude = latitude,
            Longitude = longitude,
            IsDefault = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.UserLocations.Add(location);
        await db.SaveChangesAsync();

        _logger.LogInformation("Migrated home location for user {UserId} from UserPreferences", userId);
    }

    /// <summary>
    /// Calculates the distance between two coordinates in meters using the Haversine formula.
    /// </summary>
    internal static double CalculateDistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double EarthRadiusMeters = 6371000;

        var lat1Rad = DegreesToRadians(lat1);
        var lat2Rad = DegreesToRadians(lat2);
        var deltaLatRad = DegreesToRadians(lat2 - lat1);
        var deltaLonRad = DegreesToRadians(lon2 - lon1);

        var a = Math.Sin(deltaLatRad / 2) * Math.Sin(deltaLatRad / 2) +
                Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                Math.Sin(deltaLonRad / 2) * Math.Sin(deltaLonRad / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return EarthRadiusMeters * c;
    }

    internal static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180;
    }
}
