namespace RivianMate.Core.Licensing;

/// <summary>
/// RivianMate deployment modes.
/// </summary>
public enum Edition
{
    /// <summary>
    /// Self-hosted version with limited features.
    /// Supports up to 4 users.
    /// </summary>
    SelfHosted = 0,

    /// <summary>
    /// Cloud-hosted version with all features.
    /// Managed by us, unlimited users.
    /// </summary>
    Cloud = 1
}
