using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RivianMate.Core.Entities;
using RivianMate.Infrastructure.Data;

namespace RivianMate.Api.Services;

/// <summary>
/// Service for managing user accounts and authentication-related operations.
/// </summary>
public class AccountService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RivianMateDbContext _db;
    private readonly ILogger<AccountService> _logger;

    public AccountService(
        UserManager<ApplicationUser> userManager,
        RivianMateDbContext db,
        ILogger<AccountService> logger)
    {
        _userManager = userManager;
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Get the current user from a ClaimsPrincipal.
    /// </summary>
    public async Task<ApplicationUser?> GetCurrentUserAsync(ClaimsPrincipal principal)
    {
        return await _userManager.GetUserAsync(principal);
    }

    /// <summary>
    /// Get the current user ID from a ClaimsPrincipal.
    /// </summary>
    public Guid? GetCurrentUserId(ClaimsPrincipal principal)
    {
        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }
        return null;
    }

    /// <summary>
    /// Update user's last login time.
    /// </summary>
    public async Task UpdateLastLoginAsync(ApplicationUser user)
    {
        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);
    }

    /// <summary>
    /// Update user's display name.
    /// </summary>
    public async Task<IdentityResult> UpdateDisplayNameAsync(ApplicationUser user, string displayName)
    {
        user.DisplayName = displayName;
        return await _userManager.UpdateAsync(user);
    }

    /// <summary>
    /// Delete user account and ALL associated data.
    /// This removes: Rivian accounts, vehicles, vehicle states, charging sessions,
    /// drives, positions, battery health snapshots, and dashboard configs.
    /// </summary>
    public async Task<IdentityResult> DeleteAccountAsync(ApplicationUser user)
    {
        _logger.LogInformation("Beginning full account deletion for user {UserId}", user.Id);

        // 1. Get all Rivian accounts for this user
        var rivianAccounts = await _db.RivianAccounts
            .Where(ra => ra.UserId == user.Id)
            .ToListAsync();

        // 2. Get all vehicle IDs for this user (owned or linked via Rivian accounts)
        var vehicleIds = await _db.Vehicles
            .Where(v => v.OwnerId == user.Id || rivianAccounts.Select(ra => ra.Id).Contains(v.RivianAccountId ?? 0))
            .Select(v => v.Id)
            .Distinct()
            .ToListAsync();

        // 3. Delete all vehicles (cascade will remove states, sessions, drives, positions, snapshots)
        if (vehicleIds.Any())
        {
            var vehicles = await _db.Vehicles
                .Where(v => vehicleIds.Contains(v.Id))
                .ToListAsync();

            _db.Vehicles.RemoveRange(vehicles);
            _logger.LogInformation("Deleting {VehicleCount} vehicles for user {UserId}", vehicles.Count, user.Id);
        }

        // 4. Delete all Rivian accounts
        if (rivianAccounts.Any())
        {
            _db.RivianAccounts.RemoveRange(rivianAccounts);
            _logger.LogInformation("Deleting {AccountCount} Rivian accounts for user {UserId}", rivianAccounts.Count, user.Id);
        }

        // 5. Delete dashboard configurations
        var dashboardConfigs = await _db.UserDashboardConfigs
            .Where(dc => dc.UserId == user.Id)
            .ToListAsync();

        if (dashboardConfigs.Any())
        {
            _db.UserDashboardConfigs.RemoveRange(dashboardConfigs);
            _logger.LogDebug("Deleting {ConfigCount} dashboard configs for user {UserId}", dashboardConfigs.Count, user.Id);
        }

        // 6. Save all deletions
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "User {UserId} account data deleted: {VehicleCount} vehicles, {AccountCount} Rivian accounts, {ConfigCount} dashboard configs",
            user.Id, vehicleIds.Count, rivianAccounts.Count, dashboardConfigs.Count);

        // 7. Delete the Identity user
        return await _userManager.DeleteAsync(user);
    }

    /// <summary>
    /// Get statistics for a user.
    /// </summary>
    public async Task<UserStats> GetUserStatsAsync(Guid userId)
    {
        var vehicleCount = await _db.Vehicles.CountAsync(v => v.OwnerId == userId);

        return new UserStats
        {
            VehicleCount = vehicleCount
        };
    }
}

public class UserStats
{
    public int VehicleCount { get; set; }
}
