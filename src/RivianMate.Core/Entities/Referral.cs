using RivianMate.Core.Enums;
using RivianMate.Core.Interfaces;

namespace RivianMate.Core.Entities;

public class Referral : IUserOwnedEntity
{
    public int Id { get; set; }

    public int CampaignId { get; set; }
    public PromoCampaign Campaign { get; set; } = null!;

    /// <summary>
    /// The user who shared the referral code
    /// </summary>
    public Guid ReferrerId { get; set; }
    public ApplicationUser Referrer { get; set; } = null!;

    /// <summary>
    /// The user who signed up using the referral code
    /// </summary>
    public Guid ReferredUserId { get; set; }
    public ApplicationUser ReferredUser { get; set; } = null!;

    /// <summary>
    /// The referral code that was used
    /// </summary>
    public required string ReferralCode { get; set; }

    public ReferralStatus Status { get; set; } = ReferralStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? QualifiedAt { get; set; }

    public DateTime? RewardedAt { get; set; }

    /// <summary>
    /// Maps to ReferrerId for IUserOwnedEntity ownership validation.
    /// </summary>
    public Guid UserId
    {
        get => ReferrerId;
        set => ReferrerId = value;
    }
}
