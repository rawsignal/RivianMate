using Microsoft.AspNetCore.Identity;

namespace RivianMate.Core.Entities;

/// <summary>
/// Application user extending ASP.NET Core Identity
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>
    /// Display name shown in the UI
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// When the account was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last successful login time
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// When the last email verification email was sent (for cooldown)
    /// </summary>
    public DateTime? EmailVerificationSentAt { get; set; }

    /// <summary>
    /// Deadline for email verification (7 days after registration)
    /// </summary>
    public DateTime? EmailVerificationDeadline { get; set; }

    /// <summary>
    /// Whether the account has been deactivated
    /// </summary>
    public bool IsDeactivated { get; set; }

    /// <summary>
    /// Reason for deactivation (e.g., "EmailNotVerified")
    /// </summary>
    public string? DeactivationReason { get; set; }

    /// <summary>
    /// Whether a verification reminder email has been sent
    /// </summary>
    public bool EmailVerificationReminderSent { get; set; }

    /// <summary>
    /// User's unique referral code (e.g., "LOGAN-7K2M"), generated on demand
    /// </summary>
    public string? ReferralCode { get; set; }

    /// <summary>
    /// The user who referred this user (nullable)
    /// </summary>
    public Guid? ReferredByUserId { get; set; }

    /// <summary>
    /// When the user accepted the Terms of Service
    /// </summary>
    public DateTime? TermsAcceptedAt { get; set; }

    /// <summary>
    /// When the user accepted the Privacy Policy
    /// </summary>
    public DateTime? PrivacyAcceptedAt { get; set; }

    // Navigation properties
    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
    public ICollection<RivianAccount> RivianAccounts { get; set; } = new List<RivianAccount>();
}
