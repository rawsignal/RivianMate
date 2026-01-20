using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RivianMate.Api.Configuration;
using RivianMate.Core.Entities;
using RivianMate.Core.Enums;
using RivianMate.Infrastructure.Data;
using RivianMate.Infrastructure.Rivian;
using RivianMate.Infrastructure.Rivian.Models;
using System.Collections.Concurrent;

namespace RivianMate.Api.Services;

/// <summary>
/// Background service that manages WebSocket subscriptions for real-time vehicle state updates.
/// Alternative to Hangfire-based polling when configured for WebSocket mode.
/// </summary>
public class WebSocketSubscriptionService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WebSocketSubscriptionService> _logger;
    private readonly PollingConfiguration _config;

    // Track active subscriptions per account
    private readonly ConcurrentDictionary<int, AccountSubscription> _accountSubscriptions = new();

    // Vehicle state properties to subscribe to (same as GraphQL polling)
    private static readonly string[] VehicleStateProperties = new[]
    {
        "activeDriverName",
        "alarmSoundStatus",
        "batteryCapacity",
        "batteryCellType",
        "batteryHvThermalEvent",
        "batteryHvThermalEventPropagation",
        "batteryLevel",
        "batteryLimit",
        "batteryNeedsLfpCalibration",
        "brakeFluidLow",
        "cabinClimateDriverTemperature",
        "cabinClimateInteriorTemperature",
        "cabinPreconditioningStatus",
        "cabinPreconditioningType",
        "carWashMode",
        "chargerDerateStatus",
        "chargerState",
        "chargerStatus",
        "chargePortState",
        "closureFrunkClosed",
        "closureFrunkLocked",
        "closureLiftgateClosed",
        "closureLiftgateLocked",
        "closureSideBinLeftClosed",
        "closureSideBinLeftLocked",
        "closureSideBinRightClosed",
        "closureSideBinRightLocked",
        "closureTailgateClosed",
        "closureTailgateLocked",
        "closureTonneauClosed",
        "closureTonneauLocked",
        "defrostDefogStatus",
        "distanceToEmpty",
        "doorFrontLeftClosed",
        "doorFrontLeftLocked",
        "doorFrontRightClosed",
        "doorFrontRightLocked",
        "doorRearLeftClosed",
        "doorRearLeftLocked",
        "doorRearRightClosed",
        "doorRearRightLocked",
        "driveMode",
        "gearGuardLocked",
        "gearGuardVideoMode",
        "gearGuardVideoStatus",
        "gearStatus",
        "gnssAltitude",
        "gnssBearing",
        "gnssSpeed",
        "limitedAccelCold",
        "limitedRegenCold",
        "otaAvailableVersion",
        "otaCurrentStatus",
        "otaCurrentVersion",
        "otaInstallProgress",
        "otaStatus",
        "petModeStatus",
        "powerState",
        "rangeThreshold",
        "remoteChargingAvailable",
        "serviceMode",
        "timeToEndOfCharge",
        "tirePressureStatusFrontLeft",
        "tirePressureStatusFrontRight",
        "tirePressureStatusRearLeft",
        "tirePressureStatusRearRight",
        "tirePressureFrontLeft",
        "tirePressureFrontRight",
        "tirePressureRearLeft",
        "tirePressureRearRight",
        "trailerStatus",
        "twelveVoltBatteryHealth",
        "vehicleMileage",
        "windowFrontLeftClosed",
        "windowFrontRightClosed",
        "windowRearLeftClosed",
        "windowRearRightClosed",
        "windowsNextAction"
    };

    public WebSocketSubscriptionService(
        IServiceProvider serviceProvider,
        IOptions<PollingConfiguration> config,
        ILogger<WebSocketSubscriptionService> logger)
    {
        _serviceProvider = serviceProvider;
        _config = config.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WebSocket subscription service starting...");

        // Initial startup delay to let the application settle
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshSubscriptionsAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error refreshing WebSocket subscriptions");
            }

            // Check for account changes every 30 seconds
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }

        // Cleanup on shutdown
        _logger.LogInformation("WebSocket subscription service stopping, cleaning up...");
        await CleanupAllSubscriptionsAsync();
    }

    /// <summary>
    /// Refresh subscriptions - start new ones, remove stale ones.
    /// </summary>
    public async Task RefreshSubscriptionsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RivianMateDbContext>();

        // Get all active accounts
        var activeAccounts = await db.RivianAccounts
            .Where(a => a.IsActive && !string.IsNullOrEmpty(a.EncryptedAccessToken))
            .Include(a => a.Vehicles.Where(v => v.IsActive))
            .ToListAsync(cancellationToken);

        var activeAccountIds = activeAccounts.Select(a => a.Id).ToHashSet();

        // Remove subscriptions for accounts that are no longer active
        var accountsToRemove = _accountSubscriptions.Keys
            .Where(id => !activeAccountIds.Contains(id))
            .ToList();

        foreach (var accountId in accountsToRemove)
        {
            await StopAccountSubscriptionAsync(accountId);
        }

        // Start or refresh subscriptions for active accounts
        foreach (var account in activeAccounts)
        {
            if (!account.Vehicles.Any())
            {
                _logger.LogDebug("Account {AccountId} has no active vehicles, skipping", account.Id);
                continue;
            }

            if (!_accountSubscriptions.TryGetValue(account.Id, out var subscription))
            {
                // Start new subscription
                await StartAccountSubscriptionAsync(account, cancellationToken);
            }
            else if (!subscription.Client.IsConnected)
            {
                // Reconnect if disconnected
                _logger.LogInformation("Reconnecting subscription for account {AccountId}", account.Id);
                await StopAccountSubscriptionAsync(account.Id);
                await StartAccountSubscriptionAsync(account, cancellationToken);
            }
            else
            {
                // Check if vehicles have changed
                var currentVehicleIds = account.Vehicles.Select(v => v.RivianVehicleId).ToHashSet();
                if (!currentVehicleIds.SetEquals(subscription.SubscribedVehicleIds))
                {
                    _logger.LogInformation("Vehicles changed for account {AccountId}, resubscribing", account.Id);
                    await StopAccountSubscriptionAsync(account.Id);
                    await StartAccountSubscriptionAsync(account, cancellationToken);
                }
            }
        }
    }

    private async Task StartAccountSubscriptionAsync(RivianAccount account, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting WebSocket subscription for account {AccountId} ({Email})",
            account.Id, account.RivianEmail);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var accountService = scope.ServiceProvider.GetRequiredService<RivianAccountService>();
            var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();

            // Get decrypted tokens
            var (userSessionToken, accessToken) = accountService.GetDecryptedTokens(account);

            if (string.IsNullOrEmpty(userSessionToken))
            {
                _logger.LogWarning("No user session token for account {AccountId}", account.Id);
                return;
            }

            var client = new RivianWebSocketClient(
                loggerFactory.CreateLogger<RivianWebSocketClient>());

            var subscription = new AccountSubscription
            {
                AccountId = account.Id,
                Client = client,
                CancellationSource = new CancellationTokenSource()
            };

            // Set up event handlers
            client.OnVehicleStateUpdate += async (vehicleRivianId, state, rawJson) =>
            {
                await HandleVehicleStateUpdateAsync(account.Id, vehicleRivianId, state, rawJson);
            };

            client.OnError += async (ex) =>
            {
                await HandleSubscriptionErrorAsync(subscription, ex);
            };

            client.OnDisconnected += async () =>
            {
                await HandleDisconnectionAsync(subscription);
            };

            client.OnConnected += () =>
            {
                subscription.ErrorCount = 0;
                subscription.LastConnectedAt = DateTime.UtcNow;
                _logger.LogInformation("WebSocket connected for account {AccountId}", account.Id);
                return Task.CompletedTask;
            };

            // Set tokens and connect
            client.SetTokens(userSessionToken, accessToken ?? string.Empty);
            await client.ConnectAsync(cancellationToken);

            // Subscribe to all vehicles
            foreach (var vehicle in account.Vehicles)
            {
                try
                {
                    await client.SubscribeToVehicleAsync(
                        vehicle.RivianVehicleId,
                        VehicleStateProperties,
                        cancellationToken);

                    subscription.SubscribedVehicleIds.Add(vehicle.RivianVehicleId);
                    subscription.VehicleIdMap[vehicle.RivianVehicleId] = vehicle.Id;

                    _logger.LogInformation("Subscribed to vehicle {VehicleName} ({RivianId}) for account {AccountId}",
                        vehicle.Name, vehicle.RivianVehicleId, account.Id);

                    // Fetch vehicle image if we don't have one
                    if (vehicle.ImageData == null)
                    {
                        _ = Task.Run(() => FetchVehicleImageAsync(account, vehicle, cancellationToken));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to subscribe to vehicle {VehicleId}", vehicle.RivianVehicleId);
                }
            }

            _accountSubscriptions[account.Id] = subscription;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start WebSocket subscription for account {AccountId}", account.Id);
        }
    }

    private async Task StopAccountSubscriptionAsync(int accountId)
    {
        if (_accountSubscriptions.TryRemove(accountId, out var subscription))
        {
            _logger.LogInformation("Stopping WebSocket subscription for account {AccountId}", accountId);

            try
            {
                subscription.CancellationSource.Cancel();
                await subscription.Client.DisposeAsync();
                subscription.CancellationSource.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error stopping subscription for account {AccountId}", accountId);
            }
        }
    }

    private async Task HandleVehicleStateUpdateAsync(int accountId, string vehicleRivianId, RivianVehicleState state, string rawJson)
    {
        if (!_accountSubscriptions.TryGetValue(accountId, out var subscription))
        {
            _logger.LogWarning("Received update for unknown account {AccountId}", accountId);
            return;
        }

        if (!subscription.VehicleIdMap.TryGetValue(vehicleRivianId, out var vehicleId))
        {
            _logger.LogWarning("Received update for unknown vehicle {VehicleRivianId}", vehicleRivianId);
            return;
        }

        subscription.LastUpdateAt = DateTime.UtcNow;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var vehicleService = scope.ServiceProvider.GetRequiredService<VehicleService>();
            var driveTrackingService = scope.ServiceProvider.GetRequiredService<DriveTrackingService>();
            var batteryHealthService = scope.ServiceProvider.GetRequiredService<BatteryHealthService>();
            var db = scope.ServiceProvider.GetRequiredService<RivianMateDbContext>();

            // Process the vehicle state update
            // Use isPartialUpdate: true because WebSocket sends different fields in separate messages
            var vehicleState = await vehicleService.ProcessVehicleStateAsync(
                vehicleId, state, rawJson, CancellationToken.None, isPartialUpdate: true);

            if (vehicleState != null)
            {
                _logger.LogDebug(
                    "WebSocket update for vehicle {VehicleId}: Battery={Battery}%, Range={Range}mi, Power={PowerState}",
                    vehicleId, vehicleState.BatteryLevel, vehicleState.RangeEstimate, vehicleState.PowerState);

                // Track drives
                await driveTrackingService.ProcessStateForDriveTrackingAsync(
                    vehicleId, vehicleState, CancellationToken.None);

                // Record battery health snapshot if needed
                await MaybeRecordBatteryHealthAsync(vehicleId, vehicleState, batteryHealthService, db);
            }

            // Update account last sync
            var account = await db.RivianAccounts.FindAsync(accountId);
            if (account != null)
            {
                account.LastSyncAt = DateTime.UtcNow;
                account.LastSyncError = null;
                await db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing vehicle state update for vehicle {VehicleId}", vehicleId);
        }
    }

    private async Task MaybeRecordBatteryHealthAsync(
        int vehicleId,
        VehicleState currentState,
        BatteryHealthService batteryHealthService,
        RivianMateDbContext db)
    {
        if (currentState.BatteryCapacityKwh == null)
            return;

        var lastSnapshot = await db.BatteryHealthSnapshots
            .Where(s => s.VehicleId == vehicleId)
            .OrderByDescending(s => s.Timestamp)
            .FirstOrDefaultAsync();

        var shouldRecord = lastSnapshot == null
            || lastSnapshot.Timestamp < DateTime.UtcNow.AddHours(-1)
            || Math.Abs(lastSnapshot.ReportedCapacityKwh - currentState.BatteryCapacityKwh.Value) > 0.5;

        if (shouldRecord)
        {
            await batteryHealthService.RecordHealthSnapshotAsync(vehicleId, CancellationToken.None);
        }
    }

    private async Task HandleSubscriptionErrorAsync(AccountSubscription subscription, Exception ex)
    {
        subscription.ErrorCount++;
        _logger.LogError(ex, "WebSocket error for account {AccountId} (error count: {ErrorCount})",
            subscription.AccountId, subscription.ErrorCount);

        if (subscription.ErrorCount >= _config.WebSocketMaxConsecutiveErrors)
        {
            _logger.LogWarning(
                "Account {AccountId} has reached max consecutive errors ({MaxErrors}), will reconnect on next refresh",
                subscription.AccountId, _config.WebSocketMaxConsecutiveErrors);
        }

        // Update account sync error
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RivianMateDbContext>();
            var account = await db.RivianAccounts.FindAsync(subscription.AccountId);
            if (account != null)
            {
                account.LastSyncError = $"WebSocket error: {ex.Message}";
                await db.SaveChangesAsync();
            }
        }
        catch (Exception saveEx)
        {
            _logger.LogError(saveEx, "Error saving sync error for account {AccountId}", subscription.AccountId);
        }
    }

    private async Task HandleDisconnectionAsync(AccountSubscription subscription)
    {
        _logger.LogWarning("WebSocket disconnected for account {AccountId}", subscription.AccountId);

        // Calculate backoff delay
        var delay = CalculateBackoffDelay(subscription.ReconnectAttempts);
        subscription.ReconnectAttempts++;

        _logger.LogInformation(
            "Will attempt reconnection for account {AccountId} in {Delay} seconds (attempt {Attempt})",
            subscription.AccountId, delay.TotalSeconds, subscription.ReconnectAttempts);

        // Update account sync error
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RivianMateDbContext>();
            var account = await db.RivianAccounts.FindAsync(subscription.AccountId);
            if (account != null)
            {
                account.LastSyncError = $"WebSocket disconnected, reconnecting in {delay.TotalSeconds}s";
                await db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving sync error for account {AccountId}", subscription.AccountId);
        }

        // Schedule reconnection
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, subscription.CancellationSource.Token);

                // Refresh will handle the reconnection
                _logger.LogInformation("Triggering reconnection for account {AccountId}", subscription.AccountId);
            }
            catch (OperationCanceledException)
            {
                // Subscription was stopped, ignore
            }
        });
    }

    private TimeSpan CalculateBackoffDelay(int attemptNumber)
    {
        // Exponential backoff: 5, 10, 20, 40, ... up to max
        var baseDelay = _config.WebSocketReconnectDelaySeconds;
        var maxDelay = _config.WebSocketMaxReconnectDelaySeconds;

        var delay = baseDelay * Math.Pow(2, Math.Min(attemptNumber, 10));
        delay = Math.Min(delay, maxDelay);

        // Add some jitter (0-25%)
        var jitter = delay * Random.Shared.NextDouble() * 0.25;

        return TimeSpan.FromSeconds(delay + jitter);
    }

    private async Task FetchVehicleImageAsync(RivianAccount account, Vehicle vehicle, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Fetching vehicle image for {VehicleId} ({VehicleName})", vehicle.Id, vehicle.Name);

            using var scope = _serviceProvider.CreateScope();
            var accountService = scope.ServiceProvider.GetRequiredService<RivianAccountService>();
            var db = scope.ServiceProvider.GetRequiredService<RivianMateDbContext>();

            // Create a GraphQL API client to fetch the image
            using var client = accountService.CreateApiClient(account);

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
                // Re-fetch the vehicle to avoid concurrency issues
                var dbVehicle = await db.Vehicles.FindAsync(vehicle.Id);
                if (dbVehicle != null)
                {
                    dbVehicle.ImageData = imageData;
                    dbVehicle.ImageContentType = contentType ?? "image/png";
                    dbVehicle.ImageVersion = workingVersion;
                    await db.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation("Saved vehicle image for {VehicleId} ({Size} bytes, version {Version})",
                        vehicle.Id, imageData.Length, workingVersion);
                }
            }
        }
        catch (Exception ex)
        {
            // Don't fail the subscription if image fetch fails
            _logger.LogWarning(ex, "Failed to fetch vehicle image for {VehicleId}", vehicle.Id);
        }
    }

    private async Task CleanupAllSubscriptionsAsync()
    {
        foreach (var accountId in _accountSubscriptions.Keys.ToList())
        {
            await StopAccountSubscriptionAsync(accountId);
        }
    }

    /// <summary>
    /// Tracks the subscription state for a single Rivian account.
    /// </summary>
    private class AccountSubscription
    {
        public required int AccountId { get; init; }
        public required RivianWebSocketClient Client { get; init; }
        public required CancellationTokenSource CancellationSource { get; init; }
        public HashSet<string> SubscribedVehicleIds { get; } = new();
        public Dictionary<string, int> VehicleIdMap { get; } = new(); // RivianVehicleId -> VehicleId
        public DateTime? LastUpdateAt { get; set; }
        public DateTime? LastConnectedAt { get; set; }
        public int ErrorCount { get; set; }
        public int ReconnectAttempts { get; set; }
    }
}
