namespace RivianMate.Core.Interfaces;

/// <summary>
/// Interface for entities that are owned through a Drive (which is owned through a Vehicle).
/// Ownership is validated by checking Drive.Vehicle.OwnerId in SaveChangesAsync.
/// </summary>
public interface IDriveOwnedEntity
{
    /// <summary>
    /// The ID of the drive this entity belongs to.
    /// </summary>
    int DriveId { get; set; }
}
