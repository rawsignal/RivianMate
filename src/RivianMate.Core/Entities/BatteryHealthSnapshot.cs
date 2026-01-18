namespace RivianMate.Core.Entities;

/// <summary>
/// A periodic snapshot of battery health.
/// Now that we have batteryCapacity directly from the API, this is much simpler.
/// We just need to track the reported capacity over time and compare to original.
/// </summary>
public class BatteryHealthSnapshot
{
    public int Id { get; set; }
    
    public int VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;
    
    /// <summary>
    /// When this snapshot was recorded
    /// </summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// Odometer reading at time of snapshot (miles)
    /// </summary>
    public double Odometer { get; set; }
    
    // === Capacity from API ===
    
    /// <summary>
    /// Battery capacity as reported by Rivian API (kWh).
    /// This is the current usable capacity.
    /// </summary>
    public double ReportedCapacityKwh { get; set; }
    
    /// <summary>
    /// State of charge when this reading was taken (%).
    /// Capacity readings may vary with SoC, so we track this for analysis.
    /// </summary>
    public double? StateOfCharge { get; set; }
    
    /// <summary>
    /// Cabin/ambient temperature when reading was taken (Celsius).
    /// Temperature affects reported capacity.
    /// </summary>
    public double? Temperature { get; set; }
    
    // === Reference Values ===
    
    /// <summary>
    /// Original capacity when new for this pack type (kWh).
    /// From BatteryPackSpecs based on vehicle configuration.
    /// </summary>
    public double OriginalCapacityKwh { get; set; }
    
    // === Calculated Health Metrics ===
    
    /// <summary>
    /// Battery health percentage: (ReportedCapacity / OriginalCapacity) * 100
    /// </summary>
    public double HealthPercent { get; set; }
    
    /// <summary>
    /// Capacity lost since new (kWh)
    /// </summary>
    public double CapacityLostKwh { get; set; }
    
    /// <summary>
    /// Degradation in percentage points from 100%
    /// </summary>
    public double DegradationPercent { get; set; }
    
    // === Trend Analysis (calculated from historical data) ===
    
    /// <summary>
    /// Average degradation rate in % per 10,000 miles.
    /// Calculated from trend of previous snapshots.
    /// </summary>
    public double? DegradationRatePer10kMiles { get; set; }
    
    /// <summary>
    /// Projected health % at 100,000 miles based on current trend.
    /// </summary>
    public double? ProjectedHealthAt100kMiles { get; set; }
    
    /// <summary>
    /// Projected mileage when battery will hit warranty threshold (70%).
    /// Null if degradation rate suggests it won't hit threshold.
    /// </summary>
    public double? ProjectedMilesTo70Percent { get; set; }
    
    /// <summary>
    /// Estimated remaining warranty-covered capacity loss (kWh).
    /// How much more capacity can be lost before hitting 70% threshold.
    /// </summary>
    public double? RemainingWarrantyCapacityKwh { get; set; }
}

