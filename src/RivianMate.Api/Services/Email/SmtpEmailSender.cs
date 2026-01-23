using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using RivianMate.Api.Configuration;

namespace RivianMate.Api.Services.Email;

/// <summary>
/// Email sender implementation using SMTP.
/// For self-hosted deployments using Gmail, Outlook, or custom mail servers.
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly EmailConfiguration _config;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(
        IOptions<EmailConfiguration> config,
        ILogger<SmtpEmailSender> logger)
    {
        _config = config.Value;
        _logger = logger;
    }

    public async Task<EmailSendResult> SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var smtpConfig = _config.Smtp;

        if (string.IsNullOrEmpty(smtpConfig.Host))
        {
            _logger.LogError("SMTP host is not configured");
            return EmailSendResult.Failed("SMTP host is not configured");
        }

        try
        {
            using var message = new MailMessage();
            message.From = new MailAddress(_config.FromAddress, _config.FromName);
            message.To.Add(new MailAddress(to));
            message.Subject = subject;
            message.Body = htmlBody;
            message.IsBodyHtml = true;

            using var client = new SmtpClient(smtpConfig.Host, smtpConfig.Port);
            client.EnableSsl = smtpConfig.UseSsl;

            if (!string.IsNullOrEmpty(smtpConfig.Username))
            {
                client.Credentials = new NetworkCredential(smtpConfig.Username, smtpConfig.Password);
            }

            await client.SendMailAsync(message, cancellationToken);

            _logger.LogInformation("Email sent via SMTP to {To}", to);
            return EmailSendResult.Succeeded();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email via SMTP to {To}", to);
            return EmailSendResult.Failed(ex.Message);
        }
    }
}
