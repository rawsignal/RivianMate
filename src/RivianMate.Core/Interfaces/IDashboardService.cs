using RivianMate.Core.Dashboard;

namespace RivianMate.Core.Interfaces;

/// <summary>
/// Service for managing user dashboard configurations
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// Get the dashboard configuration for a user and vehicle
    /// </summary>
    Task<DashboardConfiguration> GetDashboardAsync(Guid userId, Guid vehicleId);
    
    /// <summary>
    /// Save the dashboard configuration
    /// </summary>
    Task SaveDashboardAsync(DashboardConfiguration config);
    
    /// <summary>
    /// Add a card to the dashboard
    /// </summary>
    Task<CardConfiguration> AddCardAsync(Guid userId, Guid vehicleId, CardConfiguration card);
    
    /// <summary>
    /// Update a card's configuration
    /// </summary>
    Task UpdateCardAsync(Guid userId, Guid vehicleId, CardConfiguration card);
    
    /// <summary>
    /// Remove a card from the dashboard
    /// </summary>
    Task RemoveCardAsync(Guid userId, Guid vehicleId, Guid cardId);
    
    /// <summary>
    /// Reorder cards on the dashboard
    /// </summary>
    Task ReorderCardsAsync(Guid userId, Guid vehicleId, IReadOnlyList<Guid> cardIdsInOrder);
    
    /// <summary>
    /// Reset dashboard to default configuration
    /// </summary>
    Task ResetToDefaultAsync(Guid userId, Guid vehicleId);
    
    /// <summary>
    /// Get all available features for adding cards
    /// </summary>
    IReadOnlyList<FeatureDefinition> GetAvailableFeatures();
    
    /// <summary>
    /// Get features grouped by category
    /// </summary>
    IReadOnlyDictionary<FeatureCategory, IReadOnlyList<FeatureDefinition>> GetFeaturesByCategory();
}

/// <summary>
/// Service for providing metric values to cards
/// </summary>
public interface IMetricProvider
{
    /// <summary>
    /// Get the current value for a metric
    /// </summary>
    Task<MetricValue> GetMetricValueAsync(Guid vehicleId, string featureId, string metricId);
    
    /// <summary>
    /// Get multiple metric values at once (more efficient)
    /// </summary>
    Task<IReadOnlyDictionary<string, MetricValue>> GetMetricValuesAsync(
        Guid vehicleId, 
        string featureId, 
        IEnumerable<string> metricIds);
    
    /// <summary>
    /// Get all data needed to render a card
    /// </summary>
    Task<CardRenderData> GetCardRenderDataAsync(Guid vehicleId, CardConfiguration cardConfig);
    
    /// <summary>
    /// Get render data for all cards on a dashboard
    /// </summary>
    Task<IReadOnlyList<CardRenderData>> GetDashboardRenderDataAsync(
        Guid vehicleId, 
        DashboardConfiguration dashboardConfig);
}
