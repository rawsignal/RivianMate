using System.Text.RegularExpressions;

namespace RivianMate.Core;

/// <summary>
/// Utility for parsing vehicle configuration information from Rivian image URLs.
/// The image URLs contain encoded vehicle specifications like paint color and wheel configuration.
/// </summary>
public static class VehicleImageUrlParser
{
    // Known Rivian paint colors mapped from URL slugs to display names
    private static readonly Dictionary<string, string> PaintColorMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Current colors
        ["glacier-white"] = "Glacier White",
        ["rivian-blue"] = "Rivian Blue",
        ["el-cap-granite"] = "El Cap Granite",
        ["forest-green"] = "Forest Green",
        ["midnight"] = "Midnight",
        ["limestone"] = "Limestone",
        ["red-canyon"] = "Red Canyon",

        // Launch/Legacy colors
        ["launch-green"] = "Launch Green",
        ["compass-yellow"] = "Compass Yellow",
        ["la-silver"] = "LA Silver",

        // Alternate formats that might appear
        ["white"] = "Glacier White",
        ["blue"] = "Rivian Blue",
        ["granite"] = "El Cap Granite",
        ["green"] = "Forest Green",
        ["black"] = "Midnight",
    };

    // Known wheel configurations mapped from URL slugs to display names
    private static readonly Dictionary<string, string> WheelConfigMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // 20" wheels
        ["20-at"] = "20\" All-Terrain",
        ["20-all-terrain"] = "20\" All-Terrain",
        ["20-at-dark"] = "20\" All-Terrain Dark",
        ["20-all-terrain-dark"] = "20\" All-Terrain Dark",

        // 21" wheels
        ["21-road"] = "21\" Road",
        ["21-road-performance"] = "21\" Road Performance",

        // 22" wheels
        ["22-sport"] = "22\" Sport",
        ["22-sport-bright"] = "22\" Bright Sport",
        ["22-sport-dark"] = "22\" Sport Dark",
    };

    /// <summary>
    /// Attempts to parse the paint color from a Rivian vehicle image URL.
    /// </summary>
    /// <param name="imageUrl">The image URL from Rivian's API</param>
    /// <returns>The parsed paint color name, or null if not found</returns>
    public static string? ParsePaintColor(string? imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl))
            return null;

        // Try to find a known color slug in the URL
        foreach (var (slug, displayName) in PaintColorMap)
        {
            if (imageUrl.Contains(slug, StringComparison.OrdinalIgnoreCase))
            {
                return displayName;
            }
        }

        // Try regex patterns for common URL structures
        // Pattern: /color-name/ or _color-name_ or -color-name-
        var colorMatch = Regex.Match(imageUrl, @"[/_-](glacier-white|rivian-blue|el-cap-granite|forest-green|midnight|limestone|red-canyon|launch-green|compass-yellow|la-silver)[/_-]", RegexOptions.IgnoreCase);
        if (colorMatch.Success)
        {
            var slug = colorMatch.Groups[1].Value.ToLowerInvariant();
            if (PaintColorMap.TryGetValue(slug, out var name))
            {
                return name;
            }
        }

        return null;
    }

    /// <summary>
    /// Attempts to parse the wheel configuration from a Rivian vehicle image URL.
    /// </summary>
    /// <param name="imageUrl">The image URL from Rivian's API</param>
    /// <returns>The parsed wheel configuration, or null if not found</returns>
    public static string? ParseWheelConfig(string? imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl))
            return null;

        // Try to find a known wheel slug in the URL
        foreach (var (slug, displayName) in WheelConfigMap)
        {
            if (imageUrl.Contains(slug, StringComparison.OrdinalIgnoreCase))
            {
                return displayName;
            }
        }

        // Try regex patterns for wheel sizes
        var wheelMatch = Regex.Match(imageUrl, @"[/_-](20|21|22)[/_-]?(at|road|sport|all-terrain|performance)?[/_-]?(dark|bright)?[/_-]", RegexOptions.IgnoreCase);
        if (wheelMatch.Success)
        {
            var size = wheelMatch.Groups[1].Value;
            var type = wheelMatch.Groups[2].Value.ToLowerInvariant();
            var variant = wheelMatch.Groups[3].Value.ToLowerInvariant();

            var wheelName = size + "\"";
            wheelName += type switch
            {
                "at" or "all-terrain" => " All-Terrain",
                "road" => " Road",
                "sport" => " Sport",
                "performance" => " Road Performance",
                _ => ""
            };

            if (!string.IsNullOrEmpty(variant))
            {
                wheelName += variant == "dark" ? " Dark" : " Bright";
            }

            return wheelName;
        }

        return null;
    }

    /// <summary>
    /// Parses both paint color and wheel configuration from an image URL.
    /// </summary>
    /// <param name="imageUrl">The image URL from Rivian's API</param>
    /// <returns>Tuple of (paintColor, wheelConfig), either may be null</returns>
    public static (string? PaintColor, string? WheelConfig) ParseVehicleConfig(string? imageUrl)
    {
        return (ParsePaintColor(imageUrl), ParseWheelConfig(imageUrl));
    }
}
