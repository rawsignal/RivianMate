namespace RivianMate.Core.Enums;

/// <summary>
/// Defines the sections of the dashboard where cards can be placed
/// </summary>
public enum DashboardSection
{
    /// <summary>
    /// Top row with quick stat cards (SOC, Range, Health Summary)
    /// </summary>
    QuickStats,

    /// <summary>
    /// Main content grid (Battery Health detail, Recent Charging)
    /// </summary>
    MainGrid,

    /// <summary>
    /// Bottom row with smaller stat cards (Odometer, Temp, Efficiency, Software)
    /// </summary>
    BottomStats
}
