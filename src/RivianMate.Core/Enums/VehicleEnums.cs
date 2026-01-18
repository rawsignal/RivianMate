namespace RivianMate.Core.Enums;

/// <summary>
/// Rivian battery pack configurations
/// </summary>
public enum BatteryPackType
{
    Unknown = 0,
    Standard = 1,  // ~105 kWh usable, Standard Range
    Large = 2,     // ~128 kWh usable, Large Pack  
    Max = 3        // ~149 kWh usable, Max Pack
}

/// <summary>
/// Vehicle model types
/// </summary>
public enum VehicleModel
{
    Unknown = 0,
    R1T = 1,
    R1S = 2,
    R2 = 3,
    R3 = 4
}

/// <summary>
/// Drive configuration
/// </summary>
public enum DriveType
{
    Unknown = 0,
    DualMotor = 1,
    QuadMotor = 2,
    TriMotor = 3  // Performance dual + single
}

/// <summary>
/// Vehicle trim/package level
/// </summary>
public enum VehicleTrim
{
    Unknown = 0,
    Explore = 1,
    Adventure = 2,
    LaunchEdition = 3,
    Ascend = 4  // Gen 2
}

/// <summary>
/// Power state of the vehicle
/// </summary>
public enum PowerState
{
    Unknown = 0,
    Sleep = 1,
    Standby = 2,
    Ready = 3,
    Go = 4,
    Charging = 5
}

/// <summary>
/// Gear status
/// </summary>
public enum GearStatus
{
    Unknown = 0,
    Park = 1,
    Reverse = 2,
    Neutral = 3,
    Drive = 4
}

/// <summary>
/// Charger connection state
/// </summary>
public enum ChargerState
{
    Unknown = 0,
    Disconnected = 1,
    Connected = 2,
    ReadyToCharge = 3,
    Charging = 4,
    Complete = 5,
    Fault = 6
}

/// <summary>
/// Type of charging session
/// </summary>
public enum ChargeType
{
    Unknown = 0,
    AC_Level1 = 1,   // 120V
    AC_Level2 = 2,   // 240V home/destination
    DC_Fast = 3,     // DC fast charging (Rivian Adventure Network, EA, etc.)
}

/// <summary>
/// Closure state (doors, windows, etc.)
/// </summary>
public enum ClosureState
{
    Unknown = 0,
    Open = 1,
    Closed = 2,
    Ajar = 3
}

/// <summary>
/// Lock state
/// </summary>
public enum LockState
{
    Unknown = 0,
    Locked = 1,
    Unlocked = 2
}

/// <summary>
/// Tire pressure status
/// </summary>
public enum TirePressureStatus
{
    Unknown = 0,
    Ok = 1,
    Low = 2,
    High = 3,
    Critical = 4
}
