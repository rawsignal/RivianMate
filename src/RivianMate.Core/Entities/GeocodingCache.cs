namespace RivianMate.Core.Entities;

/// <summary>
/// Caches reverse geocoding results to avoid redundant API calls.
/// Coordinates are rounded to 4 decimal places (~11m precision) for cache keys.
/// </summary>
public class GeocodingCache
{
    public int Id { get; set; }

    /// <summary>
    /// Latitude rounded to 4 decimal places
    /// </summary>
    public double Latitude { get; set; }

    /// <summary>
    /// Longitude rounded to 4 decimal places
    /// </summary>
    public double Longitude { get; set; }

    /// <summary>
    /// Full display address from reverse geocoding
    /// </summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// Short address (e.g., "123 Main St, San Francisco")
    /// </summary>
    public string? ShortAddress { get; set; }

    /// <summary>
    /// City/locality name
    /// </summary>
    public string? City { get; set; }

    /// <summary>
    /// State/region name
    /// </summary>
    public string? State { get; set; }

    /// <summary>
    /// Country name
    /// </summary>
    public string? Country { get; set; }

    /// <summary>
    /// When this cache entry was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Rounds a coordinate to 4 decimal places for cache key matching.
    /// 4 decimal places ≈ 11 meters precision.
    /// </summary>
    public static double RoundCoordinate(double coord) => Math.Round(coord, 4);
}
