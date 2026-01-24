using System.Text;
using Hangfire;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RivianMate.Api.Configuration;
using RivianMate.Core.Entities;
using RivianMate.Infrastructure.Data;

namespace RivianMate.Api.Services.Email;

/// <summary>
/// Hangfire job that runs daily to deactivate accounts with unverified emails past the deadline.
/// </summary>
public class EmailVerificationEnforcementJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailVerificationEnforcementJob> _logger;

    public EmailVerificationEnforcementJob(
        IServiceScopeFactory scopeFactory,
        ILogger<EmailVerificationEnforcementJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Executes the enforcement job.
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    [AutomaticRetry(Attempts = 1)]
    [Queue("default")]
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("Starting email verification enforcement job");

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RivianMateDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var emailTrigger = scope.ServiceProvider.GetRequiredService<IEmailTrigger>();
        var emailConfig = scope.ServiceProvider.GetRequiredService<IOptions<EmailConfiguration>>().Value;

        // Skip if email verification is not required (self-hosted edition)
        if (!emailConfig.Verification.Required)
        {
            _logger.LogDebug("Email verification not required, skipping enforcement job");
            return;
        }

        // Find users past their deadline
        var now = DateTime.UtcNow;
        var usersToDeactivate = await db.Users
            .Where(u => !u.EmailConfirmed
                && !u.IsDeactivated
                && u.EmailVerificationDeadline.HasValue
                && u.EmailVerificationDeadline.Value < now)
            .Include(u => u.RivianAccounts)
            .ToListAsync();

        var deactivatedCount = 0;

        foreach (var user in usersToDeactivate)
        {
            try
            {
                // Deactivate the account
                user.IsDeactivated = true;
                user.DeactivationReason = "EmailNotVerified";

                // Disable all linked Rivian accounts and clear tokens
                foreach (var rivianAccount in user.RivianAccounts)
                {
                    rivianAccount.IsActive = false;
                    rivianAccount.EncryptedAccessToken = null;
                    rivianAccount.EncryptedRefreshToken = null;
                    rivianAccount.EncryptedCsrfToken = null;
                    rivianAccount.EncryptedAppSessionToken = null;
                    rivianAccount.EncryptedUserSessionToken = null;
                    rivianAccount.TokenExpiresAt = null;
                    rivianAccount.LastSyncError = "Account deactivated due to unverified email";
                }

                await db.SaveChangesAsync();

                // Generate verification link for reactivation
                var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
                var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
                var baseUrl = emailConfig.BaseUrl.TrimEnd('/');
                var verificationLink = $"{baseUrl}/verify-email?userId={user.Id}&token={encodedToken}";

                // Send deactivation email
                if (!string.IsNullOrEmpty(user.Email))
                {
                    emailTrigger.FireAccountDeactivated(user.Email, user.Id, verificationLink, user.DisplayName);
                }

                deactivatedCount++;
                _logger.LogInformation("Deactivated account for user {UserId} ({Email}) due to unverified email",
                    user.Id, user.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deactivate account for user {UserId}", user.Id);
            }
        }

        _logger.LogInformation("Email verification enforcement job completed. Deactivated {Count} accounts.", deactivatedCount);
    }
}
