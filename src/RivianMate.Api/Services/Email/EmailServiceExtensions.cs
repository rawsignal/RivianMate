using Microsoft.Extensions.Options;
using RivianMate.Api.Configuration;

namespace RivianMate.Api.Services.Email;

/// <summary>
/// Extension methods for registering email services.
/// </summary>
public static class EmailServiceExtensions
{
    /// <summary>
    /// Adds email services to the dependency injection container.
    /// </summary>
    public static IServiceCollection AddEmailServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind configuration
        services.Configure<EmailConfiguration>(configuration.GetSection($"RivianMate:{EmailConfiguration.SectionName}"));

        // Register template renderer (singleton - caches base template)
        services.AddSingleton<EmailTemplateRenderer>();

        // Register trigger service
        services.AddScoped<IEmailTrigger, EmailTriggerService>();

        // Register the appropriate email sender based on configuration
        var emailConfig = configuration
            .GetSection($"RivianMate:{EmailConfiguration.SectionName}")
            .Get<EmailConfiguration>() ?? new EmailConfiguration();

        if (emailConfig.Provider.Equals("Resend", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<IEmailSender, ResendEmailSender>();
        }
        else
        {
            services.AddScoped<IEmailSender, SmtpEmailSender>();
        }

        return services;
    }
}
