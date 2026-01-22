using RivianMate.Core.Interfaces;

namespace RivianMate.Core.Entities;

/// <summary>
/// Represents a named charging location for a user (Home, Work, etc.).
/// Charging sessions within 100m of these locations will be tagged with the location name.
/// </summary>
public class UserLocation : IUserOwnedEntity
{
    public int Id { get; set; }

    /// <summary>
    /// The user who owns this location
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Name of the location (e.g., "Home", "Work", "Beach House")
    /// </summary>
    public string Name { get; set; } = "Home";

    /// <summary>
    /// Latitude coordinate
    /// </summary>
    public double Latitude { get; set; }

    /// <summary>
    /// Longitude coordinate
    /// </summary>
    public double Longitude { get; set; }

    /// <summary>
    /// Whether this is the default/primary location
    /// </summary>
    public bool IsDefault { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public ApplicationUser? User { get; set; }
}
