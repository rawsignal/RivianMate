using System.Net;

namespace RivianMate.Core.Exceptions;

/// <summary>
/// Thrown when authentication is required but not provided or invalid.
/// Returns HTTP 401 Unauthorized.
/// </summary>
public class UnauthorizedException : RivianMateException
{
    public override HttpStatusCode StatusCode => HttpStatusCode.Unauthorized;
    public override string ErrorCode => "UNAUTHORIZED";

    private const string DefaultMessage = "Authentication required";

    public UnauthorizedException()
        : base(DefaultMessage)
    {
    }

    public UnauthorizedException(string message)
        : base(message)
    {
    }

    public UnauthorizedException(string externalMessage, string internalMessage)
        : base(externalMessage, internalMessage)
    {
    }
}
