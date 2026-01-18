namespace RivianMate.Api.Services.Jobs;

/// <summary>
/// Background service that synchronizes polling jobs on application startup.
/// Ensures all active accounts have their polling jobs registered.
/// </summary>
public class PollingJobSynchronizer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PollingJobSynchronizer> _logger;

    public PollingJobSynchronizer(
        IServiceProvider serviceProvider,
        ILogger<PollingJobSynchronizer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for Hangfire to fully initialize
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        _logger.LogInformation("Starting polling job synchronization...");

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
