namespace RivianMate.Api.Configuration;

/// <summary>
/// Configuration for the email system.
/// Bind to appsettings section: RivianMate:Email
/// </summary>
public class EmailConfiguration
{
    public const string SectionName = "Email";

    /// <summary>
    /// Master toggle for the email system. When false, all email triggers are no-ops.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Email provider to use: "Resend" or "SMTP"
    /// </summary>
    public string Provider { get; set; } = "SMTP";

    /// <summary>
    /// The email address emails are sent from.
    /// </summary>
    public string FromAddress { get; set; } = "noreply@example.com";

    /// <summary>
    /// The display name for the sender.
    /// </summary>
    public string FromName { get; set; } = "RivianMate";

    /// <summary>
    /// Base URL for links in emails (e.g., https://rivianmate.com)
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:5000";

    /// <summary>
    /// Resend API configuration.
    /// </summary>
    public ResendConfiguration Resend { get; set; } = new();

    /// <summary>
    /// SMTP configuration for self-hosted deployments.
    /// </summary>
    public SmtpConfiguration Smtp { get; set; } = new();

    /// <summary>
    /// Email trigger configurations.
    /// </summary>
    public Dictionary<string, EmailTriggerConfiguration> Triggers { get; set; } = new();

    /// <summary>
    /// Email verification settings.
    /// </summary>
    public EmailVerificationConfiguration Verification { get; set; } = new();
}

/// <summary>
/// Configuration for email verification requirements.
/// </summary>
public class EmailVerificationConfiguration
{
    /// <summary>
    /// Whether email verification is required.
    /// Defaults to false for self-hosted edition. Set to true for Pro/cloud deployments.
    /// </summary>
    public bool Required { get; set; } = false;

    /// <summary>
    /// Number of days after registration before account is deactivated if email not verified.
    /// </summary>
    public int GracePeriodDays { get; set; } = 7;

    /// <summary>
    /// Hours before deadline to send reminder email.
    /// </summary>
    public int ReminderHoursBeforeDeadline { get; set; } = 24;

    /// <summary>
    /// Minimum minutes between resend requests.
    /// </summary>
    public int ResendCooldownMinutes { get; set; } = 5;
}

/// <summary>
/// Resend API configuration.
/// </summary>
public class ResendConfiguration
{
    /// <summary>
    /// Resend API key (starts with re_)
    /// </summary>
    public string ApiKey { get; set; } = "";
}

/// <summary>
/// SMTP server configuration.
/// </summary>
public class SmtpConfiguration
{
    /// <summary>
    /// SMTP server hostname (e.g., smtp.gmail.com)
    /// </summary>
    public string Host { get; set; } = "";

    /// <summary>
    /// SMTP server port (typically 587 for TLS, 465 for SSL, 25 for unencrypted)
    /// </summary>
    public int Port { get; set; } = 587;

    /// <summary>
    /// SMTP authentication username.
    /// </summary>
    public string Username { get; set; } = "";

    /// <summary>
    /// SMTP authentication password or app password.
    /// </summary>
    public string Password { get; set; } = "";

    /// <summary>
    /// Whether to use SSL/TLS for the connection.
    /// </summary>
    public bool UseSsl { get; set; } = true;
}

/// <summary>
/// Configuration for an individual email trigger.
/// </summary>
public class EmailTriggerConfiguration
{
    /// <summary>
    /// Whether this trigger is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Critical emails are always sent regardless of user preferences.
    /// Non-critical emails require user opt-in.
    /// </summary>
    public bool Critical { get; set; } = false;

    /// <summary>
    /// The template file name (without extension) to use for this trigger.
    /// </summary>
    public string Template { get; set; } = "";

    /// <summary>
    /// The email subject line. Supports {{token}} replacement.
    /// </summary>
    public string Subject { get; set; } = "";
}
