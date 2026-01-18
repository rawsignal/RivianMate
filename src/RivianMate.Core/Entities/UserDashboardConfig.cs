using RivianMate.Core.Enums;

namespace RivianMate.Core.Entities;

/// <summary>
/// Stores per-user dashboard card configuration (visibility and order).
/// Each row represents a single card's configuration for a user.
/// </summary>
public class UserDashboardConfig
{
    public int Id { get; set; }

    /// <summary>
    /// The user who owns this configuration
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// The card identifier (e.g., "battery-health", "soc", "charging")
    /// </summary>
    public required string CardId { get; set; }

    /// <summary>
    /// Whether this card is visible on the dashboard
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// Display order within the card's section (lower = first)
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// When this configuration was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public ApplicationUser? User { get; set; }
}
