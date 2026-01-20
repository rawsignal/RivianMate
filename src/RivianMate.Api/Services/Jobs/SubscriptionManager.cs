using Microsoft.Extensions.Options;
using RivianMate.Api.Configuration;
using RivianMate.Core.Enums;

namespace RivianMate.Api.Services.Jobs;

/// <summary>
/// Unified manager that handles both polling and WebSocket modes for vehicle state subscriptions.
/// Provides a single interface for registering/removing accounts regardless of the underlying mode.
/// </summary>
public class SubscriptionManager
{
    private readonly PollingConfiguration _config;
    private readonly PollingJobManager _pollingManager;
    private readonly WebSocketSubscriptionService? _wsService;
    private readonly ILogger<SubscriptionManager> _logger;

    public SubscriptionManager(
        IOptions<PollingConfiguration> config,
        PollingJobManager pollingManager,
        ILogger<SubscriptionManager> logger,
        WebSocketSubscriptionService? wsService = null)
    {
        _config = config.Value;
        _pollingManager = pollingManager;
        _wsService = wsService;
        _logger = logger;
    }

    /// <summary>
    /// Current polling/subscription mode.
    /// </summary>
    public PollingMode CurrentMode => _config.Mode;

    /// <summary>
    /// Register an account for state updates (polling or WebSocket based on mode).
    /// </summary>
    public async Task RegisterAccountAsync(int accountId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Registering account {AccountId} for {Mode} mode", accountId, _config.Mode);

        switch (_config.Mode)
        {
            case PollingMode.GraphQL:
                _pollingManager.RegisterAccountJob(accountId);
                break;

            case PollingMode.WebSocket:
                // WebSocket service handles registration through its background refresh loop
                // Just trigger a refresh to pick up the new account
                if (_wsService != null)
                {
                    await _wsService.RefreshSubscriptionsAsync(cancellationToken);
                }
                break;
        }
    }

    /// <summary>
    /// Remove an account from state updates.
    /// </summary>
    public async Task RemoveAccountAsync(int accountId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Removing account {AccountId} from {Mode} mode", accountId, _config.Mode);

        switch (_config.Mode)
        {
            case PollingMode.GraphQL:
                _pollingManager.RemoveAccountJob(accountId);
                break;

            case PollingMode.WebSocket:
                // WebSocket service handles removal through its background refresh loop
                if (_wsService != null)
                {
                    await _wsService.RefreshSubscriptionsAsync(cancellationToken);
                }
                break;
        }
    }

    /// <summary>
    /// Update polling interval for an account (GraphQL mode only).
    /// </summary>
    public void UpdatePollingInterval(int accountId, bool hasAwakeVehicle)
    {
        if (_config.Mode == PollingMode.GraphQL)
        {
            _pollingManager.UpdateAccountPollingInterval(accountId, hasAwakeVehicle);
        }
        // WebSocket mode doesn't need interval adjustments - it's real-time
    }

    /// <summary>
    /// Trigger a backoff for an account (GraphQL mode only).
    /// </summary>
    public void BackoffAccount(int accountId, TimeSpan duration)
    {
        if (_config.Mode == PollingMode.GraphQL)
        {
            _pollingManager.BackoffAccount(accountId, duration);
        }
        // WebSocket mode handles backoff internally with exponential reconnection
    }

    /// <summary>
    /// Refresh all subscriptions/jobs.
    /// </summary>
    public async Task RefreshAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Refreshing all subscriptions for {Mode} mode", _config.Mode);

        switch (_config.Mode)
        {
            case PollingMode.GraphQL:
                await _pollingManager.SynchronizeJobsAsync(cancellationToken);
                break;

            case PollingMode.WebSocket:
                if (_wsService != null)
                {
                    await _wsService.RefreshSubscriptionsAsync(cancellationToken);
                }
                break;
        }
    }
}
