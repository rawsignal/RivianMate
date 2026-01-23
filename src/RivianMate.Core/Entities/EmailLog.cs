namespace RivianMate.Core.Entities;

/// <summary>
/// Audit log for sent emails.
/// </summary>
public class EmailLog
{
    public int Id { get; set; }

    /// <summary>
    /// The user this email was sent to (if applicable).
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Recipient email address.
    /// </summary>
    public string ToAddress { get; set; } = "";

    /// <summary>
    /// The trigger that initiated this email (e.g., "PasswordReset").
    /// </summary>
    public string Trigger { get; set; } = "";

    /// <summary>
    /// The rendered subject line.
    /// </summary>
    public string Subject { get; set; } = "";

    /// <summary>
    /// Whether the email was sent successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if the send failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Provider-specific message ID (e.g., Resend message ID).
    /// </summary>
    public string? ProviderMessageId { get; set; }

    /// <summary>
    /// When this email was sent (or attempted).
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
