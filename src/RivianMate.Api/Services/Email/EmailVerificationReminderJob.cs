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
/// Hangfire job that runs hourly to send reminder emails to users whose verification deadline is within 24 hours.
/// </summary>
public class EmailVerificationReminderJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailVerificationReminderJob> _logger;

    public EmailVerificationReminderJob(
        IServiceScopeFactory scopeFactory,
        ILogger<EmailVerificationReminderJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Executes the reminder job.
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    [AutomaticRetry(Attempts = 1)]
    [Queue("default")]
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("Starting email verification reminder job");

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RivianMateDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var emailTrigger = scope.ServiceProvider.GetRequiredService<IEmailTrigger>();
        var emailConfig = scope.ServiceProvider.GetRequiredService<IOptions<EmailConfiguration>>().Value;

        // Skip if email verification is not required (self-hosted edition)
        if (!emailConfig.Verification.Required)
        {
            _logger.LogDebug("Email verification not required, skipping reminder job");
            return;
        }

        var reminderHours = emailConfig.Verification.ReminderHoursBeforeDeadline;

        // Find users whose deadline is within the reminder window and haven't received a reminder yet
        var now = DateTime.UtcNow;
        var reminderWindow = now.AddHours(reminderHours);

        var usersToRemind = await db.Users
            .Where(u => !u.EmailConfirmed
                && !u.IsDeactivated
                && !u.EmailVerificationReminderSent
                && u.EmailVerificationDeadline.HasValue
                && u.EmailVerificationDeadline.Value <= reminderWindow
                && u.EmailVerificationDeadline.Value > now) // Not past deadline yet
            .ToListAsync();

        var reminderCount = 0;

        foreach (var user in usersToRemind)
        {
            try
            {
                // Generate verification link
                var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
                var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
                var baseUrl = emailConfig.BaseUrl.TrimEnd('/');
                var verificationLink = $"{baseUrl}/verify-email?userId={user.Id}&token={encodedToken}";

                // Send reminder email
                if (!string.IsNullOrEmpty(user.Email))
                {
                    emailTrigger.FireEmailVerificationReminder(user.Email, user.Id, verificationLink, user.DisplayName);
                }

                // Mark reminder as sent
                user.EmailVerificationReminderSent = true;
                await db.SaveChangesAsync();

                reminderCount++;
                _logger.LogInformation("Sent verification reminder to user {UserId} ({Email})",
                    user.Id, user.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send verification reminder to user {UserId}", user.Id);
            }
        }

        _logger.LogInformation("Email verification reminder job completed. Sent {Count} reminders.", reminderCount);
    }
}
