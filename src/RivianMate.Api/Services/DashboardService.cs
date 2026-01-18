using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RivianMate.Core.Dashboard;
using RivianMate.Core.Interfaces;
using RivianMate.Infrastructure.Data;

namespace RivianMate.Api.Services;

/// <summary>
/// Service for managing user dashboard configurations
/// </summary>
public class DashboardService : IDashboardService
{
    private readonly RivianMateDbContext _dbContext;
    private readonly ILogger<DashboardService> _logger;
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public DashboardService(RivianMateDbContext dbContext, ILogger<DashboardService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<DashboardConfiguration> GetDashboardAsync(Guid userId, Guid vehicleId)
    {
        var setting = await _dbContext.Settings
            .FirstOrDefaultAsync(s => s.Key == GetDashboardKey(userId, vehicleId));

        if (setting?.Value is null)
        {
            _logger.LogInformation(
                "No dashboard config found for user {UserId}, vehicle {VehicleId}. Returning default.",
                userId, vehicleId);
            return FeatureRegistry.GetDefaultDashboard(userId, vehicleId);
        }

        try
        {
            var config = JsonSerializer.Deserialize<DashboardConfiguration>(setting.Value, JsonOptions);
            return config ?? FeatureRegistry.GetDefaultDashboard(userId, vehicleId);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize dashboard config for user {UserId}", userId);
            return FeatureRegistry.GetDefaultDashboard(userId, vehicleId);
        }
    }

    public async Task SaveDashboardAsync(DashboardConfiguration config)
    {
        config.UpdatedAt = DateTime.UtcNow;
        
        var key = GetDashboardKey(config.UserId, config.VehicleId);
        var json = JsonSerializer.Serialize(config, JsonOptions);
        
        var setting = await _dbContext.Settings.FirstOrDefaultAsync(s => s.Key == key);
        
        if (setting is null)
        {
            setting = new Core.Entities.Setting
            {
                Key = key,
                Value = json,
                IsEncrypted = false
            };
            _dbContext.Settings.Add(setting);
        }
        else
        {
            setting.Value = json;
            setting.UpdatedAt = DateTime.UtcNow;
        }
        
        await _dbContext.SaveChangesAsync();
        
        _logger.LogInformation(
            "Saved dashboard config for user {UserId}, vehicle {VehicleId} with {CardCount} cards",
            config.UserId, config.VehicleId, config.Cards.Count);
    }

    public async Task<CardConfiguration> AddCardAsync(Guid userId, Guid vehicleId, CardConfiguration card)
    {
        var dashboard = await GetDashboardAsync(userId, vehicleId);
        
        // Validate the feature exists
        var feature = FeatureRegistry.GetFeature(card.FeatureId);
        if (feature is null)
        {
            throw new ArgumentException($"Unknown feature: {card.FeatureId}");
        }
        
        // Validate metrics exist
        ValidateCardMetrics(card, feature);
        
        // Set order to end of list
        card.Order = dashboard.Cards.Count > 0 
            ? dashboard.Cards.Max(c => c.Order) + 1 
            : 0;
        
        dashboard.Cards.Add(card);
        await SaveDashboardAsync(dashboard);
        
        _logger.LogInformation(
            "Added card {CardId} (feature: {FeatureId}) to dashboard for vehicle {VehicleId}",
            card.Id, card.FeatureId, vehicleId);
        
        return card;
    }

    public async Task UpdateCardAsync(Guid userId, Guid vehicleId, CardConfiguration card)
    {
        var dashboard = await GetDashboardAsync(userId, vehicleId);
        
        var existingIndex = dashboard.Cards.FindIndex(c => c.Id == card.Id);
        if (existingIndex < 0)
        {
            throw new ArgumentException($"Card not found: {card.Id}");
        }
        
        // Validate the feature exists
        var feature = FeatureRegistry.GetFeature(card.FeatureId);
        if (feature is null)
        {
            throw new ArgumentException($"Unknown feature: {card.FeatureId}");
        }
        
        // Validate metrics exist
        ValidateCardMetrics(card, feature);
        
        // Preserve order if not specified
        if (card.Order == 0)
        {
            card.Order = dashboard.Cards[existingIndex].Order;
        }
        
        dashboard.Cards[existingIndex] = card;
        await SaveDashboardAsync(dashboard);
        
        _logger.LogInformation("Updated card {CardId} on dashboard for vehicle {VehicleId}", card.Id, vehicleId);
    }

    public async Task RemoveCardAsync(Guid userId, Guid vehicleId, Guid cardId)
    {
        var dashboard = await GetDashboardAsync(userId, vehicleId);
        
        var removed = dashboard.Cards.RemoveAll(c => c.Id == cardId);
        if (removed == 0)
        {
            throw new ArgumentException($"Card not found: {cardId}");
        }
        
        // Re-normalize order values
        var orderedCards = dashboard.Cards.OrderBy(c => c.Order).ToList();
        for (int i = 0; i < orderedCards.Count; i++)
        {
            orderedCards[i].Order = i;
        }
        dashboard.Cards = orderedCards;
        
        await SaveDashboardAsync(dashboard);
        
        _logger.LogInformation("Removed card {CardId} from dashboard for vehicle {VehicleId}", cardId, vehicleId);
    }

    public async Task ReorderCardsAsync(Guid userId, Guid vehicleId, IReadOnlyList<Guid> cardIdsInOrder)
    {
        var dashboard = await GetDashboardAsync(userId, vehicleId);
        
        // Create a lookup for current cards
        var cardLookup = dashboard.Cards.ToDictionary(c => c.Id);
        
        // Validate all IDs exist
        foreach (var id in cardIdsInOrder)
        {
            if (!cardLookup.ContainsKey(id))
            {
                throw new ArgumentException($"Card not found: {id}");
            }
        }
        
        // Reorder
        var reorderedCards = new List<CardConfiguration>();
        for (int i = 0; i < cardIdsInOrder.Count; i++)
        {
            var card = cardLookup[cardIdsInOrder[i]];
            card.Order = i;
            reorderedCards.Add(card);
        }
        
        // Add any cards not in the provided list (shouldn't happen, but be safe)
        var remainingCards = dashboard.Cards
            .Where(c => !cardIdsInOrder.Contains(c.Id))
            .OrderBy(c => c.Order);
        foreach (var card in remainingCards)
        {
            card.Order = reorderedCards.Count;
            reorderedCards.Add(card);
        }
        
        dashboard.Cards = reorderedCards;
        await SaveDashboardAsync(dashboard);
        
        _logger.LogInformation(
            "Reordered {CardCount} cards on dashboard for vehicle {VehicleId}",
            cardIdsInOrder.Count, vehicleId);
    }

    public async Task ResetToDefaultAsync(Guid userId, Guid vehicleId)
    {
        var defaultDashboard = FeatureRegistry.GetDefaultDashboard(userId, vehicleId);
        await SaveDashboardAsync(defaultDashboard);
        
        _logger.LogInformation("Reset dashboard to default for user {UserId}, vehicle {VehicleId}", userId, vehicleId);
    }

    public IReadOnlyList<FeatureDefinition> GetAvailableFeatures()
    {
        return FeatureRegistry.Features;
    }

    public IReadOnlyDictionary<FeatureCategory, IReadOnlyList<FeatureDefinition>> GetFeaturesByCategory()
    {
        return FeatureRegistry.Features
            .GroupBy(f => f.Category)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<FeatureDefinition>)g.OrderBy(f => f.SortOrder).ToList());
    }
    
    private static string GetDashboardKey(Guid userId, Guid vehicleId) =>
        $"dashboard:{userId}:{vehicleId}";
    
    private static void ValidateCardMetrics(CardConfiguration card, FeatureDefinition feature)
    {
        var metricIds = feature.AvailableMetrics.Select(m => m.Id).ToHashSet();
        
        if (!metricIds.Contains(card.PrimaryMetricId))
        {
            throw new ArgumentException($"Unknown primary metric: {card.PrimaryMetricId}");
        }
        
        var primaryMetric = feature.AvailableMetrics.First(m => m.Id == card.PrimaryMetricId);
        if (!primaryMetric.CanBePrimary)
        {
            throw new ArgumentException($"Metric {card.PrimaryMetricId} cannot be used as primary");
        }
        
        foreach (var secondaryId in card.SecondaryMetricIds)
        {
            if (!metricIds.Contains(secondaryId))
            {
                throw new ArgumentException($"Unknown secondary metric: {secondaryId}");
            }
        }
        
        if (card.SecondaryMetricIds.Count > card.MaxSecondaryMetrics)
        {
            throw new ArgumentException(
                $"Card size {card.Size} allows max {card.MaxSecondaryMetrics} secondary metrics, " +
                $"but {card.SecondaryMetricIds.Count} were provided");
        }
    }
}
