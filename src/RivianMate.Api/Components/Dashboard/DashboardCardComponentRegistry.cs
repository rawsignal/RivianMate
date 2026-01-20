using RivianMate.Api.Components.Dashboard.Cards;

namespace RivianMate.Api.Components.Dashboard;

/// <summary>
/// Maps card IDs to their component types for dynamic rendering.
/// To add a new card, register it here after creating the component.
/// </summary>
public static class DashboardCardComponentRegistry
{
    private static readonly Dictionary<string, Type> _components = new()
    {
        ["soc"] = typeof(SocCard),
        ["range"] = typeof(RangeCard),
        ["health-summary"] = typeof(HealthSummaryCard),
        ["battery-health"] = typeof(BatteryHealthCard),
        ["charging"] = typeof(ChargingCard),
        ["drives"] = typeof(DrivesCard),
        ["activity-feed"] = typeof(ActivityFeedCard),
        ["recalls"] = typeof(RecallsCard),
        ["odometer"] = typeof(OdometerCard),
        ["cabin-temp"] = typeof(CabinTempCard),
        ["efficiency"] = typeof(EfficiencyCard),
        ["software"] = typeof(SoftwareCard),
        ["vehicle-status"] = typeof(VehicleStatusCard),
    };

    /// <summary>
    /// Gets the component type for a given card ID.
    /// </summary>
    /// <param name="cardId">The card ID to look up.</param>
    /// <returns>The component Type, or null if not found.</returns>
    public static Type? GetComponentType(string cardId) =>
        _components.TryGetValue(cardId, out var type) ? type : null;
}
