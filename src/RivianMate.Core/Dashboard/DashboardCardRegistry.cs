using RivianMate.Core.Enums;
using RivianMate.Core.Licensing;

namespace RivianMate.Core.Dashboard;

/// <summary>
/// Defines a dashboard card's properties
/// </summary>
public record DashboardCardDefinition(
    string Id,
    string Name,
    string Icon,
    DashboardSection Section,
    int DefaultOrder,
    int ColSpan = 1,
    string? RequiredFeature = null,
    bool SelfHostedOnly = false
);

/// <summary>
/// Registry of all available dashboard cards with their default settings
/// </summary>
public static class DashboardCardRegistry
{
    /// <summary>
    /// All available dashboard cards indexed by ID
    /// </summary>
    public static IReadOnlyDictionary<string, DashboardCardDefinition> Cards { get; }

    /// <summary>
    /// Get cards by section, ordered by default order
    /// </summary>
    public static IEnumerable<DashboardCardDefinition> GetBySection(DashboardSection section) =>
        Cards.Values.Where(c => c.Section == section).OrderBy(c => c.DefaultOrder);

    /// <summary>
    /// Get all cards ordered by section and default order
    /// </summary>
    public static IEnumerable<DashboardCardDefinition> GetAllOrdered() =>
        Cards.Values
            .OrderBy(c => c.Section)
            .ThenBy(c => c.DefaultOrder);

    static DashboardCardRegistry()
    {
        var cards = new Dictionary<string, DashboardCardDefinition>();

        // Quick Stats Row
        cards.Add("soc", new DashboardCardDefinition(
            Id: "soc",
            Name: "State of Charge",
            Icon: "battery",
            Section: DashboardSection.QuickStats,
            DefaultOrder: 0
        ));

        cards.Add("range", new DashboardCardDefinition(
            Id: "range",
            Name: "Estimated Range",
            Icon: "gauge",
            Section: DashboardSection.QuickStats,
            DefaultOrder: 1
        ));

        cards.Add("health-summary", new DashboardCardDefinition(
            Id: "health-summary",
            Name: "Battery Health",
            Icon: "activity",
            Section: DashboardSection.QuickStats,
            DefaultOrder: 2
        ));

        // Main Grid
        cards.Add("battery-health", new DashboardCardDefinition(
            Id: "battery-health",
            Name: "Battery Health Detail",
            Icon: "battery",
            Section: DashboardSection.MainGrid,
            DefaultOrder: 0,
            ColSpan: 3
        ));

        cards.Add("charging", new DashboardCardDefinition(
            Id: "charging",
            Name: "Recent Charging",
            Icon: "zap",
            Section: DashboardSection.MainGrid,
            DefaultOrder: 1
        ));

        cards.Add("drives", new DashboardCardDefinition(
            Id: "drives",
            Name: "Recent Drives",
            Icon: "navigation",
            Section: DashboardSection.MainGrid,
            DefaultOrder: 2,
            ColSpan: 2
        ));

        // Bottom Stats Row
        cards.Add("odometer", new DashboardCardDefinition(
            Id: "odometer",
            Name: "Odometer",
            Icon: "map-pin",
            Section: DashboardSection.BottomStats,
            DefaultOrder: 0
        ));

        cards.Add("cabin-temp", new DashboardCardDefinition(
            Id: "cabin-temp",
            Name: "Cabin Temperature",
            Icon: "thermometer",
            Section: DashboardSection.BottomStats,
            DefaultOrder: 1
        ));

        cards.Add("efficiency", new DashboardCardDefinition(
            Id: "efficiency",
            Name: "Efficiency",
            Icon: "activity",
            Section: DashboardSection.BottomStats,
            DefaultOrder: 2
        ));

        cards.Add("software", new DashboardCardDefinition(
            Id: "software",
            Name: "Software Version",
            Icon: "smartphone",
            Section: DashboardSection.BottomStats,
            DefaultOrder: 3
        ));

        cards.Add("vehicle-status", new DashboardCardDefinition(
            Id: "vehicle-status",
            Name: "Vehicle Status",
            Icon: "activity",
            Section: DashboardSection.MainGrid,
            DefaultOrder: 3
        ));

        cards.Add("activity-feed", new DashboardCardDefinition(
            Id: "activity-feed",
            Name: "Activity Feed",
            Icon: "list",
            Section: DashboardSection.MainGrid,
            DefaultOrder: 4
        ));

        cards.Add("recalls", new DashboardCardDefinition(
            Id: "recalls",
            Name: "Safety Recalls",
            Icon: "alert-triangle",
            Section: DashboardSection.MainGrid,
            DefaultOrder: 5
        ));

        cards.Add("referral", new DashboardCardDefinition(
            Id: "referral",
            Name: "Referrals",
            Icon: "gift",
            Section: DashboardSection.MainGrid,
            DefaultOrder: 6,
            RequiredFeature: Features.Referrals
        ));

        cards.Add("support", new DashboardCardDefinition(
            Id: "support",
            Name: "Support RivianMate",
            Icon: "coffee",
            Section: DashboardSection.BottomStats,
            DefaultOrder: 4,
            SelfHostedOnly: true
        ));

        Cards = cards;
    }
}
