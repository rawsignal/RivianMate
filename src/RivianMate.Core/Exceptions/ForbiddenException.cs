using System.Net;

namespace RivianMate.Core.Exceptions;

/// <summary>
/// Thrown when the user is authenticated but not authorized to perform the action.
/// Returns HTTP 403 Forbidden.
/// </summary>
public class ForbiddenException : RivianMateException
{
    public override HttpStatusCode StatusCode => HttpStatusCode.Forbidden;
    public override string ErrorCode => "FORBIDDEN";

    private const string DefaultMessage = "You do not have permission to perform this action";

    public ForbiddenException()
        : base(DefaultMessage)
    {
    }

    public ForbiddenException(string message)
        : base(message)
    {
    }

    public ForbiddenException(string externalMessage, string internalMessage)
        : base(externalMessage, internalMessage)
    {
    }

    /// <summary>
    /// Creates a forbidden exception for accessing a specific resource.
    /// </summary>
    public static ForbiddenException ForResource(string resourceType, object? resourceId = null)
    {
        var message = resourceId != null
            ? $"You do not have permission to access this {resourceType}"
            : $"You do not have permission to access {resourceType} resources";

        var internalMessage = resourceId != null
            ? $"Access denied to {resourceType} with ID '{resourceId}'"
            : $"Access denied to {resourceType} resources";

        return new ForbiddenException(message, internalMessage);
    }
}
