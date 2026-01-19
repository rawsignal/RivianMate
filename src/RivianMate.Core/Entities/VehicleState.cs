using RivianMate.Core.Enums;
using RivianMate.Core.Interfaces;

namespace RivianMate.Core.Entities;

/// <summary>
/// A snapshot of vehicle state at a specific point in time.
/// This is the raw data collected from the Rivian API.
/// </summary>
public class VehicleState : IVehicleOwnedEntity
{
    public long Id { get; set; }
    
    public int VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;
    
    /// <summary>
    /// When this state was recorded
    /// </summary>
    public DateTime Timestamp { get; set; }
    
    // === Location ===
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? Altitude { get; set; }  // meters
    public double? Speed { get; set; }      // m/s
    public double? Heading { get; set; }    // degrees (bearing)

    // === Driver ===
    public string? ActiveDriverName { get; set; }
    
    // === Battery & Range ===
    /// <summary>
    /// State of charge percentage (0-100)
    /// </summary>
    public double? BatteryLevel { get; set; }

    /// <summary>
    /// User-set charge limit percentage
    /// </summary>
    public double? BatteryLimit { get; set; }

    /// <summary>
    /// Current usable battery capacity in kWh.
    /// This is the actual capacity the vehicle reports - it decreases with degradation.
    /// Compare to original capacity for the pack type to calculate battery health.
    /// </summary>
    public double? BatteryCapacityKwh { get; set; }

    /// <summary>
    /// Estimated range remaining (miles)
    /// </summary>
    public double? RangeEstimate { get; set; }

    /// <summary>
    /// Calculated: RangeEstimate / (BatteryLevel / 100) = projected range at 100%
    /// Stored for easy querying
    /// </summary>
    public double? ProjectedRangeAt100 { get; set; }

    /// <summary>
    /// 12V battery health status
    /// </summary>
    public string? TwelveVoltBatteryHealth { get; set; }

    /// <summary>
    /// Battery cell chemistry type (e.g., "NMC", "LFP")
    /// NMC = Nickel Manganese Cobalt - degrades faster with high charge levels
    /// LFP = Lithium Iron Phosphate - more tolerant of 100% charging
    /// </summary>
    public string? BatteryCellType { get; set; }

    /// <summary>
    /// Whether the LFP battery needs a calibration charge to 100%
    /// </summary>
    public bool? BatteryNeedsLfpCalibration { get; set; }
    
    // === Odometer ===
    /// <summary>
    /// Total vehicle mileage (miles, stored as reported - may be in meters from API)
    /// </summary>
    public double? Odometer { get; set; }
    
    // === Power & Drive State ===
    public PowerState PowerState { get; set; } = PowerState.Unknown;
    public GearStatus GearStatus { get; set; } = GearStatus.Unknown;
    public string? DriveMode { get; set; }  // "everyday", "sport", "conserve", "off-road", etc.
    
    // === Charging ===
    public ChargerState ChargerState { get; set; } = ChargerState.Unknown;

    /// <summary>
    /// Minutes remaining to reach charge limit
    /// </summary>
    public int? TimeToEndOfCharge { get; set; }

    /// <summary>
    /// Charge port state (open/closed)
    /// </summary>
    public bool? ChargePortOpen { get; set; }

    /// <summary>
    /// Charger derate status (thermal limiting, etc.)
    /// </summary>
    public string? ChargerDerateStatus { get; set; }

    // === Cold Weather ===
    /// <summary>
    /// Acceleration limited due to cold battery
    /// </summary>
    public bool? LimitedAccelCold { get; set; }

    /// <summary>
    /// Regenerative braking limited due to cold battery
    /// </summary>
    public bool? LimitedRegenCold { get; set; }
    
    // === Climate ===
    /// <summary>
    /// Cabin temperature in Celsius
    /// </summary>
    public double? CabinTemperature { get; set; }
    
    /// <summary>
    /// Driver-set temperature target in Celsius
    /// </summary>
    public double? ClimateTargetTemp { get; set; }
    
    public bool? IsPreconditioningActive { get; set; }
    public bool? IsPetModeActive { get; set; }
    public bool? IsDefrostActive { get; set; }
    
    // === Closures (simplified - we can expand later) ===
    public bool? AllDoorsClosed { get; set; }
    public bool? AllDoorsLocked { get; set; }
    public bool? AllWindowsClosed { get; set; }
    public bool? FrunkClosed { get; set; }
    public bool? FrunkLocked { get; set; }
    public bool? LiftgateClosed { get; set; }      // R1S liftgate
    public bool? TailgateClosed { get; set; }      // R1T tailgate
    public bool? TonneauClosed { get; set; }       // R1T tonneau cover
    public bool? SideBinLeftClosed { get; set; }   // R1T gear tunnel left
    public bool? SideBinLeftLocked { get; set; }   // R1T gear tunnel left
    public bool? SideBinRightClosed { get; set; }  // R1T gear tunnel right
    public bool? SideBinRightLocked { get; set; }  // R1T gear tunnel right
    public string? GearGuardStatus { get; set; }   // "Disabled", "Enabled", "Engaged"
    
    // === Tire Pressure - Status ===
    public TirePressureStatus TirePressureStatusFrontLeft { get; set; } = TirePressureStatus.Unknown;
    public TirePressureStatus TirePressureStatusFrontRight { get; set; } = TirePressureStatus.Unknown;
    public TirePressureStatus TirePressureStatusRearLeft { get; set; } = TirePressureStatus.Unknown;
    public TirePressureStatus TirePressureStatusRearRight { get; set; } = TirePressureStatus.Unknown;

    // === Tire Pressure - Actual Values (PSI) ===
    public double? TirePressureFrontLeft { get; set; }
    public double? TirePressureFrontRight { get; set; }
    public double? TirePressureRearLeft { get; set; }
    public double? TirePressureRearRight { get; set; }
    
    // === Software ===
    public string? OtaCurrentVersion { get; set; }
    public string? OtaAvailableVersion { get; set; }
    public string? OtaStatus { get; set; }
    public int? OtaInstallProgress { get; set; }
    
    // === Raw Data ===
    /// <summary>
    /// Store the raw JSON response for debugging/future parsing
    /// </summary>
    public string? RawJson { get; set; }
}
