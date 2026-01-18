namespace RivianMate.Core.Dashboard;

/// <summary>
/// Configuration for a single dashboard card
/// </summary>
public class CardConfiguration
{
    /// <summary>
    /// Unique identifier for this card instance
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();
    
    /// <summary>
    /// The feature this card displays data from
    /// </summary>
    public required string FeatureId { get; init; }
    
    /// <summary>
    /// The primary (hero) metric to display prominently
    /// </summary>
    public required string PrimaryMetricId { get; init; }
    
    /// <summary>
    /// Secondary metrics to display (max depends on card size)
    /// </summary>
    public IReadOnlyList<string> SecondaryMetricIds { get; init; } = Array.Empty<string>();
    
    /// <summary>
    /// Card size variant
    /// </summary>
    public CardSize Size { get; init; } = CardSize.Medium;
    
    /// <summary>
    /// Optional custom title (uses feature name if null)
    /// </summary>
    public string? TitleOverride { get; init; }
    
    /// <summary>
    /// Position in the dashboard grid (0-based)
    /// </summary>
    public int Order { get; set; }
    
    /// <summary>
    /// Whether the card is currently visible
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// Maximum secondary metrics allowed for this card's size
    /// </summary>
    public int MaxSecondaryMetrics => Size switch
    {
        CardSize.Small => 0,
        CardSize.Medium => 2,
        CardSize.Large => 3,
        _ => 2
    };
    
    /// <summary>
    /// Grid column span for this card's size
    /// </summary>
    public int ColumnSpan => Size switch
    {
        CardSize.Small => 1,
        CardSize.Medium => 1,
        CardSize.Large => 2,
        _ => 1
    };
}

/// <summary>
/// Card size variants
/// </summary>
public enum CardSize
{
    /// <summary>
    /// Compact card - primary metric only, 1 column
    /// </summary>
    Small,
    
    /// <summary>
    /// Standard card - primary + 1-2 secondary metrics, 1 column
    /// </summary>
    Medium,
    
    /// <summary>
    /// Expanded card - primary + 2-3 secondary, sparklines, 2 columns
    /// </summary>
    Large
}

/// <summary>
/// Complete dashboard configuration for a user+vehicle combination
/// </summary>
public class DashboardConfiguration
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();
    
    /// <summary>
    /// User who owns this configuration
    /// </summary>
    public Guid UserId { get; init; }
    
    /// <summary>
    /// Vehicle this dashboard is for (dashboards are per-vehicle)
    /// </summary>
    public Guid VehicleId { get; init; }
    
    /// <summary>
    /// Ordered list of card configurations
    /// </summary>
    public List<CardConfiguration> Cards { get; set; } = new();
    
    /// <summary>
    /// When this configuration was created
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// When this configuration was last modified
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Schema version for migration purposes
    /// </summary>
    public int SchemaVersion { get; init; } = 1;
}

/// <summary>
/// Represents the current value of a metric for display
/// </summary>
public class MetricValue
{
    /// <summary>
    /// The metric definition this value is for
    /// </summary>
    public required string MetricId { get; init; }
    
    /// <summary>
    /// The current value (numeric)
    /// </summary>
    public double? NumericValue { get; init; }
    
    /// <summary>
    /// The current value (string, for Status type)
    /// </summary>
    public string? StringValue { get; init; }
    
    /// <summary>
    /// Historical values for sparkline display
    /// </summary>
    public IReadOnlyList<double>? SparklineData { get; init; }
    
    /// <summary>
    /// When this value was last updated
    /// </summary>
    public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// Whether the data is stale (vehicle offline, etc.)
    /// </summary>
    public bool IsStale { get; init; }
    
    /// <summary>
    /// Optional trend indicator
    /// </summary>
    public MetricTrend? Trend { get; init; }
    
    /// <summary>
    /// Resolved color based on thresholds
    /// </summary>
    public string? Color { get; init; }
    
    /// <summary>
    /// Resolved status label based on thresholds
    /// </summary>
    public string? StatusLabel { get; init; }
}

/// <summary>
/// Trend direction for a metric
/// </summary>
public enum MetricTrend
{
    Stable,
    Increasing,
    Decreasing
}

/// <summary>
/// Complete data needed to render a card
/// </summary>
public class CardRenderData
{
    /// <summary>
    /// The card configuration
    /// </summary>
    public required CardConfiguration Config { get; init; }
    
    /// <summary>
    /// The feature definition
    /// </summary>
    public required FeatureDefinition Feature { get; init; }
    
    /// <summary>
    /// The primary metric definition
    /// </summary>
    public required MetricDefinition PrimaryMetric { get; init; }
    
    /// <summary>
    /// The primary metric's current value
    /// </summary>
    public required MetricValue PrimaryValue { get; init; }
    
    /// <summary>
    /// Secondary metric definitions (in order)
    /// </summary>
    public IReadOnlyList<MetricDefinition> SecondaryMetrics { get; init; } = Array.Empty<MetricDefinition>();
    
    /// <summary>
    /// Secondary metric values (matching order of SecondaryMetrics)
    /// </summary>
    public IReadOnlyList<MetricValue> SecondaryValues { get; init; } = Array.Empty<MetricValue>();
    
    /// <summary>
    /// Resolved title (custom or feature name)
    /// </summary>
    public string Title => Config.TitleOverride ?? Feature.Name;
    
    /// <summary>
    /// Whether the card is in a loading state
    /// </summary>
    public bool IsLoading { get; init; }
    
    /// <summary>
    /// Error message if data fetch failed
    /// </summary>
    public string? ErrorMessage { get; init; }
}
