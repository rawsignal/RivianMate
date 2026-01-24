namespace RivianMate.Core.Entities;

/// <summary>
/// Audit trail for security-related actions
/// </summary>
public class SecurityEvent
{
    public int Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>
    /// Type of security event (e.g., TwoFactorEnabled, RecoveryCodeUsed, TwoFactorLoginFailed)
    /// </summary>
    public string EventType { get; set; } = "";

    /// <summary>
    /// IP address of the request (if available)
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// User agent of the request (if available)
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Additional details about the event
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// When the event occurred
    /// </summary>
    public DateTime Timestamp { get; set; }

    // Navigation property
    public ApplicationUser User { get; set; } = null!;
}
