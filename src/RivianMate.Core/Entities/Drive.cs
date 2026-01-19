using RivianMate.Core.Interfaces;

namespace RivianMate.Core.Entities;

/// <summary>
/// Represents a driving session from vehicle start to park.
/// Derived from analyzing VehicleState snapshots.
/// </summary>
public class Drive : IVehicleOwnedEntity
{
    public int Id { get; set; }
    
    public int VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;
    
    /// <summary>
    /// When the drive started
    /// </summary>
    public DateTime StartTime { get; set; }
    
    /// <summary>
    /// When the drive ended
    /// </summary>
    public DateTime? EndTime { get; set; }
    
    /// <summary>
    /// Is this drive still in progress?
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    // === Distance ===
    /// <summary>
    /// Odometer at start of drive
    /// </summary>
    public double StartOdometer { get; set; }
    
    /// <summary>
    /// Odometer at end of drive
    /// </summary>
    public double? EndOdometer { get; set; }
    
    /// <summary>
    /// Distance driven (miles)
    /// </summary>
    public double? DistanceMiles { get; set; }
    
    // === Battery Usage ===
    /// <summary>
    /// Battery level at start (%)
    /// </summary>
    public double StartBatteryLevel { get; set; }
    
    /// <summary>
    /// Battery level at end (%)
    /// </summary>
    public double? EndBatteryLevel { get; set; }
    
    /// <summary>
    /// Estimated energy used in kWh
    /// </summary>
    public double? EnergyUsedKwh { get; set; }
    
    // === Range ===
    /// <summary>
    /// Range estimate at start (miles)
    /// </summary>
    public double? StartRangeEstimate { get; set; }
    
    /// <summary>
    /// Range estimate at end (miles)
    /// </summary>
    public double? EndRangeEstimate { get; set; }
    
    // === Efficiency ===
    /// <summary>
    /// Efficiency in miles per kWh
    /// </summary>
    public double? EfficiencyMilesPerKwh { get; set; }
    
    /// <summary>
    /// Efficiency in Wh per mile
    /// </summary>
    public double? EfficiencyWhPerMile { get; set; }
    
    // === Location ===
    public double? StartLatitude { get; set; }
    public double? StartLongitude { get; set; }
    public double? EndLatitude { get; set; }
    public double? EndLongitude { get; set; }
    
    /// <summary>
    /// Optional: Starting address
    /// </summary>
    public string? StartAddress { get; set; }
    
    /// <summary>
    /// Optional: Ending address
    /// </summary>
    public string? EndAddress { get; set; }
    
    // === Speed ===
    /// <summary>
    /// Maximum speed during drive (mph)
    /// </summary>
    public double? MaxSpeedMph { get; set; }
    
    /// <summary>
    /// Average speed during drive (mph)
    /// </summary>
    public double? AverageSpeedMph { get; set; }
    
    // === Elevation ===
    /// <summary>
    /// Starting elevation (meters)
    /// </summary>
    public double? StartElevation { get; set; }
    
    /// <summary>
    /// Ending elevation (meters)
    /// </summary>
    public double? EndElevation { get; set; }
    
    /// <summary>
    /// Total elevation gain during drive (meters)
    /// </summary>
    public double? ElevationGain { get; set; }
    
    // === Temperature ===
    /// <summary>
    /// Average outside/cabin temperature during drive
    /// </summary>
    public double? AverageTemperature { get; set; }
    
    // === Drive Mode ===
    /// <summary>
    /// Primary drive mode used (e.g., "everyday", "sport", "conserve")
    /// </summary>
    public string? DriveMode { get; set; }
    
    // === Positions ===
    /// <summary>
    /// Navigation property to recorded positions during this drive
    /// </summary>
    public ICollection<Position> Positions { get; set; } = new List<Position>();
}
