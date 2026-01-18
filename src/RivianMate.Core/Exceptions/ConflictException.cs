using System.Net;

namespace RivianMate.Core.Exceptions;

/// <summary>
/// Thrown when a request conflicts with the current state of the resource.
/// Returns HTTP 409 Conflict.
/// </summary>
public class ConflictException : RivianMateException
{
    public override HttpStatusCode StatusCode => HttpStatusCode.Conflict;
    public override string ErrorCode => "CONFLICT";

    public ConflictException(string message)
        : base(message)
    {
    }

    public ConflictException(string externalMessage, string internalMessage)
        : base(externalMessage, internalMessage)
    {
    }

    /// <summary>
    /// Creates a conflict exception for a duplicate resource.
    /// </summary>
    public static ConflictException Duplicate(string resourceType, string? identifier = null)
    {
        var message = identifier != null
            ? $"A {resourceType} with this identifier already exists"
            : $"This {resourceType} already exists";

        var internalMessage = identifier != null
            ? $"Duplicate {resourceType}: {identifier}"
            : $"Duplicate {resourceType}";

        return new ConflictException(message, internalMessage);
    }

    /// <summary>
    /// Creates a conflict exception for an invalid state transition.
    /// </summary>
    public static ConflictException InvalidState(string resourceType, string currentState, string attemptedAction)
    {
        return new ConflictException(
            $"Cannot {attemptedAction} - {resourceType} is currently {currentState}",
            $"Invalid state transition: {resourceType} in state '{currentState}' cannot perform '{attemptedAction}'");
    }
}
