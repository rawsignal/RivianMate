using RivianMate.Core.Enums;
using RivianMate.Core.Interfaces;
using DriveType = RivianMate.Core.Enums.DriveType;

namespace RivianMate.Core.Entities;

/// <summary>
/// Represents a Rivian vehicle linked to the user's account
/// </summary>
public class Vehicle : IOwnerOwnedEntity
{
    public int Id { get; set; }

    /// <summary>
    /// Public identifier used in URLs and external references.
    /// Non-sequential to prevent enumeration attacks.
    /// </summary>
    public Guid PublicId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Rivian's unique vehicle identifier
    /// </summary>
    public required string RivianVehicleId { get; set; }
    
    /// <summary>
    /// Vehicle Identification Number
    /// </summary>
    public string? Vin { get; set; }
    
    /// <summary>
    /// User-assigned vehicle name (e.g., "My R1T")
    /// </summary>
    public string? Name { get; set; }
    
    public VehicleModel Model { get; set; } = VehicleModel.Unknown;
    
    public BatteryPackType BatteryPack { get; set; } = BatteryPackType.Unknown;
    
    public DriveType DriveType { get; set; } = DriveType.Unknown;

    public VehicleTrim Trim { get; set; } = VehicleTrim.Unknown;

    /// <summary>
    /// Model year
    /// </summary>
    public int? Year { get; set; }
    
    /// <summary>
    /// Exterior color
    /// </summary>
    public string? ExteriorColor { get; set; }
    
    /// <summary>
    /// Interior color
    /// </summary>
    public string? InteriorColor { get; set; }
    
    /// <summary>
    /// Wheel configuration (e.g., "20-inch AT", "22-inch Sport")
    /// </summary>
    public string? WheelConfig { get; set; }
    
    /// <summary>
    /// Original EPA-rated range in miles for this configuration
    /// </summary>
    public double? EpaRangeMiles { get; set; }
    
    /// <summary>
    /// Original usable battery capacity in kWh
    /// </summary>
    public double? OriginalCapacityKwh { get; set; }

    /// <summary>
    /// Battery cell chemistry type (e.g., "NMC", "LFP")
    /// NMC = Nickel Manganese Cobalt - degrades faster with high charge levels
    /// LFP = Lithium Iron Phosphate - more tolerant of 100% charging
    /// </summary>
    public string? BatteryCellType { get; set; }

    /// <summary>
    /// Current software version
    /// </summary>
    public string? SoftwareVersion { get; set; }

    /// <summary>
    /// Vehicle image data (PNG format, three-quarter view)
    /// Fetched once from Rivian API and cached
    /// </summary>
    public byte[]? ImageData { get; set; }

    /// <summary>
    /// Content type of the stored image (e.g., "image/png")
    /// </summary>
    public string? ImageContentType { get; set; }

    /// <summary>
    /// The vehicle version number (1-5) that works for fetching images from Rivian API.
    /// Stored to avoid trying all versions on subsequent requests.
    /// </summary>
    public int? ImageVersion { get; set; }
    
    /// <summary>
    /// When the vehicle was first added to RivianMate
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Last time we received data from this vehicle
    /// </summary>
    public DateTime? LastSeenAt { get; set; }
    
    /// <summary>
    /// Is this vehicle currently being actively polled?
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// The user who owns this vehicle (null for legacy single-user data)
    /// </summary>
    public Guid? OwnerId { get; set; }

    /// <summary>
    /// The Rivian account this vehicle belongs to
    /// </summary>
    public int? RivianAccountId { get; set; }

    // Navigation properties
    public ApplicationUser? Owner { get; set; }
    public RivianAccount? RivianAccount { get; set; }
    public ICollection<VehicleState> States { get; set; } = new List<VehicleState>();
    public ICollection<Drive> Drives { get; set; } = new List<Drive>();
    public ICollection<ChargingSession> ChargingSessions { get; set; } = new List<ChargingSession>();
    public ICollection<BatteryHealthSnapshot> BatteryHealthSnapshots { get; set; } = new List<BatteryHealthSnapshot>();
}
