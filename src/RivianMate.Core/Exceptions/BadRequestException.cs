using System.Net;

namespace RivianMate.Core.Exceptions;

/// <summary>
/// Thrown when the request is malformed or contains invalid data.
/// Returns HTTP 400 Bad Request.
/// </summary>
public class BadRequestException : RivianMateException
{
    public override HttpStatusCode StatusCode => HttpStatusCode.BadRequest;
    public override string ErrorCode => "BAD_REQUEST";

    private const string DefaultMessage = "Invalid request";

    public BadRequestException()
        : base(DefaultMessage)
    {
    }

    public BadRequestException(string message)
        : base(message)
    {
    }

    public BadRequestException(string externalMessage, string internalMessage)
        : base(externalMessage, internalMessage)
    {
    }
}
