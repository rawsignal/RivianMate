using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using RivianMate.Api.Configuration;

namespace RivianMate.Api.Services.Email;

/// <summary>
/// Email sender implementation using the Resend API.
/// https://resend.com/docs/api-reference/emails/send-email
/// </summary>
public class ResendEmailSender : IEmailSender
{
    private readonly HttpClient _httpClient;
    private readonly EmailConfiguration _config;
    private readonly ILogger<ResendEmailSender> _logger;

    private const string ResendApiUrl = "https://api.resend.com/emails";

    public ResendEmailSender(
        HttpClient httpClient,
        IOptions<EmailConfiguration> config,
        ILogger<ResendEmailSender> logger)
    {
        _httpClient = httpClient;
        _config = config.Value;
        _logger = logger;

        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _config.Resend.ApiKey);
    }

    public async Task<EmailSendResult> SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_config.Resend.ApiKey))
        {
            _logger.LogError("Resend API key is not configured");
            return EmailSendResult.Failed("Resend API key is not configured");
        }

        var request = new ResendEmailRequest
        {
            From = FormatFromAddress(),
            To = [to],
            Subject = subject,
            Html = htmlBody
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(ResendApiUrl, request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ResendEmailResponse>(cancellationToken);
                _logger.LogInformation("Email sent via Resend to {To}, MessageId: {MessageId}", to, result?.Id);
                return EmailSendResult.Succeeded(result?.Id);
            }

            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Resend API error: {StatusCode} - {Body}", response.StatusCode, errorBody);
            return EmailSendResult.Failed($"Resend API error: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email via Resend to {To}", to);
            return EmailSendResult.Failed(ex.Message);
        }
    }

    private string FormatFromAddress()
    {
        if (string.IsNullOrEmpty(_config.FromName))
            return _config.FromAddress;

        return $"{_config.FromName} <{_config.FromAddress}>";
    }

    private class ResendEmailRequest
    {
        [JsonPropertyName("from")]
        public string From { get; set; } = "";

        [JsonPropertyName("to")]
        public string[] To { get; set; } = [];

        [JsonPropertyName("subject")]
        public string Subject { get; set; } = "";

        [JsonPropertyName("html")]
        public string Html { get; set; } = "";
    }

    private class ResendEmailResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }
}
