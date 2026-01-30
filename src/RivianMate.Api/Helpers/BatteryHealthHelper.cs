namespace RivianMate.Api.Helpers;

/// <summary>
/// Centralized battery health display mappings used across BatteryHealth page and BatteryHealthCard.
/// </summary>
public static class BatteryHealthHelper
{
    public static string GetHealthColor(double percent) => percent switch
    {
        >= 95 => "#4ADE80",
        >= 90 => "#7DD87D",
        >= 85 => "#DEB526",
        >= 80 => "#F59E0B",
        _ => "#EF4444"
    };

    public static string GetHealthStatus(double percent) => percent switch
    {
        >= 95 => "Excellent",
        >= 90 => "Very Good",
        >= 85 => "Good",
        >= 80 => "Fair",
        _ => "Needs Attention"
    };
}
