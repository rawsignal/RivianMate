using System.Net;

namespace RivianMate.Core.Exceptions;

/// <summary>
/// Thrown when input validation fails.
/// Returns HTTP 400 Bad Request with validation details.
/// </summary>
public class ValidationException : RivianMateException
{
    public override HttpStatusCode StatusCode => HttpStatusCode.BadRequest;
    public override string ErrorCode => "VALIDATION_ERROR";

    /// <summary>
    /// Dictionary of field names to their validation errors.
    /// </summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    private const string DefaultMessage = "One or more validation errors occurred";

    public ValidationException(string field, string error)
        : base(DefaultMessage, $"Validation failed: {field} - {error}")
    {
        Errors = new Dictionary<string, string[]>
        {
            { field, new[] { error } }
        };
    }

    public ValidationException(IDictionary<string, string[]> errors)
        : base(DefaultMessage, $"Validation failed for {errors.Count} field(s)")
    {
        Errors = new Dictionary<string, string[]>(errors);
    }

    public ValidationException(string message, IDictionary<string, string[]> errors)
        : base(message, $"Validation failed: {message}")
    {
        Errors = new Dictionary<string, string[]>(errors);
    }
}
