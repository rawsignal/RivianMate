using RivianMate.Core.Enums;
using DriveType = RivianMate.Core.Enums.DriveType;

namespace RivianMate.Core;

/// <summary>
/// Official battery pack specifications from Rivian.
/// Sources: 
/// - https://rivian.com/support/article/what-is-the-usable-kwh-capacity-of-your-battery-packs
/// - Community research and API observations
/// </summary>
public static class BatteryPackSpecs
{
    /// <summary>
    /// Get the original usable capacity in kWh for a given pack type and generation.
    /// </summary>
    public static double GetOriginalCapacityKwh(BatteryPackType packType, int? modelYear = null)
    {
        // Gen 2 starts with model year 2025
        var isGen2 = modelYear >= 2025;
        
        return (packType, isGen2) switch
        {
            // Gen 2 (2025+) official usable capacities
            (BatteryPackType.Standard, true) => 92.5,
            (BatteryPackType.Large, true) => 108.5,
            (BatteryPackType.Max, true) => 140.0,
            
            // Gen 1 (2022-2024) official usable capacities
            (BatteryPackType.Standard, false) => 106.0,
            // Note: Standard+ was 121 kWh but is discontinued in Gen 2
            (BatteryPackType.Large, false) => 131.0,
            (BatteryPackType.Max, false) => 141.0,
            
            // Unknown - use Gen 1 Large as a reasonable default
            _ => 131.0
        };
    }
    
    /// <summary>
    /// Get the EPA-rated range in miles for a given configuration.
    /// These are approximate and vary by model (R1T vs R1S) and wheel size.
    /// </summary>
    public static double GetEpaRangeMiles(VehicleModel model, BatteryPackType packType, 
        DriveType driveType, int? modelYear = null)
    {
        var isGen2 = modelYear >= 2025;
        
        // Gen 2 R1T/R1S ranges (approximate, varies by wheel size)
        if (isGen2)
        {
            return (packType, driveType, model) switch
            {
                // R1T Gen 2
                (BatteryPackType.Standard, DriveType.DualMotor, VehicleModel.R1T) => 260,
                (BatteryPackType.Large, DriveType.DualMotor, VehicleModel.R1T) => 330,
                (BatteryPackType.Large, DriveType.QuadMotor, VehicleModel.R1T) => 315,
                (BatteryPackType.Max, DriveType.DualMotor, VehicleModel.R1T) => 420,
                (BatteryPackType.Max, DriveType.TriMotor, VehicleModel.R1T) => 390,
                (BatteryPackType.Max, DriveType.QuadMotor, VehicleModel.R1T) => 370,
                
                // R1S Gen 2 (slightly less range than R1T due to weight/aerodynamics)
                (BatteryPackType.Standard, DriveType.DualMotor, VehicleModel.R1S) => 250,
                (BatteryPackType.Large, DriveType.DualMotor, VehicleModel.R1S) => 315,
                (BatteryPackType.Large, DriveType.QuadMotor, VehicleModel.R1S) => 300,
                (BatteryPackType.Max, DriveType.DualMotor, VehicleModel.R1S) => 400,
                (BatteryPackType.Max, DriveType.TriMotor, VehicleModel.R1S) => 370,
                (BatteryPackType.Max, DriveType.QuadMotor, VehicleModel.R1S) => 350,
                
                _ => 300 // Default estimate
            };
        }
        
        // Gen 1 ranges (approximate)
        return (packType, driveType, model) switch
        {
            // R1T Gen 1
            (BatteryPackType.Standard, _, VehicleModel.R1T) => 270,
            (BatteryPackType.Large, DriveType.DualMotor, VehicleModel.R1T) => 352,
            (BatteryPackType.Large, DriveType.QuadMotor, VehicleModel.R1T) => 328,
            (BatteryPackType.Max, _, VehicleModel.R1T) => 400,
            
            // R1S Gen 1
            (BatteryPackType.Standard, _, VehicleModel.R1S) => 260,
            (BatteryPackType.Large, DriveType.DualMotor, VehicleModel.R1S) => 340,
            (BatteryPackType.Large, DriveType.QuadMotor, VehicleModel.R1S) => 316,
            (BatteryPackType.Max, _, VehicleModel.R1S) => 390,
            
            _ => 300 // Default estimate
        };
    }
    
    /// <summary>
    /// Attempt to determine battery pack type from reported capacity.
    /// Useful when pack type isn't explicitly provided in vehicle config.
    /// </summary>
    public static BatteryPackType InferPackTypeFromCapacity(double capacityKwh, int? modelYear = null)
    {
        var isGen2 = modelYear >= 2025;
        
        if (isGen2)
        {
            // Gen 2 thresholds (midpoints between pack sizes)
            return capacityKwh switch
            {
                < 100.5 => BatteryPackType.Standard,   // < midpoint of 92.5 and 108.5
                < 124.25 => BatteryPackType.Large,     // < midpoint of 108.5 and 140
                _ => BatteryPackType.Max
            };
        }
        
        // Gen 1 thresholds
        return capacityKwh switch
        {
            < 113.5 => BatteryPackType.Standard,   // < midpoint of 106 and 121 (Standard+)
            < 126 => BatteryPackType.Large,        // Could be Standard+ (121) or degraded Large
            < 136 => BatteryPackType.Large,        // 131 kWh nominal
            _ => BatteryPackType.Max               // 141 kWh nominal
        };
    }
    
    /// <summary>
    /// Calculate battery health percentage from current and original capacity.
    /// </summary>
    public static double CalculateHealthPercent(double currentCapacityKwh, double originalCapacityKwh)
    {
        if (originalCapacityKwh <= 0) return 0;
        return (currentCapacityKwh / originalCapacityKwh) * 100.0;
    }
    
    /// <summary>
    /// Calculate battery health percentage using pack type to determine original capacity.
    /// </summary>
    public static double CalculateHealthPercent(double currentCapacityKwh, BatteryPackType packType, int? modelYear = null)
    {
        var originalCapacity = GetOriginalCapacityKwh(packType, modelYear);
        return CalculateHealthPercent(currentCapacityKwh, originalCapacity);
    }
    
    /// <summary>
    /// Warranty capacity threshold - Rivian warrants battery to retain 70% capacity.
    /// </summary>
    public const double WarrantyThresholdPercent = 70.0;

    /// <summary>
    /// Warranty duration in years - all configurations have 8 year warranty.
    /// </summary>
    public const int WarrantyYears = 8;

    /// <summary>
    /// Get the warranty mileage limit for a specific vehicle configuration.
    /// Source: https://rivian.com/support/article/what-is-the-warranty-on-my-battery
    ///
    /// Gen 2 (2025+):
    /// - Standard + Dual Motor: 120,000 miles
    /// - Large + Dual Motor: 150,000 miles
    /// - Max + Dual/Tri/Quad Motor: 150,000 miles
    ///
    /// Gen 1 (2022-2024):
    /// - Standard/Standard+ + Dual Motor: 120,000 miles
    /// - Large + Dual Motor: 150,000 miles
    /// - Max + Dual Motor: 150,000 miles
    /// - Large + Quad Motor: 175,000 miles
    /// </summary>
    public static int GetWarrantyMiles(BatteryPackType packType, DriveType driveType, int? modelYear = null)
    {
        var isGen2 = modelYear >= 2025;

        // Standard pack always gets 120k miles
        if (packType == BatteryPackType.Standard)
            return 120_000;

        // Gen 1 Large + Quad Motor gets the highest coverage at 175k
        if (!isGen2 && packType == BatteryPackType.Large && driveType == DriveType.QuadMotor)
            return 175_000;

        // All other Large and Max configurations get 150k miles
        return 150_000;
    }

    /// <summary>
    /// Default warranty miles when vehicle configuration is unknown.
    /// Uses the most common configuration (Large + Dual Motor = 150k miles).
    /// </summary>
    public const int DefaultWarrantyMiles = 150_000;
}
