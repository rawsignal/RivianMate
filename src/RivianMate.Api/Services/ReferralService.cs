using Microsoft.EntityFrameworkCore;
using RivianMate.Api.Services.Email;
using RivianMate.Core.Entities;
using RivianMate.Core.Enums;
using RivianMate.Infrastructure.Data;

namespace RivianMate.Api.Services;

public class ReferralService
{
    private readonly IDbContextFactory<RivianMateDbContext> _dbFactory;
    private readonly IEmailTrigger _emailTrigger;
    private readonly ILogger<ReferralService> _logger;

    private static readonly char[] AlphanumericChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();

    public ReferralService(
        IDbContextFactory<RivianMateDbContext> dbFactory,
        IEmailTrigger emailTrigger,
        ILogger<ReferralService> logger)
    {
        _dbFactory = dbFactory;
        _emailTrigger = emailTrigger;
        _logger = logger;
    }

    /// <summary>
    /// Get or create a referral code for a user. Lazy-generated and stored permanently.
    /// </summary>
    public async Task<string> GetOrCreateReferralCodeAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var user = await db.Users.FindAsync(new object[] { userId }, cancellationToken);
        if (user == null)
            throw new InvalidOperationException("User not found");

        if (!string.IsNullOrEmpty(user.ReferralCode))
            return user.ReferralCode;

        // Generate a new code with retry for uniqueness
        const int maxAttempts = 5;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var code = GenerateReferralCode(user.DisplayName, user.Email);

            // Check uniqueness
            var exists = await db.Users.AnyAsync(u => u.ReferralCode == code, cancellationToken);
            if (exists)
                continue;

            user.ReferralCode = code;
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Generated referral code {Code} for user {UserId}", code, userId);
            return code;
        }

        throw new InvalidOperationException("Failed to generate unique referral code after multiple attempts");
    }

    /// <summary>
    /// Register a referral when a new user signs up with a referral code.
    /// </summary>
    public async Task<bool> RegisterReferralAsync(Guid referredUserId, string code, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        // Find the referrer by code
        var referrer = await db.Users
            .FirstOrDefaultAsync(u => u.ReferralCode == code, cancellationToken);

        if (referrer == null)
        {
            _logger.LogWarning("Referral code {Code} not found", code);
            return false;
        }

        // Self-referral prevention
        if (referrer.Id == referredUserId)
        {
            _logger.LogWarning("Self-referral attempted by user {UserId}", referredUserId);
            return false;
        }

        // Single referral per user
        var existingReferral = await db.Referrals
            .AnyAsync(r => r.ReferredUserId == referredUserId, cancellationToken);

        if (existingReferral)
        {
            _logger.LogWarning("User {UserId} already has a referral record", referredUserId);
            return false;
        }

        // Find the active referral campaign
        var campaign = await db.PromoCampaigns
            .FirstOrDefaultAsync(c => c.CampaignType == "Referral" && c.IsActive
                && (c.StartsAt == null || c.StartsAt <= DateTime.UtcNow)
                && (c.EndsAt == null || c.EndsAt > DateTime.UtcNow),
                cancellationToken);

        if (campaign == null)
        {
            _logger.LogWarning("No active referral campaign found");
            return false;
        }

        var referral = new Referral
        {
            CampaignId = campaign.Id,
            ReferrerId = referrer.Id,
            ReferredUserId = referredUserId,
            ReferralCode = code,
            Status = ReferralStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        db.Referrals.Add(referral);

        // Update referred user's ReferredByUserId
        var referredUser = await db.Users.FindAsync(new object[] { referredUserId }, cancellationToken);
        if (referredUser != null)
        {
            referredUser.ReferredByUserId = referrer.Id;
        }

        await db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Referral registered: {ReferrerId} → {ReferredUserId} via code {Code}",
            referrer.Id, referredUserId, code);

        return true;
    }

    /// <summary>
    /// Check if a referred user has met both conditions (email verified + Rivian linked)
    /// and award credits to both parties if so.
    /// </summary>
    public async Task CheckAndAwardAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        // Find pending referral where this user is the referred user
        var referral = await db.Referrals
            .Include(r => r.Campaign)
            .FirstOrDefaultAsync(r => r.ReferredUserId == userId && r.Status == ReferralStatus.Pending,
                cancellationToken);

        if (referral == null)
            return;

        // Check condition 1: Email verified
        var referredUser = await db.Users.FindAsync(new object[] { userId }, cancellationToken);
        if (referredUser == null || !referredUser.EmailConfirmed)
            return;

        // Check condition 2: At least one Rivian account linked
        var hasRivianAccount = await db.RivianAccounts
            .AnyAsync(ra => ra.UserId == userId, cancellationToken);

        if (!hasRivianAccount)
            return;

        // Both conditions met - award credits
        var referrer = await db.Users.FindAsync(new object[] { referral.ReferrerId }, cancellationToken);
        if (referrer == null)
            return;

        var creditsToAward = referral.Campaign.CreditsPerReward;

        // Credit for referrer
        var referrerCredit = new PromoCredit
        {
            UserId = referral.ReferrerId,
            CampaignId = referral.CampaignId,
            ReferralId = referral.Id,
            Credits = creditsToAward,
            Reason = $"Referral reward - referred {referredUser.DisplayName ?? referredUser.Email}",
            CreatedAt = DateTime.UtcNow
        };

        // Credit for referred user
        var referredCredit = new PromoCredit
        {
            UserId = userId,
            CampaignId = referral.CampaignId,
            ReferralId = referral.Id,
            Credits = creditsToAward,
            Reason = $"Referral reward - referred by {referrer.DisplayName ?? referrer.Email}",
            CreatedAt = DateTime.UtcNow
        };

        db.PromoCredits.Add(referrerCredit);
        db.PromoCredits.Add(referredCredit);

        referral.Status = ReferralStatus.Rewarded;
        referral.QualifiedAt = DateTime.UtcNow;
        referral.RewardedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Referral rewarded: {ReferrerId} and {ReferredUserId} each received {Credits} month(s) credit",
            referral.ReferrerId, userId, creditsToAward);

        // Fire emails to both parties
        if (!string.IsNullOrEmpty(referrer.Email))
        {
            _emailTrigger.FireReferralCreditAwarded(
                referrer.Email, referrer.Id,
                referrer.DisplayName, referredUser.DisplayName ?? referredUser.Email!, creditsToAward);
        }

        if (!string.IsNullOrEmpty(referredUser.Email))
        {
            _emailTrigger.FireReferralCreditAwarded(
                referredUser.Email, referredUser.Id,
                referredUser.DisplayName, referrer.DisplayName ?? referrer.Email!, creditsToAward);
        }
    }

    /// <summary>
    /// Get referral stats for a user.
    /// </summary>
    public async Task<ReferralStats> GetReferralStatsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var referrals = await db.Referrals
            .Where(r => r.ReferrerId == userId)
            .ToListAsync(cancellationToken);

        var totalCredits = await db.PromoCredits
            .Where(c => c.UserId == userId)
            .SumAsync(c => c.Credits, cancellationToken);

        var usedCredits = await db.PromoCredits
            .Where(c => c.UserId == userId && c.ConsumedAt != null)
            .SumAsync(c => c.Credits, cancellationToken);

        return new ReferralStats
        {
            TotalReferrals = referrals.Count,
            PendingReferrals = referrals.Count(r => r.Status == ReferralStatus.Pending),
            CompletedReferrals = referrals.Count(r => r.Status == ReferralStatus.Rewarded),
            TotalCreditsEarned = totalCredits,
            AvailableCredits = totalCredits - usedCredits,
            UsedCredits = usedCredits
        };
    }

    /// <summary>
    /// Get all referrals for a user (as referrer).
    /// </summary>
    public async Task<List<ReferralInfo>> GetReferralsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.Referrals
            .Where(r => r.ReferrerId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ReferralInfo
            {
                Id = r.Id,
                ReferredUserName = r.ReferredUser.DisplayName ?? r.ReferredUser.Email!,
                Status = r.Status,
                CreatedAt = r.CreatedAt,
                RewardedAt = r.RewardedAt
            })
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Get total available (unconsumed, non-expired) credits for a user.
    /// </summary>
    public async Task<int> GetAvailableCreditsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.PromoCredits
            .Where(c => c.UserId == userId
                && c.ConsumedAt == null
                && (c.ExpiresAt == null || c.ExpiresAt > DateTime.UtcNow))
            .SumAsync(c => c.Credits, cancellationToken);
    }

    private static string GenerateReferralCode(string? displayName, string? email)
    {
        // Get the base name
        string baseName;
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            baseName = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        }
        else if (!string.IsNullOrEmpty(email))
        {
            baseName = email.Split('@')[0];
        }
        else
        {
            baseName = "USER";
        }

        // Uppercase and clean (letters/digits only)
        baseName = new string(baseName.ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
        if (baseName.Length == 0)
            baseName = "USER";
        if (baseName.Length > 10)
            baseName = baseName[..10];

        // Generate 4 random alphanumeric suffix
        var random = Random.Shared;
        var suffix = new char[4];
        for (var i = 0; i < 4; i++)
        {
            suffix[i] = AlphanumericChars[random.Next(AlphanumericChars.Length)];
        }

        return $"{baseName}-{new string(suffix)}";
    }
}

public class ReferralStats
{
    public int TotalReferrals { get; set; }
    public int PendingReferrals { get; set; }
    public int CompletedReferrals { get; set; }
    public int TotalCreditsEarned { get; set; }
    public int AvailableCredits { get; set; }
    public int UsedCredits { get; set; }
}

public class ReferralInfo
{
    public int Id { get; set; }
    public string ReferredUserName { get; set; } = "";
    public ReferralStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RewardedAt { get; set; }
}
