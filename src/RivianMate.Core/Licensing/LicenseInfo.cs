namespace RivianMate.Core.Licensing;

/// <summary>
/// Information about the current edition and its limits.
/// </summary>
public class LicenseInfo
{
    /// <summary>
    /// The deployment edition.
    /// </summary>
    public Edition Edition { get; init; }

    /// <summary>
    /// Maximum number of users allowed.
    /// </summary>
    public int MaxUsers { get; init; }

    /// <summary>
    /// Maximum number of vehicles allowed per user.
    /// </summary>
    public int MaxVehiclesPerUser { get; init; }

    /// <summary>
    /// Maximum number of Rivian accounts that can be linked per user.
    /// </summary>
    public int MaxRivianAccountsPerUser { get; init; }

    /// <summary>
    /// Whether this is self-hosted.
    /// </summary>
    public bool IsSelfHosted => Edition == Edition.SelfHosted;

    /// <summary>
    /// Whether this is Pro edition.
    /// </summary>
    public bool IsPro => Edition == Edition.Pro;

    /// <summary>
    /// Check if a specific feature is enabled.
    /// </summary>
    public bool HasFeature(string feature)
    {
        return Features.GetFeaturesForEdition(Edition).Contains(feature);
    }

    /// <summary>
    /// Get all enabled features.
    /// </summary>
    public HashSet<string> GetEnabledFeatures()
    {
        return Features.GetFeaturesForEdition(Edition);
    }

    /// <summary>
    /// Self-hosted edition limits.
    /// </summary>
    public static LicenseInfo SelfHosted() => new()
    {
        Edition = Edition.SelfHosted,
        MaxUsers = 4,
        MaxVehiclesPerUser = 10,
        MaxRivianAccountsPerUser = 2
    };

    /// <summary>
    /// Pro edition limits.
    /// </summary>
    public static LicenseInfo Pro() => new()
    {
        Edition = Edition.Pro,
        MaxUsers = int.MaxValue,
        MaxVehiclesPerUser = int.MaxValue,
        MaxRivianAccountsPerUser = int.MaxValue
    };
}
