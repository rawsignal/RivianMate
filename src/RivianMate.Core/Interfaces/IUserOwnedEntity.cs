namespace RivianMate.Core.Interfaces;

/// <summary>
/// Interface for entities that are directly owned by a user.
/// Used for ownership validation in SaveChangesAsync.
/// </summary>
public interface IUserOwnedEntity
{
    /// <summary>
    /// The ID of the user who owns this entity.
    /// </summary>
    Guid UserId { get; set; }
}
