using RivianMate.Api.Services;
using RivianMate.Core.Entities;
using RivianMate.Core.Enums;

namespace RivianMate.Api.Components.Dashboard;

/// <summary>
/// Contains all data that dashboard cards might need.
/// Passed to cards via DynamicComponent to enable dynamic rendering.
/// </summary>
public class DashboardContext
{
    // Vehicle data
    public VehicleState? VehicleState { get; init; }
    public int? VehicleId { get; init; }

    // Battery health data
    public BatteryHealthSummary? HealthSummary { get; init; }
    public List<BatteryHealthSnapshot>? HealthHistory { get; init; }

    // Charging data
    public List<ChargingSession>? RecentSessions { get; init; }

    // Drives data
    public List<Drive>? RecentDrives { get; init; }

    // Computed values
    public double? Efficiency { get; init; }

    // Services
    public TimeZoneService? TimeZoneService { get; init; }

    // Section context
    public DashboardSection Section { get; init; }

    /// <summary>
    /// True for BottomStats cards that should render in compact mode.
    /// </summary>
    public bool IsCompact => Section == DashboardSection.BottomStats;
}
