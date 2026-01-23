namespace RivianMate.Api.Services.Email;

/// <summary>
/// Low-level email sending interface. Implementations handle the actual
/// delivery via different providers (Resend, SMTP, etc.)
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Sends an email.
    /// </summary>
    /// <param name="to">Recipient email address</param>
    /// <param name="subject">Email subject line</param>
    /// <param name="htmlBody">HTML content of the email</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing success status and optional message ID or error</returns>
    Task<EmailSendResult> SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of an email send operation.
/// </summary>
public class EmailSendResult
{
    /// <summary>
    /// Whether the email was sent successfully.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Provider-specific message ID for tracking (e.g., Resend message ID).
    /// </summary>
    public string? MessageId { get; init; }

    /// <summary>
    /// Error message if the send failed.
    /// </summary>
    public string? Error { get; init; }

    public static EmailSendResult Succeeded(string? messageId = null) => new()
    {
        Success = true,
        MessageId = messageId
    };

    public static EmailSendResult Failed(string error) => new()
    {
        Success = false,
        Error = error
    };
}
