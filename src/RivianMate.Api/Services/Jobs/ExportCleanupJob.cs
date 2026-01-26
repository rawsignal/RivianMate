using Hangfire;
using Microsoft.EntityFrameworkCore;
using RivianMate.Core.Enums;
using RivianMate.Infrastructure.Data;

namespace RivianMate.Api.Services.Jobs;

/// <summary>
/// Daily Hangfire job that deletes expired data exports to free up storage.
/// </summary>
public class ExportCleanupJob
{
    private readonly IDbContextFactory<RivianMateDbContext> _dbFactory;
    private readonly ILogger<ExportCleanupJob> _logger;

    public ExportCleanupJob(
        IDbContextFactory<RivianMateDbContext> dbFactory,
        ILogger<ExportCleanupJob> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 1)]
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    [Queue("default")]
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting export cleanup job");

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var now = DateTime.UtcNow;

        // Mark completed exports past expiration as expired, and clear their file data
        var expiredExports = await db.DataExports
            .Where(e => e.ExpiresAt < now && e.Status == ExportStatus.Completed)
            .ToListAsync(cancellationToken);

        foreach (var export in expiredExports)
        {
            export.Status = ExportStatus.Expired;
            export.FileData = null;
        }

        // Delete failed and expired exports older than 7 days entirely
        var cutoff = now.AddDays(-7);
        var oldExports = await db.DataExports
            .Where(e => e.CreatedAt < cutoff &&
                (e.Status == ExportStatus.Failed || e.Status == ExportStatus.Expired))
            .ToListAsync(cancellationToken);

        db.DataExports.RemoveRange(oldExports);

        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Export cleanup complete. Expired: {ExpiredCount}, Deleted: {DeletedCount}",
            expiredExports.Count, oldExports.Count);
    }
}
