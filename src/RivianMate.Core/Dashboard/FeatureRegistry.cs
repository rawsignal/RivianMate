namespace RivianMate.Core.Dashboard;

/// <summary>
/// Registry of all available features and their metric definitions
/// </summary>
public static class FeatureRegistry
{
    private static readonly List<FeatureDefinition> _features = new();
    
    /// <summary>
    /// All registered features
    /// </summary>
    public static IReadOnlyList<FeatureDefinition> Features => _features.AsReadOnly();
    
    /// <summary>
    /// Get a feature by ID
    /// </summary>
    public static FeatureDefinition? GetFeature(string id) => 
        _features.FirstOrDefault(f => f.Id == id);
    
    /// <summary>
    /// Get features by category
    /// </summary>
    public static IEnumerable<FeatureDefinition> GetByCategory(FeatureCategory category) =>
        _features.Where(f => f.Category == category).OrderBy(f => f.SortOrder);
    
    /// <summary>
    /// Initialize the registry with built-in features
    /// </summary>
    static FeatureRegistry()
    {
        RegisterBuiltInFeatures();
    }
    
    private static void RegisterBuiltInFeatures()
    {
        // Battery Health Feature
        _features.Add(new FeatureDefinition
        {
            Id = "battery-health",
            Name = "Battery Health",
            Description = "Track battery degradation, capacity trends, and warranty status",
            Icon = "battery-full",
            DetailPageRoute = "/battery-health",
            Category = FeatureCategory.Battery,
            SortOrder = 10,
            AvailableMetrics = new List<MetricDefinition>
            {
                new()
                {
                    Id = "health-percent",
                    Name = "Health Percentage",
                    ShortName = "Health",
                    Unit = "%",
                    DisplayType = MetricDisplayType.Progress,
                    CanBePrimary = true,
                    MaxDecimalPlaces = 1,
                    Thresholds = HealthThresholds
                },
                new()
                {
                    Id = "current-capacity",
                    Name = "Current Capacity",
                    ShortName = "Capacity",
                    Unit = "kWh",
                    DisplayType = MetricDisplayType.Number,
                    CanBePrimary = true,
                    MaxDecimalPlaces = 1
                },
                new()
                {
                    Id = "original-capacity",
                    Name = "Original Capacity",
                    ShortName = "Original",
                    Unit = "kWh",
                    DisplayType = MetricDisplayType.Number,
                    CanBePrimary = false,
                    MaxDecimalPlaces = 1
                },
                new()
                {
                    Id = "capacity-lost",
                    Name = "Capacity Lost",
                    ShortName = "Lost",
                    Unit = "kWh",
                    DisplayType = MetricDisplayType.Number,
                    CanBePrimary = false,
                    MaxDecimalPlaces = 1
                },
                new()
                {
                    Id = "degradation-rate",
                    Name = "Degradation Rate",
                    ShortName = "Rate",
                    Unit = "% / 10k mi",
                    DisplayType = MetricDisplayType.Number,
                    CanBePrimary = false,
                    MaxDecimalPlaces = 2
                },
                new()
                {
                    Id = "health-trend",
                    Name = "Health Trend",
                    ShortName = "Trend",
                    DisplayType = MetricDisplayType.Sparkline,
                    CanBePrimary = false
                },
                new()
                {
                    Id = "warranty-status",
                    Name = "Warranty Status",
                    ShortName = "Warranty",
                    DisplayType = MetricDisplayType.Status,
                    CanBePrimary = false
                },
                new()
                {
                    Id = "projected-100k",
                    Name = "Projected at 100k mi",
                    ShortName = "@ 100k",
                    Unit = "%",
                    DisplayType = MetricDisplayType.Number,
                    CanBePrimary = false,
                    MaxDecimalPlaces = 1
                }
            },
            DefaultCardConfig = new CardConfiguration
            {
                FeatureId = "battery-health",
                PrimaryMetricId = "health-percent",
                SecondaryMetricIds = new[] { "current-capacity", "degradation-rate" },
                Size = CardSize.Medium
            }
        });
        
        // State of Charge Feature
        _features.Add(new FeatureDefinition
        {
            Id = "state-of-charge",
            Name = "State of Charge",
            Description = "Current battery level, range, and charging status",
            Icon = "battery-charging",
            DetailPageRoute = "/charging",
            Category = FeatureCategory.Battery,
            SortOrder = 5,
            AvailableMetrics = new List<MetricDefinition>
            {
                new()
                {
                    Id = "soc-percent",
                    Name = "State of Charge",
                    ShortName = "Charge",
                    Unit = "%",
                    DisplayType = MetricDisplayType.Progress,
                    CanBePrimary = true,
                    MaxDecimalPlaces = 0,
                    Thresholds = SocThresholds
                },
                new()
                {
                    Id = "range-miles",
                    Name = "Estimated Range",
                    ShortName = "Range",
                    Unit = "mi",
                    DisplayType = MetricDisplayType.Number,
                    CanBePrimary = true,
                    MaxDecimalPlaces = 0
                },
                new()
                {
                    Id = "charging-status",
                    Name = "Charging Status",
                    ShortName = "Status",
                    DisplayType = MetricDisplayType.Status,
                    CanBePrimary = false
                },
                new()
                {
                    Id = "charge-limit",
                    Name = "Charge Limit",
                    ShortName = "Limit",
                    Unit = "%",
                    DisplayType = MetricDisplayType.Number,
                    CanBePrimary = false,
                    MaxDecimalPlaces = 0
                },
                new()
                {
                    Id = "time-to-full",
                    Name = "Time to Full",
                    ShortName = "Time",
                    DisplayType = MetricDisplayType.Duration,
                    CanBePrimary = false
                },
                new()
                {
                    Id = "charge-rate",
                    Name = "Charge Rate",
                    ShortName = "Rate",
                    Unit = "kW",
                    DisplayType = MetricDisplayType.Number,
                    CanBePrimary = false,
                    MaxDecimalPlaces = 1
                }
            },
            DefaultCardConfig = new CardConfiguration
            {
                FeatureId = "state-of-charge",
                PrimaryMetricId = "soc-percent",
                SecondaryMetricIds = new[] { "range-miles", "charging-status" },
                Size = CardSize.Medium
            }
        });
        
        // Charging Sessions Feature
        _features.Add(new FeatureDefinition
        {
            Id = "charging-sessions",
            Name = "Charging Sessions",
            Description = "Charging history, costs, and statistics",
            Icon = "zap",
            DetailPageRoute = "/charging/sessions",
            Category = FeatureCategory.Charging,
            SortOrder = 10,
            AvailableMetrics = new List<MetricDefinition>
            {
                new()
                {
                    Id = "total-energy-month",
                    Name = "Energy This Month",
                    ShortName = "This Month",
                    Unit = "kWh",
                    DisplayType = MetricDisplayType.Number,
                    CanBePrimary = true,
                    MaxDecimalPlaces = 1
                },
                new()
                {
                    Id = "session-count-month",
                    Name = "Sessions This Month",
                    ShortName = "Sessions",
                    DisplayType = MetricDisplayType.Number,
                    CanBePrimary = true,
                    MaxDecimalPlaces = 0
                },
                new()
                {
                    Id = "last-session-energy",
                    Name = "Last Session Energy",
                    ShortName = "Last",
                    Unit = "kWh",
                    DisplayType = MetricDisplayType.Number,
                    CanBePrimary = false,
                    MaxDecimalPlaces = 1
                },
                new()
                {
                    Id = "avg-session-energy",
                    Name = "Avg Session Energy",
                    ShortName = "Avg",
                    Unit = "kWh",
                    DisplayType = MetricDisplayType.Number,
                    CanBePrimary = false,
                    MaxDecimalPlaces = 1
                },
                new()
                {
                    Id = "home-vs-public",
                    Name = "Home vs Public",
                    ShortName = "Split",
                    DisplayType = MetricDisplayType.Status,
                    CanBePrimary = false
                },
                new()
                {
                    Id = "charging-cost-month",
                    Name = "Cost This Month",
                    ShortName = "Cost",
                    DisplayType = MetricDisplayType.Currency,
                    CanBePrimary = true,
                    MaxDecimalPlaces = 2
                }
            },
            DefaultCardConfig = new CardConfiguration
            {
                FeatureId = "charging-sessions",
                PrimaryMetricId = "total-energy-month",
                SecondaryMetricIds = new[] { "session-count-month", "last-session-energy" },
                Size = CardSize.Medium
            },
            IsProFeature = false  // Basic stats free, detailed history Pro
        });
        
        // Drives Feature
        _features.Add(new FeatureDefinition
        {
            Id = "drives",
            Name = "Drives",
            Description = "Trip history, routes, and efficiency tracking",
            Icon = "map",
            DetailPageRoute = "/drives",
            Category = FeatureCategory.Driving,
            SortOrder = 10,
            AvailableMetrics = new List<MetricDefinition>
            {
                new()
                {
                    Id = "total-miles-month",
                    Name = "Miles This Month",
                    ShortName = "This Month",
                    Unit = "mi",
                    DisplayType = MetricDisplayType.Number,
                    CanBePrimary = true,
                    MaxDecimalPlaces = 0
                },
                new()
                {
                    Id = "drive-count-month",
                    Name = "Drives This Month",
                    ShortName = "Drives",
                    DisplayType = MetricDisplayType.Number,
                    CanBePrimary = false,
                    MaxDecimalPlaces = 0
                },
                new()
                {
                    Id = "last-drive-distance",
                    Name = "Last Drive Distance",
                    ShortName = "Last",
                    Unit = "mi",
                    DisplayType = MetricDisplayType.Number,
                    CanBePrimary = false,
                    MaxDecimalPlaces = 1
                },
                new()
                {
                    Id = "last-drive-efficiency",
                    Name = "Last Drive Efficiency",
                    ShortName = "Efficiency",
                    Unit = "mi/kWh",
                    DisplayType = MetricDisplayType.Number,
                    CanBePrimary = false,
                    MaxDecimalPlaces = 2
                },
                new()
                {
                    Id = "odometer",
                    Name = "Odometer",
                    ShortName = "Total",
                    Unit = "mi",
                    DisplayType = MetricDisplayType.Number,
                    CanBePrimary = true,
                    MaxDecimalPlaces = 0
                }
            },
            DefaultCardConfig = new CardConfiguration
            {
                FeatureId = "drives",
                PrimaryMetricId = "total-miles-month",
                SecondaryMetricIds = new[] { "drive-count-month", "last-drive-efficiency" },
                Size = CardSize.Medium
            },
            IsProFeature = false  // Basic stats free, lifetime history Pro
        });
        
        // Efficiency Feature
        _features.Add(new FeatureDefinition
        {
            Id = "efficiency",
            Name = "Efficiency",
            Description = "Energy consumption analysis and trends",
            Icon = "gauge",
            DetailPageRoute = "/efficiency",
            Category = FeatureCategory.Driving,
            SortOrder = 20,
            AvailableMetrics = new List<MetricDefinition>
            {
                new()
                {
                    Id = "avg-efficiency",
                    Name = "Average Efficiency",
                    ShortName = "Avg",
                    Unit = "mi/kWh",
                    DisplayType = MetricDisplayType.Number,
                    CanBePrimary = true,
                    MaxDecimalPlaces = 2
                },
                new()
                {
                    Id = "efficiency-trend",
                    Name = "Efficiency Trend",
                    ShortName = "Trend",
                    DisplayType = MetricDisplayType.Sparkline,
                    CanBePrimary = false
                },
                new()
                {
                    Id = "best-efficiency",
                    Name = "Best Efficiency",
                    ShortName = "Best",
                    Unit = "mi/kWh",
                    DisplayType = MetricDisplayType.Number,
                    CanBePrimary = false,
                    MaxDecimalPlaces = 2
                },
                new()
                {
                    Id = "energy-used-month",
                    Name = "Energy Used This Month",
                    ShortName = "Used",
                    Unit = "kWh",
                    DisplayType = MetricDisplayType.Number,
                    CanBePrimary = false,
                    MaxDecimalPlaces = 1
                }
            },
            DefaultCardConfig = new CardConfiguration
            {
                FeatureId = "efficiency",
                PrimaryMetricId = "avg-efficiency",
                SecondaryMetricIds = new[] { "efficiency-trend" },
                Size = CardSize.Medium
            }
        });
        
        // Vehicle Status Feature
        _features.Add(new FeatureDefinition
        {
            Id = "vehicle-status",
            Name = "Vehicle Status",
            Description = "Doors, windows, locks, and security",
            Icon = "car",
            DetailPageRoute = "/vehicle",
            Category = FeatureCategory.Vehicle,
            SortOrder = 10,
            AvailableMetrics = new List<MetricDefinition>
            {
                new()
                {
                    Id = "lock-status",
                    Name = "Lock Status",
                    ShortName = "Locks",
                    DisplayType = MetricDisplayType.Status,
                    CanBePrimary = true
                },
                new()
                {
                    Id = "doors-status",
                    Name = "Doors",
                    ShortName = "Doors",
                    DisplayType = MetricDisplayType.Status,
                    CanBePrimary = false
                },
                new()
                {
                    Id = "windows-status",
                    Name = "Windows",
                    ShortName = "Windows",
                    DisplayType = MetricDisplayType.Status,
                    CanBePrimary = false
                },
                new()
                {
                    Id = "frunk-status",
                    Name = "Frunk",
                    ShortName = "Frunk",
                    DisplayType = MetricDisplayType.Status,
                    CanBePrimary = false
                },
                new()
                {
                    Id = "gear-guard-status",
                    Name = "Gear Guard",
                    ShortName = "Gear Guard",
                    DisplayType = MetricDisplayType.Status,
                    CanBePrimary = false
                },
                new()
                {
                    Id = "open-items-count",
                    Name = "Open Items",
                    ShortName = "Open",
                    DisplayType = MetricDisplayType.Number,
                    CanBePrimary = true,
                    MaxDecimalPlaces = 0
                }
            },
            DefaultCardConfig = new CardConfiguration
            {
                FeatureId = "vehicle-status",
                PrimaryMetricId = "lock-status",
                SecondaryMetricIds = new[] { "doors-status", "gear-guard-status" },
                Size = CardSize.Medium
            }
        });
        
        // Climate Feature
        _features.Add(new FeatureDefinition
        {
            Id = "climate",
            Name = "Climate",
            Description = "Cabin temperature and preconditioning",
            Icon = "thermometer",
            DetailPageRoute = "/climate",
            Category = FeatureCategory.Climate,
            SortOrder = 10,
            AvailableMetrics = new List<MetricDefinition>
            {
                new()
                {
                    Id = "cabin-temp",
                    Name = "Cabin Temperature",
                    ShortName = "Cabin",
                    Unit = "°F",
                    DisplayType = MetricDisplayType.Temperature,
                    CanBePrimary = true,
                    MaxDecimalPlaces = 0
                },
                new()
                {
                    Id = "exterior-temp",
                    Name = "Exterior Temperature",
                    ShortName = "Outside",
                    Unit = "°F",
                    DisplayType = MetricDisplayType.Temperature,
                    CanBePrimary = false,
                    MaxDecimalPlaces = 0
                },
                new()
                {
                    Id = "climate-status",
                    Name = "Climate Status",
                    ShortName = "Status",
                    DisplayType = MetricDisplayType.Status,
                    CanBePrimary = false
                },
                new()
                {
                    Id = "climate-target",
                    Name = "Target Temperature",
                    ShortName = "Target",
                    Unit = "°F",
                    DisplayType = MetricDisplayType.Temperature,
                    CanBePrimary = false,
                    MaxDecimalPlaces = 0
                }
            },
            DefaultCardConfig = new CardConfiguration
            {
                FeatureId = "climate",
                PrimaryMetricId = "cabin-temp",
                SecondaryMetricIds = new[] { "climate-status" },
                Size = CardSize.Small
            }
        });
        
        // Location Feature
        _features.Add(new FeatureDefinition
        {
            Id = "location",
            Name = "Location",
            Description = "Current position and location history",
            Icon = "map-pin",
            DetailPageRoute = "/location",
            Category = FeatureCategory.Location,
            SortOrder = 10,
            AvailableMetrics = new List<MetricDefinition>
            {
                new()
                {
                    Id = "current-address",
                    Name = "Current Location",
                    ShortName = "Location",
                    DisplayType = MetricDisplayType.Status,
                    CanBePrimary = true
                },
                new()
                {
                    Id = "last-moved",
                    Name = "Last Moved",
                    ShortName = "Moved",
                    DisplayType = MetricDisplayType.DateTime,
                    CanBePrimary = false
                },
                new()
                {
                    Id = "distance-from-home",
                    Name = "Distance from Home",
                    ShortName = "From Home",
                    Unit = "mi",
                    DisplayType = MetricDisplayType.Number,
                    CanBePrimary = false,
                    MaxDecimalPlaces = 1
                }
            },
            DefaultCardConfig = new CardConfiguration
            {
                FeatureId = "location",
                PrimaryMetricId = "current-address",
                SecondaryMetricIds = new[] { "last-moved" },
                Size = CardSize.Medium
            }
        });
        
        // Software Feature
        _features.Add(new FeatureDefinition
        {
            Id = "software",
            Name = "Software",
            Description = "OTA updates and version history",
            Icon = "download",
            DetailPageRoute = "/software",
            Category = FeatureCategory.System,
            SortOrder = 10,
            AvailableMetrics = new List<MetricDefinition>
            {
                new()
                {
                    Id = "software-version",
                    Name = "Software Version",
                    ShortName = "Version",
                    DisplayType = MetricDisplayType.Status,
                    CanBePrimary = true
                },
                new()
                {
                    Id = "update-available",
                    Name = "Update Available",
                    ShortName = "Update",
                    DisplayType = MetricDisplayType.Status,
                    CanBePrimary = false
                },
                new()
                {
                    Id = "last-updated",
                    Name = "Last Updated",
                    ShortName = "Updated",
                    DisplayType = MetricDisplayType.DateTime,
                    CanBePrimary = false
                }
            },
            DefaultCardConfig = new CardConfiguration
            {
                FeatureId = "software",
                PrimaryMetricId = "software-version",
                SecondaryMetricIds = new[] { "update-available" },
                Size = CardSize.Small
            }
        });
        
        // Tires Feature
        _features.Add(new FeatureDefinition
        {
            Id = "tires",
            Name = "Tire Pressure",
            Description = "Tire pressure monitoring",
            Icon = "circle",
            DetailPageRoute = "/vehicle/tires",
            Category = FeatureCategory.Vehicle,
            SortOrder = 20,
            AvailableMetrics = new List<MetricDefinition>
            {
                new()
                {
                    Id = "tire-status",
                    Name = "Tire Status",
                    ShortName = "Status",
                    DisplayType = MetricDisplayType.Status,
                    CanBePrimary = true
                },
                new()
                {
                    Id = "tire-fl",
                    Name = "Front Left",
                    ShortName = "FL",
                    Unit = "PSI",
                    DisplayType = MetricDisplayType.Number,
                    CanBePrimary = false,
                    MaxDecimalPlaces = 0
                },
                new()
                {
                    Id = "tire-fr",
                    Name = "Front Right",
                    ShortName = "FR",
                    Unit = "PSI",
                    DisplayType = MetricDisplayType.Number,
                    CanBePrimary = false,
                    MaxDecimalPlaces = 0
                },
                new()
                {
                    Id = "tire-rl",
                    Name = "Rear Left",
                    ShortName = "RL",
                    Unit = "PSI",
                    DisplayType = MetricDisplayType.Number,
                    CanBePrimary = false,
                    MaxDecimalPlaces = 0
                },
                new()
                {
                    Id = "tire-rr",
                    Name = "Rear Right",
                    ShortName = "RR",
                    Unit = "PSI",
                    DisplayType = MetricDisplayType.Number,
                    CanBePrimary = false,
                    MaxDecimalPlaces = 0
                }
            },
            DefaultCardConfig = new CardConfiguration
            {
                FeatureId = "tires",
                PrimaryMetricId = "tire-status",
                SecondaryMetricIds = Array.Empty<string>(),
                Size = CardSize.Small
            }
        });
    }
    
    // Shared threshold definitions
    private static readonly List<MetricThreshold> HealthThresholds = new()
    {
        new() { MinValue = 95, Color = "health-excellent", Label = "Excellent" },
        new() { MinValue = 90, MaxValue = 95, Color = "health-good", Label = "Very Good" },
        new() { MinValue = 85, MaxValue = 90, Color = "health-good", Label = "Good" },
        new() { MinValue = 80, MaxValue = 85, Color = "health-fair", Label = "Fair" },
        new() { MinValue = 70, MaxValue = 80, Color = "health-warning", Label = "Warning" },
        new() { MinValue = 0, MaxValue = 70, Color = "health-poor", Label = "Poor" }
    };
    
    private static readonly List<MetricThreshold> SocThresholds = new()
    {
        new() { MinValue = 80, Color = "soc-high", Label = "High" },
        new() { MinValue = 40, MaxValue = 80, Color = "soc-normal", Label = "Normal" },
        new() { MinValue = 20, MaxValue = 40, Color = "soc-low", Label = "Low" },
        new() { MinValue = 0, MaxValue = 20, Color = "soc-critical", Label = "Critical" }
    };
    
    /// <summary>
    /// Get the default dashboard configuration for a new user
    /// </summary>
    public static DashboardConfiguration GetDefaultDashboard(Guid userId, Guid vehicleId)
    {
        return new DashboardConfiguration
        {
            UserId = userId,
            VehicleId = vehicleId,
            Cards = new List<CardConfiguration>
            {
                new()
                {
                    FeatureId = "state-of-charge",
                    PrimaryMetricId = "soc-percent",
                    SecondaryMetricIds = new[] { "range-miles", "charging-status" },
                    Size = CardSize.Medium,
                    Order = 0
                },
                new()
                {
                    FeatureId = "battery-health",
                    PrimaryMetricId = "health-percent",
                    SecondaryMetricIds = new[] { "current-capacity", "degradation-rate" },
                    Size = CardSize.Medium,
                    Order = 1
                },
                new()
                {
                    FeatureId = "efficiency",
                    PrimaryMetricId = "avg-efficiency",
                    SecondaryMetricIds = new[] { "efficiency-trend" },
                    Size = CardSize.Medium,
                    Order = 2
                },
                new()
                {
                    FeatureId = "vehicle-status",
                    PrimaryMetricId = "lock-status",
                    SecondaryMetricIds = new[] { "doors-status" },
                    Size = CardSize.Small,
                    Order = 3
                },
                new()
                {
                    FeatureId = "climate",
                    PrimaryMetricId = "cabin-temp",
                    SecondaryMetricIds = new[] { "climate-status" },
                    Size = CardSize.Small,
                    Order = 4
                },
                new()
                {
                    FeatureId = "drives",
                    PrimaryMetricId = "odometer",
                    SecondaryMetricIds = new[] { "total-miles-month" },
                    Size = CardSize.Small,
                    Order = 5
                },
                new()
                {
                    FeatureId = "software",
                    PrimaryMetricId = "software-version",
                    SecondaryMetricIds = Array.Empty<string>(),
                    Size = CardSize.Small,
                    Order = 6
                }
            }
        };
    }
}
