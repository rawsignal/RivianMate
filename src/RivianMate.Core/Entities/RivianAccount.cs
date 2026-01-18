namespace RivianMate.Core.Entities;

/// <summary>
/// Represents a linked Rivian account for a RivianMate user.
/// Each user can have multiple Rivian accounts linked.
/// </summary>
public class RivianAccount
{
    public int Id { get; set; }

    /// <summary>
    /// The RivianMate user who owns this linked account
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Email address used to log into Rivian
    /// </summary>
    public required string RivianEmail { get; set; }

    /// <summary>
    /// Rivian user ID from the API
    /// </summary>
    public string? RivianUserId { get; set; }

    /// <summary>
    /// Display name for this account (defaults to email)
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Encrypted CSRF token
    /// </summary>
    public string? EncryptedCsrfToken { get; set; }

    /// <summary>
    /// Encrypted app session token
    /// </summary>
    public string? EncryptedAppSessionToken { get; set; }

    /// <summary>
    /// Encrypted user session token
    /// </summary>
    public string? EncryptedUserSessionToken { get; set; }

    /// <summary>
    /// Encrypted access token
    /// </summary>
    public string? EncryptedAccessToken { get; set; }

    /// <summary>
    /// Encrypted refresh token
    /// </summary>
    public string? EncryptedRefreshToken { get; set; }

    /// <summary>
    /// When the current access token expires (if known)
    /// </summary>
    public DateTime? TokenExpiresAt { get; set; }

    /// <summary>
    /// When this account was linked
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last time we successfully synced data from this account
    /// </summary>
    public DateTime? LastSyncAt { get; set; }

    /// <summary>
    /// Last error message if sync failed
    /// </summary>
    public string? LastSyncError { get; set; }

    /// <summary>
    /// Is this account active and should be polled?
    /// </summary>
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ApplicationUser User { get; set; } = null!;
    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
}
