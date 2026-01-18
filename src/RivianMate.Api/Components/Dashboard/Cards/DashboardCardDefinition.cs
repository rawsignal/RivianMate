namespace RivianMate.Api.Components.Dashboard.Cards;

/// <summary>
/// Definition for a dashboard card's UI metadata.
/// Used by card components to define their appearance and behavior.
/// Note: Grid layout (column span) is controlled by DashboardCardRegistry in Core.
/// </summary>
public record DashboardCardDefinition
{
    /// <summary>Unique identifier for the card</summary>
    public required string CardId { get; init; }

    /// <summary>Icon name (from Icon component)</summary>
    public required string Icon { get; init; }

    /// <summary>Card title displayed in header</summary>
    public required string Title { get; init; }

    /// <summary>URL to navigate to for details (null = no link)</summary>
    public string? DetailUrl { get; init; }

    /// <summary>Label for the detail link (e.g., "View Details", "View All")</summary>
    public string? DetailLabel { get; init; }

    /// <summary>Whether to show the card header (title and icon)</summary>
    public bool ShowHeader { get; init; } = true;
}
