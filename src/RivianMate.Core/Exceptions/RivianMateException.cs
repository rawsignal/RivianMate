using System.Net;

namespace RivianMate.Core.Exceptions;

/// <summary>
/// Base exception for all RivianMate application exceptions.
/// Provides structured error handling with separate internal (logging) and external (API response) messages.
/// </summary>
public abstract class RivianMateException : Exception
{
    /// <summary>
    /// HTTP status code to return when this exception is thrown.
    /// </summary>
    public abstract HttpStatusCode StatusCode { get; }

    /// <summary>
    /// Message safe to expose to external clients (API responses).
    /// Should not contain sensitive information.
    /// </summary>
    public string ExternalMessage { get; }

    /// <summary>
    /// Detailed message for internal logging. May contain sensitive details.
    /// Falls back to ExternalMessage if not specified.
    /// </summary>
    public string InternalMessage => Message;

    /// <summary>
    /// Optional error code for client-side error handling.
    /// </summary>
    public virtual string? ErrorCode => null;

    protected RivianMateException(string externalMessage)
        : base(externalMessage)
    {
        ExternalMessage = externalMessage;
    }

    protected RivianMateException(string externalMessage, string internalMessage)
        : base(internalMessage)
    {
        ExternalMessage = externalMessage;
    }

    protected RivianMateException(string externalMessage, Exception innerException)
        : base(externalMessage, innerException)
    {
        ExternalMessage = externalMessage;
    }

    protected RivianMateException(string externalMessage, string internalMessage, Exception innerException)
        : base(internalMessage, innerException)
    {
        ExternalMessage = externalMessage;
    }
}
