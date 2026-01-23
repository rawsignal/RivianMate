using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using RivianMate.Api.Configuration;

namespace RivianMate.Api.Services.Email;

/// <summary>
/// Renders email templates by loading template files and replacing {{tokens}} with values.
/// </summary>
public partial class EmailTemplateRenderer
{
    private readonly EmailConfiguration _config;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<EmailTemplateRenderer> _logger;

    private const string TemplatesFolder = "Email/Templates";
    private const string BaseTemplateName = "BaseTemplate";

    // Cached base template
    private string? _baseTemplateCache;

    public EmailTemplateRenderer(
        IOptions<EmailConfiguration> config,
        IWebHostEnvironment env,
        ILogger<EmailTemplateRenderer> logger)
    {
        _config = config.Value;
        _env = env;
        _logger = logger;
    }

    /// <summary>
    /// Renders an email template with the given tokens.
    /// </summary>
    /// <param name="templateName">Template file name (without .html extension)</param>
    /// <param name="tokens">Dictionary of token names to values (without {{ }})</param>
    /// <returns>Fully rendered HTML email</returns>
    public async Task<string> RenderAsync(string templateName, Dictionary<string, string> tokens)
    {
        // Load the content template
        var contentHtml = await LoadTemplateAsync(templateName);

        // Load the base template
        var baseHtml = await GetBaseTemplateAsync();

        // Inject content into base template
        var html = baseHtml.Replace("{{Content}}", contentHtml);

        // Add standard tokens
        var allTokens = new Dictionary<string, string>(tokens)
        {
            ["BaseUrl"] = _config.BaseUrl.TrimEnd('/'),
            ["CurrentYear"] = DateTime.UtcNow.Year.ToString()
        };

        // Replace all tokens
        html = ReplaceTokens(html, allTokens);

        return html;
    }

    /// <summary>
    /// Renders just the subject line with token replacement.
    /// </summary>
    public string RenderSubject(string subjectTemplate, Dictionary<string, string> tokens)
    {
        return ReplaceTokens(subjectTemplate, tokens);
    }

    private async Task<string> LoadTemplateAsync(string templateName)
    {
        var templatePath = Path.Combine(_env.ContentRootPath, TemplatesFolder, $"{templateName}.html");

        if (!File.Exists(templatePath))
        {
            _logger.LogError("Email template not found: {TemplatePath}", templatePath);
            throw new FileNotFoundException($"Email template not found: {templateName}", templatePath);
        }

        return await File.ReadAllTextAsync(templatePath);
    }

    private async Task<string> GetBaseTemplateAsync()
    {
        if (_baseTemplateCache != null)
            return _baseTemplateCache;

        _baseTemplateCache = await LoadTemplateAsync(BaseTemplateName);
        return _baseTemplateCache;
    }

    private string ReplaceTokens(string template, Dictionary<string, string> tokens)
    {
        // Replace {{TokenName}} with values (case-insensitive)
        return TokenRegex().Replace(template, match =>
        {
            var tokenName = match.Groups[1].Value;

            if (tokens.TryGetValue(tokenName, out var value))
                return value;

            // Check case-insensitive
            var key = tokens.Keys.FirstOrDefault(k => k.Equals(tokenName, StringComparison.OrdinalIgnoreCase));
            if (key != null)
                return tokens[key];

            // Token not found - leave as-is for debugging, but log warning
            _logger.LogWarning("Email template token not found: {TokenName}", tokenName);
            return match.Value;
        });
    }

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex TokenRegex();
}
