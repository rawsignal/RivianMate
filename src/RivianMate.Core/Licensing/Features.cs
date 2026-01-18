namespace RivianMate.Core.Licensing;

/// <summary>
/// Feature flags that can be gated by edition.
/// </summary>
public static class Features
{
    public const string Dashboard = "dashboard";
    public const string BatteryHealth = "battery_health";
    public const string VehicleState = "vehicle_state";
    public const string ChargingSessions = "charging_sessions";
    public const string BasicPolling = "basic_polling";
    public const string CustomDashboard = "custom_dashboard";
    public const string BatteryCareTips = "battery_care_tips";
    public const string AdvancedAnalytics = "advanced_analytics";
    public const string DriveHistory = "drive_history";
    public const string ExportData = "export_data";
    public const string Notifications = "notifications";
    public const string ApiAccess = "api_access";

    /// <summary>
    /// Features included in self-hosted edition.
    /// </summary>
    public static readonly HashSet<string> SelfHostedFeatures = new()
    {
        Dashboard,
        BatteryHealth,
        VehicleState,
        ChargingSessions,
        BasicPolling,
        CustomDashboard,
        BatteryCareTips,
        AdvancedAnalytics,
        DriveHistory,
        ExportData,
    };

    /// <summary>
    /// Features included in Pro edition (all features).
    /// </summary>
    public static readonly HashSet<string> ProFeatures = new(SelfHostedFeatures)
    {
        Notifications,
        ApiAccess,
    };

    /// <summary>
    /// Get features for an edition.
    /// </summary>
    public static HashSet<string> GetFeaturesForEdition(Edition edition) => edition switch
    {
        Edition.Pro => ProFeatures,
        _ => SelfHostedFeatures
    };
}
