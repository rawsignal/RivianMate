namespace RivianMate.Core.Entities;

public class PromoCampaign
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// Campaign type: "Referral", "PromoCode", "Manual"
    /// </summary>
    public required string CampaignType { get; set; }

    /// <summary>
    /// Number of credit months awarded per reward
    /// </summary>
    public int CreditsPerReward { get; set; } = 1;

    public bool IsActive { get; set; } = true;

    public DateTime? StartsAt { get; set; }

    public DateTime? EndsAt { get; set; }

    /// <summary>
    /// Max redemptions per user (null = unlimited)
    /// </summary>
    public int? MaxRedemptionsPerUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<Referral> Referrals { get; set; } = new List<Referral>();
    public ICollection<PromoCredit> PromoCredits { get; set; } = new List<PromoCredit>();
}
