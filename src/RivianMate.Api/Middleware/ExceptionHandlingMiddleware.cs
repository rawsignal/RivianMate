using System.Net;
using System.Text.Json;
using RivianMate.Core.Exceptions;

namespace RivianMate.Api.Middleware;

/// <summary>
/// Middleware that catches exceptions and converts them to appropriate HTTP responses.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, response) = exception switch
        {
            RivianMateException rmEx => HandleRivianMateException(rmEx),
            OperationCanceledException => HandleCancellation(),
            _ => HandleUnknownException(exception)
        };

        // Log the exception
        LogException(exception, statusCode);

        // Don't modify the response if it's already started
        if (context.Response.HasStarted)
        {
            _logger.LogWarning("Response has already started, cannot write error response");
            return;
        }

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }

    private (HttpStatusCode, ErrorResponse) HandleRivianMateException(RivianMateException ex)
    {
        var response = new ErrorResponse
        {
            Error = ex.ExternalMessage,
            Code = ex.ErrorCode
        };

        // Add validation errors if present
        if (ex is ValidationException validationEx)
        {
            response.ValidationErrors = validationEx.Errors;
        }

        // Add retry-after header for rate limiting
        if (ex is RateLimitedException rateLimitEx && rateLimitEx.RetryAfter.HasValue)
        {
            response.RetryAfterSeconds = (int)rateLimitEx.RetryAfter.Value.TotalSeconds;
        }

        // Include stack trace in development
        if (_environment.IsDevelopment())
        {
            response.Detail = ex.InternalMessage;
            response.StackTrace = ex.StackTrace;
        }

        return (ex.StatusCode, response);
    }

    private (HttpStatusCode, ErrorResponse) HandleCancellation()
    {
        return (HttpStatusCode.BadRequest, new ErrorResponse
        {
            Error = "Request was cancelled",
            Code = "REQUEST_CANCELLED"
        });
    }

    private (HttpStatusCode, ErrorResponse) HandleUnknownException(Exception ex)
    {
        var response = new ErrorResponse
        {
            Error = "An unexpected error occurred",
            Code = "INTERNAL_ERROR"
        };

        // Include details in development
        if (_environment.IsDevelopment())
        {
            response.Detail = ex.Message;
            response.StackTrace = ex.StackTrace;
        }

        return (HttpStatusCode.InternalServerError, response);
    }

    private void LogException(Exception exception, HttpStatusCode statusCode)
    {
        var logLevel = statusCode switch
        {
            HttpStatusCode.InternalServerError => LogLevel.Error,
            HttpStatusCode.BadGateway => LogLevel.Error,
            HttpStatusCode.ServiceUnavailable => LogLevel.Error,
            HttpStatusCode.Unauthorized => LogLevel.Warning,
            HttpStatusCode.Forbidden => LogLevel.Warning,
            HttpStatusCode.TooManyRequests => LogLevel.Warning,
            _ => LogLevel.Information
        };

        if (exception is RivianMateException rmEx)
        {
            _logger.Log(logLevel, exception, "Request failed with {StatusCode}: {InternalMessage}",
                statusCode, rmEx.InternalMessage);
        }
        else
        {
            _logger.Log(logLevel, exception, "Request failed with {StatusCode}: {Message}",
                statusCode, exception.Message);
        }
    }
}

/// <summary>
/// Standard error response format for API errors.
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// Human-readable error message safe for display to users.
    /// </summary>
    public required string Error { get; set; }

    /// <summary>
    /// Machine-readable error code for client-side handling.
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// Additional detail (only in development).
    /// </summary>
    public string? Detail { get; set; }

    /// <summary>
    /// Stack trace (only in development).
    /// </summary>
    public string? StackTrace { get; set; }

    /// <summary>
    /// Validation errors by field name.
    /// </summary>
    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; set; }

    /// <summary>
    /// Seconds to wait before retrying (for rate limiting).
    /// </summary>
    public int? RetryAfterSeconds { get; set; }
}

/// <summary>
/// Extension methods for registering the exception handling middleware.
/// </summary>
public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}
