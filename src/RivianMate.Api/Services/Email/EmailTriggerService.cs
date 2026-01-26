using Hangfire;
using Microsoft.Extensions.Options;
using RivianMate.Api.Configuration;

namespace RivianMate.Api.Services.Email;

/// <summary>
/// Fire-and-forget email trigger service.
/// Checks configuration and user preferences, then enqueues emails via Hangfire.
/// </summary>
public class EmailTriggerService : IEmailTrigger
{
    private readonly EmailConfiguration _config;
    private readonly IBackgroundJobClient _jobClient;
    private readonly ILogger<EmailTriggerService> _logger;

    public EmailTriggerService(
        IOptions<EmailConfiguration> config,
        IBackgroundJobClient jobClient,
        ILogger<EmailTriggerService> logger)
    {
        _config = config.Value;
        _jobClient = jobClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public void Fire(string trigger, string toEmail, object context, Guid? userId = null)
    {
        _logger.LogInformation("Email trigger fired: {Trigger} to {ToEmail}", trigger, toEmail);

        // Check if email system is enabled
        if (!_config.Enabled)
        {
            _logger.LogWarning("Email system disabled, skipping trigger: {Trigger}", trigger);
            return;
        }

        // Get trigger configuration
        if (!_config.Triggers.TryGetValue(trigger, out var triggerConfig))
        {
            _logger.LogWarning("Unknown email trigger: {Trigger}. Available triggers: {Triggers}",
                trigger, string.Join(", ", _config.Triggers.Keys));
            return;
        }

        // Check if trigger is enabled
        if (!triggerConfig.Enabled)
        {
            _logger.LogWarning("Email trigger disabled: {Trigger}", trigger);
            return;
        }

        // Convert context object to token dictionary
        var tokens = ConvertToTokens(context);

        // Create job request
        var request = new EmailJobRequest
        {
            To = toEmail,
            Trigger = trigger,
            Template = triggerConfig.Template,
            SubjectTemplate = triggerConfig.Subject,
            Tokens = tokens,
            UserId = userId
        };

        // Enqueue the job
        var jobId = _jobClient.Enqueue<SendEmailJob>(job => job.ExecuteAsync(request));
        _logger.LogInformation("Email job enqueued: Trigger={Trigger}, To={To}, JobId={JobId}", trigger, toEmail, jobId);
    }

    /// <inheritdoc />
    public void FirePasswordReset(string toEmail, string resetLink, string? userName = null)
    {
        Fire(EmailTriggers.PasswordReset, toEmail, new
        {
            UserName = userName ?? "there",
            ResetLink = resetLink,
            ExpiresIn = "24 hours"
        });
    }

    /// <inheritdoc />
    public void FireEmailVerification(string toEmail, string verificationLink, string? userName = null, int daysRemaining = 7)
    {
        Fire(EmailTriggers.EmailVerification, toEmail, new
        {
            UserName = userName ?? "there",
            VerificationLink = verificationLink,
            DaysRemaining = daysRemaining
        });
    }

    /// <inheritdoc />
    public void FireEmailVerificationReminder(string toEmail, Guid userId, string verificationLink, string? userName = null)
    {
        Fire(EmailTriggers.EmailVerificationReminder, toEmail, new
        {
            UserName = userName ?? "there",
            VerificationLink = verificationLink
        }, userId);
    }

    /// <inheritdoc />
    public void FireAccountDeactivated(string toEmail, Guid userId, string verificationLink, string? userName = null)
    {
        Fire(EmailTriggers.AccountDeactivated, toEmail, new
        {
            UserName = userName ?? "there",
            VerificationLink = verificationLink
        }, userId);
    }

    /// <inheritdoc />
    public void FirePasswordChanged(string toEmail, Guid userId, string? userName = null)
    {
        Fire(EmailTriggers.PasswordChanged, toEmail, new
        {
            UserName = userName ?? "there",
            ChangedAt = DateTime.UtcNow.ToString("MMMM d, yyyy 'at' h:mm tt 'UTC'")
        }, userId);
    }

    /// <inheritdoc />
    public void FireTwoFactorEnabled(string toEmail, Guid userId, string? userName = null)
    {
        Fire(EmailTriggers.TwoFactorEnabled, toEmail, new
        {
            UserName = userName ?? "there",
            EnabledAt = DateTime.UtcNow.ToString("MMMM d, yyyy 'at' h:mm tt 'UTC'")
        }, userId);
    }

    /// <inheritdoc />
    public void FireSecurityAlert(string toEmail, Guid userId, string alertType, object details)
    {
        var tokens = ConvertToTokens(details);
        tokens["AlertType"] = alertType;

        var request = new EmailJobRequest
        {
            To = toEmail,
            Trigger = EmailTriggers.SecurityAlert,
            Template = "SecurityAlert",
            SubjectTemplate = "Security alert for your account",
            Tokens = tokens,
            UserId = userId
        };

        if (_config.Enabled && _config.Triggers.TryGetValue(EmailTriggers.SecurityAlert, out var config) && config.Enabled)
        {
            _jobClient.Enqueue<SendEmailJob>(job => job.ExecuteAsync(request));
        }
    }

    /// <inheritdoc />
    public void FireReferralCreditAwarded(string toEmail, Guid userId, string? userName, string otherPartyName, int creditAmount)
    {
        Fire(EmailTriggers.ReferralCreditAwarded, toEmail, new
        {
            UserName = userName ?? "there",
            ReferredName = otherPartyName,
            CreditAmount = creditAmount.ToString()
        }, userId);
    }

    private static Dictionary<string, string> ConvertToTokens(object context)
    {
        var tokens = new Dictionary<string, string>();

        if (context == null)
            return tokens;

        // Use reflection to get all public properties
        var properties = context.GetType().GetProperties();
        foreach (var prop in properties)
        {
            var value = prop.GetValue(context);
            tokens[prop.Name] = value?.ToString() ?? "";
        }

        return tokens;
    }
}

/// <summary>
/// Fire-and-forget email trigger interface.
/// </summary>
public interface IEmailTrigger
{
    /// <summary>
    /// Fire an email trigger with custom context.
    /// </summary>
    /// <param name="trigger">Trigger name (from EmailTriggers constants)</param>
    /// <param name="toEmail">Recipient email address</param>
    /// <param name="context">Anonymous object with token values</param>
    /// <param name="userId">Optional user ID for logging</param>
    void Fire(string trigger, string toEmail, object context, Guid? userId = null);

    /// <summary>
    /// Fire a password reset email.
    /// </summary>
    void FirePasswordReset(string toEmail, string resetLink, string? userName = null);

    /// <summary>
    /// Fire an email verification email.
    /// </summary>
    void FireEmailVerification(string toEmail, string verificationLink, string? userName = null, int daysRemaining = 7);

    /// <summary>
    /// Fire an email verification reminder (24 hours before deadline).
    /// </summary>
    void FireEmailVerificationReminder(string toEmail, Guid userId, string verificationLink, string? userName = null);

    /// <summary>
    /// Fire an account deactivated email.
    /// </summary>
    void FireAccountDeactivated(string toEmail, Guid userId, string verificationLink, string? userName = null);

    /// <summary>
    /// Fire a password changed confirmation email.
    /// </summary>
    void FirePasswordChanged(string toEmail, Guid userId, string? userName = null);

    /// <summary>
    /// Fire a two-factor authentication enabled confirmation email.
    /// </summary>
    void FireTwoFactorEnabled(string toEmail, Guid userId, string? userName = null);

    /// <summary>
    /// Fire a security alert email.
    /// </summary>
    void FireSecurityAlert(string toEmail, Guid userId, string alertType, object details);

    /// <summary>
    /// Fire a referral credit awarded email.
    /// </summary>
    void FireReferralCreditAwarded(string toEmail, Guid userId, string? userName, string otherPartyName, int creditAmount);
}

/// <summary>
/// Constants for email trigger names.
/// </summary>
public static class EmailTriggers
{
    public const string PasswordReset = "PasswordReset";
    public const string EmailVerification = "EmailVerification";
    public const string EmailVerificationReminder = "EmailVerificationReminder";
    public const string AccountDeactivated = "AccountDeactivated";
    public const string SecurityAlert = "SecurityAlert";
    public const string PasswordChanged = "PasswordChanged";
    public const string TwoFactorEnabled = "TwoFactorEnabled";
    public const string ChargingComplete = "ChargingComplete";
    public const string LowBattery = "LowBattery";
    public const string ChargingInterrupted = "ChargingInterrupted";
    public const string SoftwareUpdate = "SoftwareUpdate";
    public const string AdminBroadcast = "AdminBroadcast";
    public const string ReferralCreditAwarded = "ReferralCreditAwarded";
}
