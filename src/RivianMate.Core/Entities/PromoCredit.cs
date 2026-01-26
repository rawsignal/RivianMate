using RivianMate.Core.Interfaces;

namespace RivianMate.Core.Entities;

public class PromoCredit : IUserOwnedEntity
{
    public int Id { get; set; }

    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public int CampaignId { get; set; }
    public PromoCampaign Campaign { get; set; } = null!;

    public int? ReferralId { get; set; }
    public Referral? Referral { get; set; }

    /// <summary>
    /// Number of credit months awarded
    /// </summary>
    public int Credits { get; set; }

    /// <summary>
    /// Human-readable reason (e.g., "Referral reward - referred LOGAN")
    /// </summary>
    public required string Reason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Set when this credit is consumed by billing
    /// </summary>
    public DateTime? ConsumedAt { get; set; }
}
