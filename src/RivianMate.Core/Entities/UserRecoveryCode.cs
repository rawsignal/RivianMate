namespace RivianMate.Core.Entities;

/// <summary>
/// Stores hashed recovery codes for two-factor authentication backup
/// </summary>
public class UserRecoveryCode
{
    public int Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>
    /// SHA256 hash of the recovery code
    /// </summary>
    public string CodeHash { get; set; } = "";

    /// <summary>
    /// Whether this code has been used
    /// </summary>
    public bool IsUsed { get; set; }

    /// <summary>
    /// When the code was used (if used)
    /// </summary>
    public DateTime? UsedAt { get; set; }

    /// <summary>
    /// When the code was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    // Navigation property
    public ApplicationUser User { get; set; } = null!;
}
