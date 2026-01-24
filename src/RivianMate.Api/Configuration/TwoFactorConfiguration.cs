namespace RivianMate.Api.Configuration;

/// <summary>
/// Configuration for two-factor authentication features
/// </summary>
public class TwoFactorConfiguration
{
    /// <summary>
    /// Whether two-factor authentication is enabled for users to set up
    /// </summary>
    public bool Enabled { get; set; } = false;
}
