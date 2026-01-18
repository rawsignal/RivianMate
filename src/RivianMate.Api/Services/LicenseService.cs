using Microsoft.EntityFrameworkCore;
using RivianMate.Core.Licensing;
using RivianMate.Infrastructure.Data;

namespace RivianMate.Api.Services;

/// <summary>
/// Service for checking edition and enforcing limits.
/// Edition is determined at compile time - Pro code doesn't exist in SelfHosted builds.
/// </summary>
public class LicenseService
{
    private readonly RivianMateDbContext _db;
    private readonly ILogger<LicenseService> _logger;
    private readonly LicenseInfo _license;

    public LicenseService(
        RivianMateDbContext db,
        ILogger<LicenseService> logger)
    {
        _db = db;
        _logger = logger;

        // Edition is determined at compile time
        _license = BuildInfo.IsPro ? LicenseInfo.Pro() : LicenseInfo.SelfHosted();
        _logger.LogInformation("Running as {Edition} edition ({DisplayName})",
            _license.Edition, BuildInfo.DisplayName);
    }

    /// <summary>
    /// Get the current license information.
    /// </summary>
    public LicenseInfo GetLicense() => _license;

    /// <summary>
    /// Get the current edition.
    /// </summary>
    public Edition GetEdition() => _license.Edition;

    /// <summary>
    /// Check if running as Pro edition.
    /// </summary>
    public bool IsPro => _license.IsPro;

    /// <summary>
    /// Check if running as self-hosted edition.
    /// </summary>
    public bool IsSelfHosted => _license.IsSelfHosted;

    /// <summary>
    /// Check if a feature is enabled.
    /// </summary>
    public bool IsFeatureEnabled(string feature)
    {
        return _license.HasFeature(feature);
    }

    /// <summary>
    /// Check if a new user can be registered.
    /// </summary>
    public async Task<bool> CanAddUserAsync(CancellationToken cancellationToken = default)
    {
        var currentUsers = await _db.Users.CountAsync(cancellationToken);
        return currentUsers < _license.MaxUsers;
    }

    /// <summary>
    /// Check if a user can add another vehicle.
    /// </summary>
    public async Task<bool> CanAddVehicleAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var currentVehicles = await _db.Vehicles.CountAsync(v => v.OwnerId == userId, cancellationToken);
        return currentVehicles < _license.MaxVehiclesPerUser;
    }

    /// <summary>
    /// Check if a user can link another Rivian account.
    /// </summary>
    public async Task<bool> CanAddRivianAccountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var currentAccounts = await _db.RivianAccounts.CountAsync(a => a.UserId == userId, cancellationToken);
        return currentAccounts < _license.MaxRivianAccountsPerUser;
    }

    /// <summary>
    /// Get usage statistics for the current installation.
    /// </summary>
    public async Task<UsageStats> GetUsageStatsAsync(CancellationToken cancellationToken = default)
    {
        return new UsageStats
        {
            UserCount = await _db.Users.CountAsync(cancellationToken),
            MaxUsers = _license.MaxUsers,
            Edition = _license.Edition
        };
    }
}

/// <summary>
/// Current usage statistics.
/// </summary>
public class UsageStats
{
    public int UserCount { get; init; }
    public int MaxUsers { get; init; }
    public Edition Edition { get; init; }
    public bool AtUserLimit => UserCount >= MaxUsers;
}
