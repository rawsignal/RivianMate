using Microsoft.EntityFrameworkCore;
using RivianMate.Core.Dashboard;
using RivianMate.Core.Entities;
using RivianMate.Core.Enums;
using RivianMate.Infrastructure.Data;

namespace RivianMate.Api.Services;

/// <summary>
/// Represents a card's configuration merged with its definition
/// </summary>
public record DashboardCardConfig(
    string CardId,
    string Name,
    string Icon,
    DashboardSection Section,
    int Order,
    bool IsVisible,
    int ColSpan
);

/// <summary>
/// Service for managing user dashboard configurations
/// </summary>
public class DashboardConfigService
{
    private readonly RivianMateDbContext _db;
    private readonly ILogger<DashboardConfigService> _logger;

    public DashboardConfigService(RivianMateDbContext db, ILogger<DashboardConfigService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Gets the merged dashboard configuration for a user.
    /// Returns default card settings merged with any user customizations.
    /// </summary>
    public async Task<List<DashboardCardConfig>> GetUserConfigAsync(Guid userId)
    {
        // Get user's custom configurations
        var userConfigs = await _db.UserDashboardConfigs
            .Where(c => c.UserId == userId)
            .ToDictionaryAsync(c => c.CardId);

        var result = new List<DashboardCardConfig>();

        foreach (var card in DashboardCardRegistry.Cards.Values)
        {
            if (userConfigs.TryGetValue(card.Id, out var userConfig))
            {
                // User has customized this card
                result.Add(new DashboardCardConfig(
                    CardId: card.Id,
                    Name: card.Name,
                    Icon: card.Icon,
                    Section: card.Section,
                    Order: userConfig.Order,
                    IsVisible: userConfig.IsVisible,
                    ColSpan: card.ColSpan
                ));
            }
            else
            {
                // Use default configuration
                result.Add(new DashboardCardConfig(
                    CardId: card.Id,
                    Name: card.Name,
                    Icon: card.Icon,
                    Section: card.Section,
                    Order: card.DefaultOrder,
                    IsVisible: true,
                    ColSpan: card.ColSpan
                ));
            }
        }

        return result;
    }

    /// <summary>
    /// Gets visible cards for a specific section, ordered appropriately
    /// </summary>
    public async Task<List<DashboardCardConfig>> GetVisibleCardsForSectionAsync(Guid userId, DashboardSection section)
    {
        var allConfigs = await GetUserConfigAsync(userId);
        return allConfigs
            .Where(c => c.Section == section && c.IsVisible)
            .OrderBy(c => c.Order)
            .ToList();
    }

    /// <summary>
    /// Gets all hidden cards for display in the "Add Card" modal
    /// </summary>
    public async Task<List<DashboardCardConfig>> GetHiddenCardsAsync(Guid userId)
    {
        var allConfigs = await GetUserConfigAsync(userId);
        return allConfigs
            .Where(c => !c.IsVisible)
            .OrderBy(c => c.Section)
            .ThenBy(c => c.Name)
            .ToList();
    }

    /// <summary>
    /// Sets the visibility of a card for a user
    /// </summary>
    public async Task SetCardVisibilityAsync(Guid userId, string cardId, bool visible)
    {
        if (!DashboardCardRegistry.Cards.ContainsKey(cardId))
        {
            _logger.LogWarning("Attempted to set visibility for unknown card: {CardId}", cardId);
            return;
        }

        var config = await _db.UserDashboardConfigs
            .FirstOrDefaultAsync(c => c.UserId == userId && c.CardId == cardId);

        if (config != null)
        {
            config.IsVisible = visible;
            config.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            // Create new config with user override
            var cardDef = DashboardCardRegistry.Cards[cardId];
            config = new UserDashboardConfig
            {
                UserId = userId,
                CardId = cardId,
                IsVisible = visible,
                Order = cardDef.DefaultOrder,
                UpdatedAt = DateTime.UtcNow
            };
            _db.UserDashboardConfigs.Add(config);
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Set card {CardId} visibility to {Visible} for user {UserId}", cardId, visible, userId);
    }

    /// <summary>
    /// Reorders cards within a section for a user
    /// </summary>
    public async Task ReorderCardsAsync(Guid userId, DashboardSection section, IReadOnlyList<string> orderedCardIds)
    {
        // Get existing user configs for this section
        var existingConfigs = await _db.UserDashboardConfigs
            .Where(c => c.UserId == userId)
            .ToDictionaryAsync(c => c.CardId);

        for (int i = 0; i < orderedCardIds.Count; i++)
        {
            var cardId = orderedCardIds[i];
            if (!DashboardCardRegistry.Cards.TryGetValue(cardId, out var cardDef))
            {
                continue;
            }

            // Only process cards that belong to this section
            if (cardDef.Section != section)
            {
                continue;
            }

            if (existingConfigs.TryGetValue(cardId, out var config))
            {
                config.Order = i;
                config.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                // Create new config with custom order
                var newConfig = new UserDashboardConfig
                {
                    UserId = userId,
                    CardId = cardId,
                    IsVisible = true,
                    Order = i,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.UserDashboardConfigs.Add(newConfig);
            }
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Reordered {Count} cards in section {Section} for user {UserId}",
            orderedCardIds.Count, section, userId);
    }

    /// <summary>
    /// Resets all dashboard customizations for a user, restoring defaults
    /// </summary>
    public async Task ResetToDefaultsAsync(Guid userId)
    {
        var userConfigs = await _db.UserDashboardConfigs
            .Where(c => c.UserId == userId)
            .ToListAsync();

        _db.UserDashboardConfigs.RemoveRange(userConfigs);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Reset dashboard to defaults for user {UserId}", userId);
    }
}
