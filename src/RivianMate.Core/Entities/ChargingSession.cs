using RivianMate.Core.Enums;
using RivianMate.Core.Interfaces;

namespace RivianMate.Core.Entities;

/// <summary>
/// Represents a complete charging session from plug-in to plug-out.
/// Derived from analyzing VehicleState snapshots.
/// </summary>
public class ChargingSession : IVehicleOwnedEntity
{
    public int Id { get; set; }
    
    public int VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;
    
    /// <summary>
    /// When charging started (plugged in or charging began)
    /// </summary>
    public DateTime StartTime { get; set; }
    
    /// <summary>
    /// When charging ended (unplugged or completed)
    /// </summary>
    public DateTime? EndTime { get; set; }
    
    /// <summary>
    /// Is this charging session still in progress?
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    // === State of Charge ===
    /// <summary>
    /// Battery level when charging started (%)
    /// </summary>
    public double StartBatteryLevel { get; set; }
    
    /// <summary>
    /// Battery level when charging ended (%)
    /// </summary>
    public double? EndBatteryLevel { get; set; }
    
    /// <summary>
    /// The charge limit that was set (%)
    /// </summary>
    public double? ChargeLimit { get; set; }
    
    // === Energy ===
    /// <summary>
    /// Estimated energy added in kWh (calculated from SoC delta and estimated capacity)
    /// </summary>
    public double? EnergyAddedKwh { get; set; }
    
    /// <summary>
    /// Peak charging power observed in kW
    /// </summary>
    public double? PeakPowerKw { get; set; }
    
    /// <summary>
    /// Average charging power in kW
    /// </summary>
    public double? AveragePowerKw { get; set; }
    
    // === Range ===
    /// <summary>
    /// Estimated range when charging started (miles)
    /// </summary>
    public double? StartRangeEstimate { get; set; }
    
    /// <summary>
    /// Estimated range when charging ended (miles)
    /// </summary>
    public double? EndRangeEstimate { get; set; }
    
    /// <summary>
    /// Range added during this session (miles)
    /// </summary>
    public double? RangeAdded { get; set; }
    
    // === Charge Type & Location ===
    public ChargeType ChargeType { get; set; } = ChargeType.Unknown;

    /// <summary>
    /// Location where charging occurred
    /// </summary>
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    /// <summary>
    /// FK to user's saved location (if matched)
    /// </summary>
    public int? UserLocationId { get; set; }
    public UserLocation? UserLocation { get; set; }

    /// <summary>
    /// Cached location name for display (denormalized for performance).
    /// Updated when UserLocation is linked or when geocoded.
    /// </summary>
    public string? LocationName { get; set; }

    /// <summary>
    /// Is this a "home" charging location?
    /// </summary>
    public bool? IsHomeCharging { get; set; }
    
    // === Cost ===
    /// <summary>
    /// Charging cost from API (if available), in user's currency
    /// </summary>
    public double? Cost { get; set; }

    // === Drive Mode ===
    /// <summary>
    /// Drive mode at start of charging (e.g., "everyday", "sport", "conserve")
    /// </summary>
    public string? DriveMode { get; set; }

    // === Odometer ===
    /// <summary>
    /// Odometer reading at start of charge
    /// </summary>
    public double? OdometerAtStart { get; set; }
    
    // === Temperature ===
    /// <summary>
    /// Ambient/cabin temperature at start of charge
    /// </summary>
    public double? TemperatureAtStart { get; set; }
    
    // === Live Tracking (during active session) ===
    /// <summary>
    /// Current battery level during active charging (for progress tracking)
    /// </summary>
    public double? CurrentBatteryLevel { get; set; }

    /// <summary>
    /// Current estimated range during active charging
    /// </summary>
    public double? CurrentRangeEstimate { get; set; }

    /// <summary>
    /// Last time this session was updated with new data
    /// </summary>
    public DateTime? LastUpdatedAt { get; set; }

    // === Calculated Fields (for battery health) ===
    /// <summary>
    /// Calculated total battery capacity based on this charge session.
    /// Formula: EnergyAddedKwh / ((EndBatteryLevel - StartBatteryLevel) / 100)
    /// </summary>
    public double? CalculatedCapacityKwh { get; set; }

    /// <summary>
    /// Confidence score for the capacity calculation (0-1).
    /// Higher for longer charges with larger SoC deltas.
    /// </summary>
    public double? CapacityConfidence { get; set; }
}
