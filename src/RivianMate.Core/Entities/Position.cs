namespace RivianMate.Core.Entities;

/// <summary>
/// A GPS position recorded during a drive.
/// Used for mapping routes and calculating drive statistics.
/// </summary>
public class Position
{
    public long Id { get; set; }
    
    public int DriveId { get; set; }
    public Drive Drive { get; set; } = null!;
    
    public DateTime Timestamp { get; set; }
    
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? Altitude { get; set; }
    
    /// <summary>
    /// Speed at this position (mph)
    /// </summary>
    public double? Speed { get; set; }
    
    /// <summary>
    /// Heading in degrees (0-360)
    /// </summary>
    public double? Heading { get; set; }
    
    /// <summary>
    /// Battery level at this position (%)
    /// </summary>
    public double? BatteryLevel { get; set; }
    
    /// <summary>
    /// Odometer at this position
    /// </summary>
    public double? Odometer { get; set; }
}
