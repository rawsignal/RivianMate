using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RivianMate.Core.Entities;
using RivianMate.Infrastructure.Data;

namespace RivianMate.Api.Services;

/// <summary>
/// Service for reverse geocoding coordinates to addresses using Nominatim (OpenStreetMap).
/// Caches results to avoid redundant API calls and respects rate limits.
/// </summary>
public class GeocodingService
{
    private readonly IDbContextFactory<RivianMateDbContext> _dbFactory;
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeocodingService> _logger;

    // Nominatim requires a valid User-Agent and email for contact
    private const string NominatimBaseUrl = "https://nominatim.openstreetmap.org/reverse";
    private const string UserAgent = "RivianMate/1.0 (https://github.com/your-repo)";

    // Rate limiting - Nominatim allows 1 request per second
    private static readonly SemaphoreSlim RateLimiter = new(1, 1);
    private static DateTime _lastRequestTime = DateTime.MinValue;

    public GeocodingService(
        IDbContextFactory<RivianMateDbContext> dbFactory,
        HttpClient httpClient,
        ILogger<GeocodingService> logger)
    {
        _dbFactory = dbFactory;
        _httpClient = httpClient;
        _logger = logger;

        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    }

    /// <summary>
    /// Gets the address for the given coordinates, using cache first.
    /// Returns null if geocoding fails.
    /// </summary>
    public async Task<GeocodingCache?> GetAddressAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        // Round coordinates for cache lookup
        var roundedLat = GeocodingCache.RoundCoordinate(latitude);
        var roundedLon = GeocodingCache.RoundCoordinate(longitude);

        // Check cache first
        var cached = await GetFromCacheAsync(roundedLat, roundedLon, cancellationToken);
        if (cached != null)
        {
            _logger.LogDebug("Cache hit for ({Lat}, {Lon})", roundedLat, roundedLon);
            return cached;
        }

        // Fetch from Nominatim
        var result = await FetchFromNominatimAsync(latitude, longitude, cancellationToken);
        if (result != null)
        {
            // Save to cache with rounded coordinates
            result.Latitude = roundedLat;
            result.Longitude = roundedLon;
            await SaveToCacheAsync(result, cancellationToken);
        }

        return result;
    }

    /// <summary>
    /// Gets just the short address string for the given coordinates.
    /// </summary>
    public async Task<string?> GetShortAddressAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        var result = await GetAddressAsync(latitude, longitude, cancellationToken);
        return result?.ShortAddress ?? result?.Address;
    }

    /// <summary>
    /// Checks if we have a cached address for these coordinates.
    /// </summary>
    public async Task<bool> IsCachedAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        var roundedLat = GeocodingCache.RoundCoordinate(latitude);
        var roundedLon = GeocodingCache.RoundCoordinate(longitude);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.GeocodingCache
            .AnyAsync(c => c.Latitude == roundedLat && c.Longitude == roundedLon, cancellationToken);
    }

    private async Task<GeocodingCache?> GetFromCacheAsync(double roundedLat, double roundedLon, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.GeocodingCache
            .FirstOrDefaultAsync(c => c.Latitude == roundedLat && c.Longitude == roundedLon, cancellationToken);
    }

    private async Task SaveToCacheAsync(GeocodingCache entry, CancellationToken cancellationToken)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

            // Check if already exists (race condition protection)
            var existing = await db.GeocodingCache
                .FirstOrDefaultAsync(c => c.Latitude == entry.Latitude && c.Longitude == entry.Longitude, cancellationToken);

            if (existing == null)
            {
                db.GeocodingCache.Add(entry);
                await db.SaveChangesAsync(cancellationToken);
                _logger.LogDebug("Cached address for ({Lat}, {Lon}): {Address}", entry.Latitude, entry.Longitude, entry.ShortAddress);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache geocoding result for ({Lat}, {Lon})", entry.Latitude, entry.Longitude);
        }
    }

    private async Task<GeocodingCache?> FetchFromNominatimAsync(double latitude, double longitude, CancellationToken cancellationToken)
    {
        // Rate limiting - ensure at least 1 second between requests
        await RateLimiter.WaitAsync(cancellationToken);
        try
        {
            var timeSinceLastRequest = DateTime.UtcNow - _lastRequestTime;
            if (timeSinceLastRequest < TimeSpan.FromSeconds(1))
            {
                var delay = TimeSpan.FromSeconds(1) - timeSinceLastRequest;
                await Task.Delay(delay, cancellationToken);
            }

            var url = $"{NominatimBaseUrl}?lat={latitude}&lon={longitude}&format=json&addressdetails=1";

            _logger.LogDebug("Fetching address from Nominatim for ({Lat}, {Lon})", latitude, longitude);

            var response = await _httpClient.GetAsync(url, cancellationToken);
            _lastRequestTime = DateTime.UtcNow;

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Nominatim returned {StatusCode} for ({Lat}, {Lon})", response.StatusCode, latitude, longitude);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var data = JsonDocument.Parse(json);
            var root = data.RootElement;

            // Check for error
            if (root.TryGetProperty("error", out _))
            {
                _logger.LogWarning("Nominatim returned error for ({Lat}, {Lon})", latitude, longitude);
                return null;
            }

            var displayName = root.GetProperty("display_name").GetString() ?? "";

            // Extract address components
            string? city = null;
            string? state = null;
            string? country = null;
            string? road = null;
            string? houseNumber = null;

            if (root.TryGetProperty("address", out var addressObj))
            {
                // City can be under various keys
                city = GetFirstValue(addressObj, "city", "town", "village", "municipality", "hamlet");
                state = GetFirstValue(addressObj, "state", "province", "region");
                country = GetFirstValue(addressObj, "country");
                road = GetFirstValue(addressObj, "road", "street", "pedestrian", "path");
                houseNumber = GetFirstValue(addressObj, "house_number");
            }

            // Build short address
            var shortAddress = BuildShortAddress(houseNumber, road, city, state);

            return new GeocodingCache
            {
                Address = displayName,
                ShortAddress = shortAddress,
                City = city,
                State = state,
                Country = country,
                CreatedAt = DateTime.UtcNow
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching address from Nominatim for ({Lat}, {Lon})", latitude, longitude);
            return null;
        }
        finally
        {
            RateLimiter.Release();
        }
    }

    private static string? GetFirstValue(JsonElement obj, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (obj.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }
        return null;
    }

    private static string BuildShortAddress(string? houseNumber, string? road, string? city, string? state)
    {
        var parts = new List<string>();

        // Street address
        if (!string.IsNullOrEmpty(road))
        {
            if (!string.IsNullOrEmpty(houseNumber))
            {
                parts.Add($"{houseNumber} {road}");
            }
            else
            {
                parts.Add(road);
            }
        }

        // City
        if (!string.IsNullOrEmpty(city))
        {
            parts.Add(city);
        }

        // State (abbreviated if possible)
        if (!string.IsNullOrEmpty(state))
        {
            parts.Add(AbbreviateState(state));
        }

        return parts.Count > 0 ? string.Join(", ", parts) : "Unknown location";
    }

    private static string AbbreviateState(string state)
    {
        // Common US state abbreviations
        var abbreviations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Alabama", "AL" }, { "Alaska", "AK" }, { "Arizona", "AZ" }, { "Arkansas", "AR" },
            { "California", "CA" }, { "Colorado", "CO" }, { "Connecticut", "CT" }, { "Delaware", "DE" },
            { "Florida", "FL" }, { "Georgia", "GA" }, { "Hawaii", "HI" }, { "Idaho", "ID" },
            { "Illinois", "IL" }, { "Indiana", "IN" }, { "Iowa", "IA" }, { "Kansas", "KS" },
            { "Kentucky", "KY" }, { "Louisiana", "LA" }, { "Maine", "ME" }, { "Maryland", "MD" },
            { "Massachusetts", "MA" }, { "Michigan", "MI" }, { "Minnesota", "MN" }, { "Mississippi", "MS" },
            { "Missouri", "MO" }, { "Montana", "MT" }, { "Nebraska", "NE" }, { "Nevada", "NV" },
            { "New Hampshire", "NH" }, { "New Jersey", "NJ" }, { "New Mexico", "NM" }, { "New York", "NY" },
            { "North Carolina", "NC" }, { "North Dakota", "ND" }, { "Ohio", "OH" }, { "Oklahoma", "OK" },
            { "Oregon", "OR" }, { "Pennsylvania", "PA" }, { "Rhode Island", "RI" }, { "South Carolina", "SC" },
            { "South Dakota", "SD" }, { "Tennessee", "TN" }, { "Texas", "TX" }, { "Utah", "UT" },
            { "Vermont", "VT" }, { "Virginia", "VA" }, { "Washington", "WA" }, { "West Virginia", "WV" },
            { "Wisconsin", "WI" }, { "Wyoming", "WY" }, { "District of Columbia", "DC" }
        };

        return abbreviations.TryGetValue(state, out var abbr) ? abbr : state;
    }
}
