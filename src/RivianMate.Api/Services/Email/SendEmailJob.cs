using Hangfire;
using RivianMate.Core.Entities;
using RivianMate.Infrastructure.Data;

namespace RivianMate.Api.Services.Email;

/// <summary>
/// Hangfire job for sending emails in the background.
/// </summary>
public class SendEmailJob
{
    private readonly IEmailSender _emailSender;
    private readonly EmailTemplateRenderer _templateRenderer;
    private readonly RivianMateDbContext _db;
    private readonly ILogger<SendEmailJob> _logger;

    public SendEmailJob(
        IEmailSender emailSender,
        EmailTemplateRenderer templateRenderer,
        RivianMateDbContext db,
        ILogger<SendEmailJob> logger)
    {
        _emailSender = emailSender;
        _templateRenderer = templateRenderer;
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Executes the email send job.
    /// </summary>
    /// <param name="request">Email request containing all necessary data</param>
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = [30, 120, 300])]
    [Queue("default")]
    public async Task ExecuteAsync(EmailJobRequest request)
    {
        _logger.LogInformation("Sending email: Trigger={Trigger}, To={To}", request.Trigger, request.To);

        try
        {
            // Render the template
            var htmlBody = await _templateRenderer.RenderAsync(request.Template, request.Tokens);
            var subject = _templateRenderer.RenderSubject(request.SubjectTemplate, request.Tokens);

            // Send the email
            var result = await _emailSender.SendAsync(request.To, subject, htmlBody);

            // Log the result
            await LogEmailAsync(request, subject, result);

            if (!result.Success)
            {
                throw new Exception($"Email send failed: {result.Error}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email: Trigger={Trigger}, To={To}", request.Trigger, request.To);
            throw; // Re-throw to trigger Hangfire retry
        }
    }

    private async Task LogEmailAsync(EmailJobRequest request, string subject, EmailSendResult result)
    {
        var log = new EmailLog
        {
            UserId = request.UserId,
            ToAddress = request.To,
            Trigger = request.Trigger,
            Subject = subject,
            Success = result.Success,
            ErrorMessage = result.Error,
            ProviderMessageId = result.MessageId,
            CreatedAt = DateTime.UtcNow
        };

        _db.EmailLogs.Add(log);
        await _db.SaveChangesAsync();
    }
}

/// <summary>
/// Request data for the SendEmailJob.
/// </summary>
public class EmailJobRequest
{
    /// <summary>
    /// Recipient email address.
    /// </summary>
    public string To { get; init; } = "";

    /// <summary>
    /// The trigger that initiated this email.
    /// </summary>
    public string Trigger { get; init; } = "";

    /// <summary>
    /// Template file name (without extension).
    /// </summary>
    public string Template { get; init; } = "";

    /// <summary>
    /// Subject line template (supports {{tokens}}).
    /// </summary>
    public string SubjectTemplate { get; init; } = "";

    /// <summary>
    /// Token values for template replacement.
    /// </summary>
    public Dictionary<string, string> Tokens { get; init; } = new();

    /// <summary>
    /// Optional user ID for logging purposes.
    /// </summary>
    public Guid? UserId { get; init; }
}
