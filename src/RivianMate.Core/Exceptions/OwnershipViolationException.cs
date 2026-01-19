namespace RivianMate.Core.Exceptions;

/// <summary>
/// Thrown when an attempt is made to create or modify an entity
/// that doesn't belong to the current user.
/// </summary>
public class OwnershipViolationException : Exception
{
    public string EntityType { get; }
    public Guid? CurrentUserId { get; }
    public Guid? EntityOwnerId { get; }

    public OwnershipViolationException(string entityType, Guid? currentUserId, Guid? entityOwnerId)
        : base($"Ownership violation: User {currentUserId} attempted to modify {entityType} owned by {entityOwnerId}")
    {
        EntityType = entityType;
        CurrentUserId = currentUserId;
        EntityOwnerId = entityOwnerId;
    }

    public OwnershipViolationException(string message) : base(message)
    {
        EntityType = "Unknown";
    }
}
