namespace RivianMate.Api.Services.Jobs;

/// <summary>
/// Manager for WebSocket vehicle state subscriptions.
/// Provides a unified interface for registering/removing accounts.
/// </summary>
public class SubscriptionManager
{
    private readonly WebSocketSubscriptionService? _wsService;
    private readonly ILogger<SubscriptionManager> _logger;

    public SubscriptionManager(
        ILogger<SubscriptionManager> logger,
        WebSocketSubscriptionService? wsService = null)
    {
        _wsService = wsService;
        _logger = logger;
    }

    /// <summary>
    /// Register an account for WebSocket state updates.
    /// </summary>
    public async Task RegisterAccountAsync(int accountId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Registering account {AccountId} for WebSocket updates", accountId);

        if (_wsService != null)
        {
            await _wsService.RefreshSubscriptionsAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Remove an account from WebSocket state updates.
    /// </summary>
    public async Task RemoveAccountAsync(int accountId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Removing account {AccountId} from WebSocket updates", accountId);

        if (_wsService != null)
        {
            await _wsService.RefreshSubscriptionsAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Refresh all WebSocket subscriptions.
    /// </summary>
    public async Task RefreshAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Refreshing all WebSocket subscriptions");

        if (_wsService != null)
        {
            await _wsService.RefreshSubscriptionsAsync(cancellationToken);
        }
    }
}
