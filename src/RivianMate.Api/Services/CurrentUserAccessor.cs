using System.Security.Claims;
using RivianMate.Core.Interfaces;

namespace RivianMate.Api.Services;

/// <summary>
/// Provides access to the current user's ID from HttpContext.
/// Returns null when called from background jobs or when no user is authenticated.
/// </summary>
public class CurrentUserAccessor : ICurrentUserAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Gets the current user's ID from the HttpContext claims.
    /// Returns null if:
    /// - No HttpContext exists (background job)
    /// - User is not authenticated
    /// - User ID claim is missing or invalid
    /// </summary>
    public Guid? UserId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
                return null;

            var user = httpContext.User;
            if (user?.Identity?.IsAuthenticated != true)
                return null;

            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                return null;

            return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
        }
    }
}
