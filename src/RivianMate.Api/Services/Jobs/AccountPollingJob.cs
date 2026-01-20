using Hangfire;
using Microsoft.EntityFrameworkCore;
using RivianMate.Core.Entities;
using RivianMate.Core.Enums;
using RivianMate.Core.Exceptions;
using RivianMate.Infrastructure.Data;

namespace RivianMate.Api.Services.Jobs;

/// <summary>
/// Hangfire job that polls a single Rivian account for vehicle state updates.
/// Each account gets its own recurring job, allowing independent scheduling and scaling.
/// </summary>
public class AccountPollingJob
{
    private readonly RivianMateDbContext _db;
    private readonly RivianAccountService _rivianAccountService;
    private readonly VehicleService _vehicleService;
    private readonly BatteryHealthService _batteryHealthService;
    private readonly DriveTrackingService _driveTrackingService;
    private readonly PollingJobManager _jobManager;
    private readonly ILogger<AccountPollingJob> _logger;

    public AccountPollingJob(
        RivianMateDbContext db,
        RivianAccountService rivianAccountService,
        VehicleService vehicleService,
        BatteryHealthService batteryHealthService,
        DriveTrackingService driveTrackingService,
        PollingJobManager jobManager,
        ILogger<AccountPollingJob> logger)
    {
        _db = db;
        _rivianAccountService = rivianAccountService;
        _vehicleService = vehicleService;
        _batteryHealthService = batteryHealthService;
        _driveTrackingService = driveTrackingService;
        _jobManager = jobManager;
        _logger = logger;
    }

    /// <summary>
    /// Poll a specific Rivian account for vehicle state updates.
    /// This method is called by Hangfire on a schedule.
    /// </summary>
    [AutomaticRetry(Attempts = 2, DelaysInSeconds = new[] { 30, 60 })]
    [DisableConcurrentExecution(timeoutInSeconds: 120)]
    public async Task ExecuteAsync(int accountId, CancellationToken cancellationToken)
    {
        var account = await _db.RivianAccounts
            .FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);

        if (account == null)
        {
            _logger.LogWarning("Account {AccountId} not found, removing job", accountId);
            _jobManager.RemoveAccountJob(accountId);
            return;
        }

        if (!account.IsActive || string.IsNullOrEmpty(account.EncryptedAccessToken))
        {
            _logger.LogDebug("Account {AccountId} is inactive or has no token, skipping poll", accountId);
            return;
        }

        _logger.LogDebug("Starting poll for account {AccountId} ({Email})", accountId, account.RivianEmail);

        try
        {
            var (hasAwakeVehicle, vehicleErrors) = await PollAccountVehiclesAsync(account, cancellationToken);

            // Update last sync
            account.LastSyncAt = DateTime.UtcNow;
            account.LastSyncError = vehicleErrors.Count > 0
                ? string.Join("; ", vehicleErrors)
                : null;
            await _db.SaveChangesAsync(cancellationToken);

            // Adjust polling frequency based on vehicle state
            _jobManager.UpdateAccountPollingInterval(accountId, hasAwakeVehicle);

            _logger.LogDebug("Poll complete for account {AccountId}, awake vehicles: {HasAwake}, errors: {ErrorCount}",
                accountId, hasAwakeVehicle, vehicleErrors.Count);
        }
        catch (RateLimitedException ex)
        {
            _logger.LogWarning("Rate limited for account {AccountId}, backing off: {Message}",
                accountId, ex.ExternalMessage);

            account.LastSyncError = $"Rate limited: {ex.ExternalMessage}";
            await _db.SaveChangesAsync(cancellationToken);

            // Back off this specific account
            _jobManager.BackoffAccount(accountId, TimeSpan.FromMinutes(5));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error polling account {AccountId}", accountId);

            account.LastSyncError = ex.Message;
            await _db.SaveChangesAsync(cancellationToken);

            throw; // Let Hangfire handle retry
        }
    }

    private async Task<(bool AnyAwake, List<string> Errors)> PollAccountVehiclesAsync(RivianAccount account, CancellationToken cancellationToken)
    {
        using var client = _rivianAccountService.CreateApiClient(account);

        var vehicles = await _db.Vehicles
            .Where(v => v.IsActive && v.RivianAccountId == account.Id)
            .ToListAsync(cancellationToken);

        if (!vehicles.Any())
        {
            _logger.LogDebug("No active vehicles for account {AccountId}", account.Id);
            return (false, new List<string>());
        }

        var anyAwake = false;
        var errors = new List<string>();
        var hasRefreshedToken = false;

        foreach (var vehicle in vehicles)
        {
            try
            {
                var isAwake = await PollVehicleAsync(vehicle, client, cancellationToken);
                if (isAwake) anyAwake = true;
            }
            catch (ExternalServiceException ex) when (IsLikelyTokenExpiration(ex) && !hasRefreshedToken)
            {
                // Try refreshing the CSRF token and retry once
                _logger.LogWarning("Possible token expiration for account {AccountId}, attempting CSRF refresh...", account.Id);

                if (await TryRefreshCsrfTokenAsync(client, account, cancellationToken))
                {
                    hasRefreshedToken = true;
                    _logger.LogInformation("CSRF token refreshed for account {AccountId}, retrying vehicle poll...", account.Id);

                    try
                    {
                        var isAwake = await PollVehicleAsync(vehicle, client, cancellationToken);
                        if (isAwake) anyAwake = true;
                    }
                    catch (Exception retryEx)
                    {
                        _logger.LogError(retryEx, "Error polling vehicle {VehicleId} after token refresh", vehicle.Id);
                        errors.Add($"Vehicle {vehicle.Name}: {retryEx.Message} (after token refresh)");
                    }
                }
                else
                {
                    _logger.LogError("Failed to refresh CSRF token for account {AccountId}", account.Id);
                    errors.Add($"Vehicle {vehicle.Name}: {ex.Message} (token refresh failed)");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling vehicle {VehicleId} for account {AccountId}",
                    vehicle.Id, account.Id);
                errors.Add($"Vehicle {vehicle.Name}: {ex.Message}");
            }
        }

        return (anyAwake, errors);
    }

    /// <summary>
    /// Check if an exception is likely caused by token expiration.
    /// Rivian returns INTERNAL_SERVER_ERROR or UNAUTHENTICATED when tokens expire.
    /// </summary>
    private static bool IsLikelyTokenExpiration(ExternalServiceException ex)
    {
        var message = ex.Message.ToLowerInvariant();
        return message.Contains("internal_server_error") ||
               message.Contains("unauthenticated") ||
               message.Contains("unauthorized") ||
               message.Contains("unexpected error occurred");
    }

    /// <summary>
    /// Try to refresh the CSRF token and update stored tokens.
    /// </summary>
    private async Task<bool> TryRefreshCsrfTokenAsync(
        Infrastructure.Rivian.RivianApiClient client,
        RivianAccount account,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!await client.CreateCsrfTokenAsync(cancellationToken))
            {
                return false;
            }

            // Get the new tokens and save them to the database
            var tokens = client.GetTokens();
            await _rivianAccountService.UpdateTokensAsync(
                account,
                tokens.CsrfToken,
                tokens.AppSessionToken,
                tokens.UserSessionToken,
                tokens.AccessToken,
                tokens.RefreshToken,
                cancellationToken);

            _logger.LogInformation("Updated stored tokens for account {AccountId} after CSRF refresh", account.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing CSRF token for account {AccountId}", account.Id);
            return false;
        }
    }

    private async Task<bool> PollVehicleAsync(
        Vehicle vehicle,
        Infrastructure.Rivian.RivianApiClient client,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Polling vehicle {VehicleName} (ID: {VehicleId}, RivianId: {RivianVehicleId})",
            vehicle.Name, vehicle.Id, vehicle.RivianVehicleId);

        var (state, rawJson) = await client.GetVehicleStateAsync(vehicle.RivianVehicleId, cancellationToken);

        if (state == null)
        {
            _logger.LogWarning("No state returned for vehicle {VehicleId}", vehicle.Id);
            return false;
        }

        var vehicleState = await _vehicleService.ProcessVehicleStateAsync(
            vehicle.Id, state, rawJson, cancellationToken);

        if (vehicleState == null)
        {
            return false;
        }

        _logger.LogDebug(
            "Vehicle {VehicleId}: Battery={Battery}%, Range={Range}mi, Power={PowerState}",
            vehicle.Id, vehicleState.BatteryLevel, vehicleState.RangeEstimate, vehicleState.PowerState);

        // Track drives (detect start/end, record positions)
        await _driveTrackingService.ProcessStateForDriveTrackingAsync(
            vehicle.Id, vehicleState, cancellationToken);

        // Record battery health snapshot if needed
        await MaybeRecordBatteryHealthAsync(vehicle.Id, vehicleState, cancellationToken);

        // Fetch vehicle image if we don't have one
        await MaybeFetchVehicleImageAsync(vehicle, client, cancellationToken);

        return vehicleState.PowerState != PowerState.Sleep;
    }

    private async Task MaybeFetchVehicleImageAsync(
        Vehicle vehicle,
        Infrastructure.Rivian.RivianApiClient client,
        CancellationToken cancellationToken)
    {
        // Only fetch if we don't have an image
        if (vehicle.ImageData != null)
            return;

        try
        {
            _logger.LogDebug("Fetching vehicle image for {VehicleId}", vehicle.Id);

            // Use stored version if available to avoid trying all 5 versions
            var (imageUrl, workingVersion) = await client.GetVehicleImageUrlAsync(
                vehicle.RivianVehicleId, vehicle.ImageVersion, cancellationToken);

            if (string.IsNullOrEmpty(imageUrl))
            {
                _logger.LogDebug("No vehicle images available for {VehicleId}", vehicle.Id);
                return;
            }

            _logger.LogDebug("Downloading vehicle image from {Url}", imageUrl);
            var (imageData, contentType) = await client.DownloadImageAsync(imageUrl, cancellationToken);

            if (imageData != null && imageData.Length > 0)
            {
                vehicle.ImageData = imageData;
                vehicle.ImageContentType = contentType ?? "image/png";
                vehicle.ImageVersion = workingVersion;
                await _db.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Saved vehicle image for {VehicleId} ({Size} bytes, version {Version})",
                    vehicle.Id, imageData.Length, workingVersion);
            }
        }
        catch (Exception ex)
        {
            // Don't fail the whole poll if image fetch fails
            _logger.LogWarning(ex, "Failed to fetch vehicle image for {VehicleId}", vehicle.Id);
        }
    }

    private async Task MaybeRecordBatteryHealthAsync(
        int vehicleId,
        VehicleState currentState,
        CancellationToken cancellationToken)
    {
        if (currentState.BatteryCapacityKwh == null)
            return;

        var lastSnapshot = await _db.BatteryHealthSnapshots
            .Where(s => s.VehicleId == vehicleId)
            .OrderByDescending(s => s.Timestamp)
            .FirstOrDefaultAsync(cancellationToken);

        var shouldRecord = lastSnapshot == null
            || lastSnapshot.Timestamp < DateTime.UtcNow.AddHours(-1)
            || Math.Abs(lastSnapshot.ReportedCapacityKwh - currentState.BatteryCapacityKwh.Value) > 0.5;

        if (shouldRecord)
        {
            await _batteryHealthService.RecordHealthSnapshotAsync(vehicleId, cancellationToken);
        }
    }
}
