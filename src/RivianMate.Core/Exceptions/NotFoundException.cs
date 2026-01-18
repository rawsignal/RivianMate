using System.Net;

namespace RivianMate.Core.Exceptions;

/// <summary>
/// Thrown when a requested resource cannot be found.
/// Returns HTTP 404 Not Found.
/// </summary>
public class NotFoundException : RivianMateException
{
    public override HttpStatusCode StatusCode => HttpStatusCode.NotFound;
    public override string ErrorCode => "NOT_FOUND";

    public string? ResourceType { get; }
    public string? ResourceId { get; }

    /// <summary>
    /// Creates a not found exception for a specific resource type.
    /// </summary>
    /// <param name="resourceType">The type of resource (e.g., "Vehicle", "User")</param>
    public NotFoundException(string resourceType)
        : base($"{resourceType} not found")
    {
        ResourceType = resourceType;
    }

    /// <summary>
    /// Creates a not found exception for a specific resource with an identifier.
    /// </summary>
    /// <param name="resourceType">The type of resource (e.g., "Vehicle", "User")</param>
    /// <param name="resourceId">The identifier that was searched for</param>
    public NotFoundException(string resourceType, object resourceId)
        : base($"{resourceType} not found", $"{resourceType} with ID '{resourceId}' not found")
    {
        ResourceType = resourceType;
        ResourceId = resourceId?.ToString();
    }

    /// <summary>
    /// Creates a not found exception with a custom message.
    /// </summary>
    /// <param name="resourceType">The type of resource</param>
    /// <param name="resourceId">The identifier that was searched for</param>
    /// <param name="customMessage">Custom external message</param>
    public NotFoundException(string resourceType, object resourceId, string customMessage)
        : base(customMessage, $"{resourceType} with ID '{resourceId}' not found")
    {
        ResourceType = resourceType;
        ResourceId = resourceId?.ToString();
    }
}
