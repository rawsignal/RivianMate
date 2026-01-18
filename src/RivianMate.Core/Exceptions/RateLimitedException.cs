using System.Net;

namespace RivianMate.Core.Exceptions;

/// <summary>
/// Thrown when rate limiting is encountered (either our own or from external services).
/// Returns HTTP 429 Too Many Requests.
/// </summary>
public class RateLimitedException : RivianMateException
{
    public override HttpStatusCode StatusCode => HttpStatusCode.TooManyRequests;
    public override string ErrorCode => "RATE_LIMITED";

    /// <summary>
    /// Suggested time to wait before retrying, if known.
    /// </summary>
    public TimeSpan? RetryAfter { get; }

    private const string DefaultMessage = "Too many requests. Please try again later.";

    public RateLimitedException()
        : base(DefaultMessage)
    {
    }

    public RateLimitedException(string message)
        : base(message)
    {
    }

    public RateLimitedException(TimeSpan retryAfter)
        : base($"Too many requests. Please try again in {FormatRetryAfter(retryAfter)}.")
    {
        RetryAfter = retryAfter;
    }

    public RateLimitedException(string message, TimeSpan retryAfter)
        : base(message)
    {
        RetryAfter = retryAfter;
    }

    private static string FormatRetryAfter(TimeSpan retryAfter)
    {
        if (retryAfter.TotalSeconds < 60)
            return $"{(int)retryAfter.TotalSeconds} seconds";
        if (retryAfter.TotalMinutes < 60)
            return $"{(int)retryAfter.TotalMinutes} minutes";
        return $"{(int)retryAfter.TotalHours} hours";
    }
}
