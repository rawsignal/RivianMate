namespace RivianMate.Core.Licensing;

/// <summary>
/// RivianMate editions.
/// </summary>
public enum Edition
{
    /// <summary>
    /// Self-hosted version - full features for personal use.
    /// </summary>
    SelfHosted = 0,

    /// <summary>
    /// Pro version - cloud-hosted with additional features.
    /// Only available in Pro builds.
    /// </summary>
    Pro = 1
}

/// <summary>
/// Compile-time edition information.
/// </summary>
public static class BuildInfo
{
    /// <summary>
    /// The edition this binary was built for.
    /// </summary>
    public static readonly Edition Edition =
#if EDITION_PRO
        Edition.Pro;
#else
        Edition.SelfHosted;
#endif

    /// <summary>
    /// Whether this is a Pro edition build.
    /// </summary>
    public const bool IsPro =
#if EDITION_PRO
        true;
#else
        false;
#endif

    /// <summary>
    /// Whether this is a SelfHosted edition build.
    /// </summary>
    public const bool IsSelfHosted =
#if EDITION_PRO
        false;
#else
        true;
#endif

    /// <summary>
    /// Display name for the application.
    /// </summary>
    public const string DisplayName =
#if EDITION_PRO
        "RivianMate Pro";
#else
        "RivianMate";
#endif
}
