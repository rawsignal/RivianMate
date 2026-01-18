using Hangfire;
using Microsoft.EntityFrameworkCore;
using RivianMate.Infrastructure.Data;

namespace RivianMate.Api.Services.Jobs;

/// <summary>
/// Manages Hangfire recurring jobs for Rivian account polling.
/// Handles registration, removal, and dynamic interval adjustment.
/// </summary>
public class PollingJobManager
{
    private readonly IRecurringJobManager _recurringJobs;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PollingJobManager> _logger;

    // Default intervals
    private const int DefaultAwakeIntervalSeconds = 30;
    private const int DefaultAsleepIntervalSeconds = 300; // 5 minutes
    private const int DefaultBackoffIntervalSeconds = 600; // 10 minutes

    public PollingJobManager(
        IRecurringJobManager recurringJobs,
        IBackgroundJobClient backgroundJobs,
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<PollingJobManager> logger)
    {
        _recurringJobs = recurringJobs;
        _backgroundJobs = backgroundJobs;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Get the job ID for an account's polling job.
    /// </summary>
    public static string GetJobId(int accountId) => $"poll-account-{accountId}";

    /// <summary>
    /// Register a polling job for a new account.
    /// Starts with the asleep interval (conservative).
    /// Note: Call TriggerImmediatePoll separately after vehicles are synced.
    /// </summary>
    public void RegisterAccountJob(int accountId)
    {
        var jobId = GetJobId(accountId);
        var interval = GetAsleepInterval();

        _logger.LogInformation(
            "Registering polling job {JobId} for account {AccountId} with interval {Interval}s",
            jobId, accountId, interval);

        var cronMinutes = Math.Max(1, interval / 60);
        _recurringJobs.AddOrUpdate<AccountPollingJob>(
            jobId,
            job => job.ExecuteAsync(accountId, CancellationToken.None),
            GetCronExpression(cronMinutes));
    }

    /// <summary>
    /// Remove the polling job for an account.
    /// </summary>
    public void RemoveAccountJob(int accountId)
    {
        var jobId = GetJobId(accountId);

        _logger.LogInformation("Removing polling job {JobId} for account {AccountId}", jobId, accountId);

        _recurringJobs.RemoveIfExists(jobId);
    }

    /// <summary>
    /// Update polling interval based on whether vehicles are awake.
    /// </summary>
    public void UpdateAccountPollingInterval(int accountId, bool hasAwakeVehicle)
    {
        var jobId = GetJobId(accountId);
        var interval = hasAwakeVehicle ? GetAwakeInterval() : GetAsleepInterval();
        var cronMinutes = Math.Max(1, interval / 60);

        _logger.LogDebug(
            "Updating polling interval for account {AccountId}: {Interval}s (awake: {Awake})",
            accountId, interval, hasAwakeVehicle);

        _recurringJobs.AddOrUpdate<AccountPollingJob>(
            jobId,
            job => job.ExecuteAsync(accountId, CancellationToken.None),
            GetCronExpression(cronMinutes));
    }

    /// <summary>
    /// Back off polling for an account (e.g., after rate limiting).
    /// </summary>
    public void BackoffAccount(int accountId, TimeSpan backoffDuration)
    {
        var jobId = GetJobId(accountId);
        var intervalMinutes = Math.Max(1, (int)backoffDuration.TotalMinutes);

        _logger.LogWarning(
            "Backing off polling for account {AccountId} for {Minutes} minutes",
            accountId, intervalMinutes);

        _recurringJobs.AddOrUpdate<AccountPollingJob>(
            jobId,
            job => job.ExecuteAsync(accountId, CancellationToken.None),
            GetCronExpression(intervalMinutes));
    }

    /// <summary>
    /// Synchronize jobs with database - register missing jobs, remove orphaned jobs.
    /// Called on application startup.
    /// </summary>
    public async Task SynchronizeJobsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Synchronizing polling jobs with database...");

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RivianMateDbContext>();

        // Get all active accounts with tokens
        var activeAccountIds = await db.RivianAccounts
            .Where(a => a.IsActive && a.EncryptedAccessToken != null)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Found {Count} active accounts to poll", activeAccountIds.Count);

        // Register jobs for all active accounts
        foreach (var accountId in activeAccountIds)
        {
            var jobId = GetJobId(accountId);

            // Register with asleep interval (conservative start)
            var interval = GetAsleepInterval();
            var cronMinutes = Math.Max(1, interval / 60);

            _recurringJobs.AddOrUpdate<AccountPollingJob>(
                jobId,
                job => job.ExecuteAsync(accountId, CancellationToken.None),
                GetCronExpression(cronMinutes));
        }

        _logger.LogInformation("Polling job synchronization complete");
    }

    /// <summary>
    /// Trigger an immediate poll for an account (e.g., when user opens the app).
    /// </summary>
    public void TriggerImmediatePoll(int accountId)
    {
        _logger.LogDebug("Triggering immediate poll for account {AccountId}", accountId);

        _backgroundJobs.Enqueue<AccountPollingJob>(
            job => job.ExecuteAsync(accountId, CancellationToken.None));
    }

    private int GetAwakeInterval()
    {
        return _configuration.GetValue("RivianMate:Polling:IntervalAwakeSeconds", DefaultAwakeIntervalSeconds);
    }

    private int GetAsleepInterval()
    {
        return _configuration.GetValue("RivianMate:Polling:IntervalAsleepSeconds", DefaultAsleepIntervalSeconds);
    }

    /// <summary>
    /// Generate a cron expression for the given minute interval.
    /// </summary>
    private static string GetCronExpression(int minutes)
    {
        minutes = Math.Max(1, minutes);

        return minutes switch
        {
            1 => "* * * * *",      // Every minute
            2 => "*/2 * * * *",    // Every 2 minutes
            3 => "*/3 * * * *",    // Every 3 minutes
            5 => "*/5 * * * *",    // Every 5 minutes
            10 => "*/10 * * * *",  // Every 10 minutes
            15 => "*/15 * * * *",  // Every 15 minutes
            30 => "*/30 * * * *",  // Every 30 minutes
            60 => "0 * * * *",     // Every hour
            _ => $"*/{minutes} * * * *"  // Every N minutes
        };
    }
}
