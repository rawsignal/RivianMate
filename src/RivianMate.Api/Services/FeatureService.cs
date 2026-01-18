using RivianMate.Core.Licensing;

namespace RivianMate.Api.Services;

/// <summary>
/// Simplified service for checking feature availability.
/// Wraps LicenseService with a cleaner API for use in components.
/// </summary>
public class FeatureService
{
    private readonly LicenseService _licenseService;

    public FeatureService(LicenseService licenseService)
    {
        _licenseService = licenseService;
    }

    /// <summary>
    /// Check if a feature is enabled.
    /// </summary>
    public Task<bool> IsEnabledAsync(string feature, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_licenseService.IsFeatureEnabled(feature));
    }

    /// <summary>
    /// Check if a feature is enabled (sync version).
    /// </summary>
    public bool IsEnabled(string feature)
    {
        return _licenseService.IsFeatureEnabled(feature);
    }

    /// <summary>
    /// Get the current license info.
    /// </summary>
    public LicenseInfo GetLicense()
    {
        return _licenseService.GetLicense();
    }

    /// <summary>
    /// Get the current edition.
    /// </summary>
    public Edition GetEdition()
    {
        return _licenseService.GetEdition();
    }

    /// <summary>
    /// Check if running as cloud edition.
    /// </summary>
    public bool IsCloud => _licenseService.IsCloud;

    /// <summary>
    /// Check if running as self-hosted edition.
    /// </summary>
    public bool IsSelfHosted => _licenseService.IsSelfHosted;

    /// <summary>
    /// Check if a new user can be registered.
    /// </summary>
    public Task<bool> CanRegisterUserAsync(CancellationToken cancellationToken = default)
    {
        return _licenseService.CanAddUserAsync(cancellationToken);
    }

    /// <summary>
    /// Check if a user can add another vehicle.
    /// </summary>
    public Task<bool> CanAddVehicleAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _licenseService.CanAddVehicleAsync(userId, cancellationToken);
    }

    /// <summary>
    /// Check if a user can link another Rivian account.
    /// </summary>
    public Task<bool> CanLinkRivianAccountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _licenseService.CanAddRivianAccountAsync(userId, cancellationToken);
    }

    // === Convenience Methods for Common Features ===

    public bool HasBatteryCareTips => IsEnabled(Features.BatteryCareTips);
    public bool HasAdvancedAnalytics => IsEnabled(Features.AdvancedAnalytics);
    public bool HasDriveHistory => IsEnabled(Features.DriveHistory);
    public bool HasExportData => IsEnabled(Features.ExportData);
    public bool HasNotifications => IsEnabled(Features.Notifications);
    public bool HasApiAccess => IsEnabled(Features.ApiAccess);
}
