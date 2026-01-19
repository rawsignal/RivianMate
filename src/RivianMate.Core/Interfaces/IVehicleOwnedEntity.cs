namespace RivianMate.Core.Interfaces;

/// <summary>
/// Interface for entities that are owned through a Vehicle.
/// Ownership is validated by checking Vehicle.OwnerId in SaveChangesAsync.
/// </summary>
public interface IVehicleOwnedEntity
{
    /// <summary>
    /// The ID of the vehicle this entity belongs to.
    /// </summary>
    int VehicleId { get; set; }
}
