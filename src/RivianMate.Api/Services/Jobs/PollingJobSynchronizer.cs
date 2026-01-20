using Microsoft.Extensions.Options;
using RivianMate.Api.Configuration;
using RivianMate.Core.Enums;

namespace RivianMate.Api.Services.Jobs;

/// <summary>
/// Background service that synchronizes polling jobs on application startup.
/// Ensures all active accounts have their polling jobs registered.
/// Only runs in GraphQL mode - WebSocket mode uses WebSocketSubscriptionService instead.
/// </summary>
public class PollingJobSynchronizer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly PollingConfiguration _config;
    private readonly ILogger<PollingJobSynchronizer> _logger;

    public PollingJobSynchronizer(
        IServiceProvider serviceProvider,
        IOptions<PollingConfiguration> config,
        ILogger<PollingJobSynchronizer> logger)
    {
        _serviceProvider = serviceProvider;
        _config = config.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Only synchronize polling jobs in GraphQL mode
        if (_config.Mode != PollingMode.GraphQL)
        {
            _logger.LogInformation(
                "Polling job synchronization skipped - running in {Mode} mode",
                _config.Mode);
            return;
        }

        // Wait for Hangfire to fully initialize
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        _logger.LogInformation("Starting polling job synchronization (GraphQL mode)...");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var jobManager = scope.ServiceProvider.GetRequiredService<PollingJobManager>();
            await jobManager.SynchronizeJobsAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error synchronizing polling jobs");
        }
    }
}
