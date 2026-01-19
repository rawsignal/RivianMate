namespace RivianMate.Core.Interfaces;

/// <summary>
/// Interface for entities that have an OwnerId property (nullable).
/// Used for Vehicle which uses OwnerId instead of UserId.
/// </summary>
public interface IOwnerOwnedEntity
{
    /// <summary>
    /// The ID of the user who owns this entity (nullable for legacy data).
    /// </summary>
    Guid? OwnerId { get; set; }
}
