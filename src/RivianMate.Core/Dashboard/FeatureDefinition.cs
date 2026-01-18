namespace RivianMate.Core.Dashboard;

/// <summary>
/// Defines a feature module that can be displayed on the dashboard
/// </summary>
public class FeatureDefinition
{
    /// <summary>
    /// Unique identifier for the feature (e.g., "battery-health")
    /// </summary>
    public required string Id { get; init; }
    
    /// <summary>
    /// Display name (e.g., "Battery Health")
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// Brief description of what the feature tracks
    /// </summary>
    public required string Description { get; init; }
    
    /// <summary>
    /// Lucide icon name for the feature
    /// </summary>
    public required string Icon { get; init; }
    
    /// <summary>
    /// Route to the feature's detail page
    /// </summary>
    public required string DetailPageRoute { get; init; }
    
    /// <summary>
    /// Category for grouping in the "Add Card" UI
    /// </summary>
    public FeatureCategory Category { get; init; } = FeatureCategory.Vehicle;
    
    /// <summary>
    /// All metrics this feature can expose to cards
    /// </summary>
    public required IReadOnlyList<MetricDefinition> AvailableMetrics { get; init; }
    
    /// <summary>
    /// Default card configuration for new cards
    /// </summary>
    public required CardConfiguration DefaultCardConfig { get; init; }
    
    /// <summary>
    /// Whether this feature requires a Pro license
    /// </summary>
    public bool IsProFeature { get; init; } = false;
    
    /// <summary>
    /// Sort order within category
    /// </summary>
    public int SortOrder { get; init; } = 100;
}

/// <summary>
/// Categories for grouping features in the UI
/// </summary>
public enum FeatureCategory
{
    Battery,
    Charging,
    Driving,
    Vehicle,
    Climate,
    Location,
    System
}

/// <summary>
/// Defines a single metric that can be displayed on a card
/// </summary>
public class MetricDefinition
{
    /// <summary>
    /// Unique identifier within the feature (e.g., "health-percent")
    /// </summary>
    public required string Id { get; init; }
    
    /// <summary>
    /// Display name (e.g., "Health Percentage")
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// Short label for compact display (e.g., "Health")
    /// </summary>
    public string? ShortName { get; init; }
    
    /// <summary>
    /// Unit of measurement (e.g., "%", "kWh", "mi")
    /// </summary>
    public string? Unit { get; init; }
    
    /// <summary>
    /// How the metric should be rendered
    /// </summary>
    public MetricDisplayType DisplayType { get; init; } = MetricDisplayType.Number;
    
    /// <summary>
    /// Whether this metric can be used as the primary (hero) metric on a card
    /// </summary>
    public bool CanBePrimary { get; init; } = true;
    
    /// <summary>
    /// Optional icon to display with the metric
    /// </summary>
    public string? Icon { get; init; }
    
    /// <summary>
    /// Maximum decimal places for number display
    /// </summary>
    public int MaxDecimalPlaces { get; init; } = 0;
    
    /// <summary>
    /// For Progress type: the maximum value (default 100)
    /// </summary>
    public double ProgressMax { get; init; } = 100;
    
    /// <summary>
    /// Color coding rules for the metric value
    /// </summary>
    public IReadOnlyList<MetricThreshold>? Thresholds { get; init; }
}

/// <summary>
/// How a metric value should be displayed
/// </summary>
public enum MetricDisplayType
{
    /// <summary>Plain number with unit</summary>
    Number,
    
    /// <summary>Progress bar (0-100 or custom max)</summary>
    Progress,
    
    /// <summary>Mini trend chart</summary>
    Sparkline,
    
    /// <summary>Badge/pill with status text</summary>
    Status,
    
    /// <summary>Temperature with color coding</summary>
    Temperature,
    
    /// <summary>Time duration display</summary>
    Duration,
    
    /// <summary>Distance with unit</summary>
    Distance,
    
    /// <summary>Currency with formatting</summary>
    Currency,
    
    /// <summary>Percentage with optional color coding</summary>
    Percentage,
    
    /// <summary>Date/time display</summary>
    DateTime,
    
    /// <summary>Boolean on/off state</summary>
    Toggle
}

/// <summary>
/// Defines color thresholds for metric values
/// </summary>
public class MetricThreshold
{
    /// <summary>
    /// Minimum value for this threshold (inclusive)
    /// </summary>
    public double MinValue { get; init; }
    
    /// <summary>
    /// Maximum value for this threshold (exclusive, null = no max)
    /// </summary>
    public double? MaxValue { get; init; }
    
    /// <summary>
    /// CSS color or semantic color name
    /// </summary>
    public required string Color { get; init; }
    
    /// <summary>
    /// Optional label for this range (e.g., "Excellent", "Warning")
    /// </summary>
    public string? Label { get; init; }
}
