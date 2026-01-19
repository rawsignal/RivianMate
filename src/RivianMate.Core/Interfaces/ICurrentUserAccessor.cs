namespace RivianMate.Core.Interfaces;

/// <summary>
/// Provides access to the current user's ID for ownership validation.
/// Returns null for system/background operations where no user context exists.
/// </summary>
public interface ICurrentUserAccessor
{
    /// <summary>
    /// Gets the current user's ID, or null if no user context exists
    /// (e.g., during background job execution).
    /// </summary>
    Guid? UserId { get; }
}
