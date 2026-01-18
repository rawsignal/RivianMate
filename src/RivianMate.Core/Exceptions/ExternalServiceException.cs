using System.Net;

namespace RivianMate.Core.Exceptions;

/// <summary>
/// Thrown when an external service (like Rivian API) returns an error or is unavailable.
/// Returns HTTP 502 Bad Gateway by default.
/// </summary>
public class ExternalServiceException : RivianMateException
{
    public override HttpStatusCode StatusCode => _statusCode;
    public override string ErrorCode => "EXTERNAL_SERVICE_ERROR";

    private readonly HttpStatusCode _statusCode;

    /// <summary>
    /// Name of the external service that failed.
    /// </summary>
    public string ServiceName { get; }

    /// <summary>
    /// The original error from the external service, if available.
    /// </summary>
    public string? ServiceError { get; }

    /// <summary>
    /// HTTP status code returned by the external service, if applicable.
    /// </summary>
    public int? ServiceStatusCode { get; }

    public ExternalServiceException(string serviceName, string message)
        : base($"Error communicating with {serviceName}", $"{serviceName}: {message}")
    {
        ServiceName = serviceName;
        ServiceError = message;
        _statusCode = HttpStatusCode.BadGateway;
    }

    public ExternalServiceException(string serviceName, string message, int serviceStatusCode)
        : base($"Error communicating with {serviceName}", $"{serviceName} returned {serviceStatusCode}: {message}")
    {
        ServiceName = serviceName;
        ServiceError = message;
        ServiceStatusCode = serviceStatusCode;
        _statusCode = HttpStatusCode.BadGateway;
    }

    public ExternalServiceException(string serviceName, string externalMessage, string internalMessage)
        : base(externalMessage, internalMessage)
    {
        ServiceName = serviceName;
        _statusCode = HttpStatusCode.BadGateway;
    }

    public ExternalServiceException(string serviceName, string message, Exception innerException)
        : base($"Error communicating with {serviceName}", $"{serviceName}: {message}", innerException)
    {
        ServiceName = serviceName;
        ServiceError = message;
        _statusCode = HttpStatusCode.BadGateway;
    }

    /// <summary>
    /// Creates an exception indicating the service is unavailable.
    /// </summary>
    public static ExternalServiceException Unavailable(string serviceName)
    {
        return new ExternalServiceException(
            serviceName,
            $"{serviceName} is currently unavailable. Please try again later.",
            $"{serviceName} is unavailable");
    }

    /// <summary>
    /// Creates an exception for authentication failure with external service.
    /// </summary>
    public static ExternalServiceException AuthenticationFailed(string serviceName, string? details = null)
    {
        var internalMsg = details != null
            ? $"{serviceName} authentication failed: {details}"
            : $"{serviceName} authentication failed";

        return new ExternalServiceException(
            serviceName,
            $"Failed to authenticate with {serviceName}. Please re-link your account.",
            internalMsg);
    }
}
