using System.Security.Claims;
using Hangfire.Dashboard;

namespace RivianMate.Api.Services.Jobs;

/// <summary>
/// Authorization filter for Hangfire dashboard.
/// In development, allows all access. In production, requires user to be in AdminEmails list.
/// </summary>
public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    private readonly bool _isDevelopment;
    private readonly HashSet<string> _adminEmails;

    public HangfireAuthorizationFilter(bool isDevelopment, IEnumerable<string>? adminEmails = null)
    {
        _isDevelopment = isDevelopment;
        _adminEmails = new HashSet<string>(
            adminEmails ?? Enumerable.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);
    }

    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        // Must be authenticated
        if (!httpContext.User.Identity?.IsAuthenticated ?? true)
            return false;

        // In development mode, allow any authenticated user
        if (_isDevelopment)
            return true;

        // In production, require email to be in admin list
        // If no admin emails configured, deny all access (secure by default)
        if (_adminEmails.Count == 0)
            return false;

        var userEmail = httpContext.User.FindFirst(ClaimTypes.Email)?.Value
            ?? httpContext.User.FindFirst("email")?.Value;

        return userEmail != null && _adminEmails.Contains(userEmail);
    }
}
