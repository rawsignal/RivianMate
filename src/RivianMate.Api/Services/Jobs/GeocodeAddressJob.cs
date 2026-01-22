using Hangfire;
using Microsoft.EntityFrameworkCore;
using RivianMate.Infrastructure.Data;

namespace RivianMate.Api.Services.Jobs;

/// <summary>
/// Hangfire job that geocodes addresses for drives.
/// Can process a single drive or batch process drives missing addresses.
/// </summary>
public class GeocodeAddressJob
{
    private readonly IDbContextFactory<RivianMateDbContext> _dbFactory;
    private readonly GeocodingService _geocodingService;
    private readonly ILogger<GeocodeAddressJob> _logger;

    public GeocodeAddressJob(
        IDbContextFactory<RivianMateDbContext> dbFactory,
        GeocodingService geocodingService,
        ILogger<GeocodeAddressJob> logger)
    {
        _dbFactory = dbFactory;
        _geocodingService = geocodingService;
        _logger = logger;
    }

    /// <summary>
    /// Geocode addresses for a specific drive.
    /// Called when a drive ends.
    /// </summary>
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
    [Queue("default")]
    public async Task GeocodeForDriveAsync(int driveId, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Geocoding addresses for drive {DriveId}", driveId);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var drive = await db.Drives.FindAsync(new object[] { driveId }, cancellationToken);
        if (drive == null)
        {
            _logger.LogWarning("Drive {DriveId} not found", driveId);
            return;
        }

        var updated = false;

        // Geocode start address if missing and we have coordinates
        if (string.IsNullOrEmpty(drive.StartAddress) && drive.StartLatitude.HasValue && drive.StartLongitude.HasValue)
        {
            var startAddress = await _geocodingService.GetShortAddressAsync(
                drive.StartLatitude.Value, drive.StartLongitude.Value, cancellationToken);

            if (!string.IsNullOrEmpty(startAddress))
            {
                drive.StartAddress = startAddress;
                updated = true;
                _logger.LogDebug("Set start address for drive {DriveId}: {Address}", driveId, startAddress);
            }
        }

        // Geocode end address if missing and we have coordinates
        if (string.IsNullOrEmpty(drive.EndAddress) && drive.EndLatitude.HasValue && drive.EndLongitude.HasValue)
        {
            var endAddress = await _geocodingService.GetShortAddressAsync(
                drive.EndLatitude.Value, drive.EndLongitude.Value, cancellationToken);

            if (!string.IsNullOrEmpty(endAddress))
            {
                drive.EndAddress = endAddress;
                updated = true;
                _logger.LogDebug("Set end address for drive {DriveId}: {Address}", driveId, endAddress);
            }
        }

        if (updated)
        {
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Updated addresses for drive {DriveId}", driveId);
        }
    }

    /// <summary>
    /// Batch geocode addresses for drives missing them.
    /// Processes up to maxDrives drives, respecting rate limits via natural job scheduling.
    /// </summary>
    [AutomaticRetry(Attempts = 1)]
    [DisableConcurrentExecution(timeoutInSeconds: 3600)]
    [Queue("default")]
    public async Task BackfillAddressesAsync(int maxDrives = 50, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting address backfill for up to {MaxDrives} drives", maxDrives);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        // Find drives that have coordinates but missing addresses
        var drivesNeedingAddresses = await db.Drives
            .Where(d => !d.IsActive) // Only completed drives
            .Where(d =>
                // Missing start address but has coordinates
                (string.IsNullOrEmpty(d.StartAddress) && d.StartLatitude != null && d.StartLongitude != null) ||
                // Missing end address but has coordinates
                (string.IsNullOrEmpty(d.EndAddress) && d.EndLatitude != null && d.EndLongitude != null))
            .OrderByDescending(d => d.StartTime) // Most recent first
            .Take(maxDrives)
            .Select(d => d.Id)
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Found {Count} drives needing address geocoding", drivesNeedingAddresses.Count);

        var processedCount = 0;
        var errorCount = 0;

        foreach (var driveId in drivesNeedingAddresses)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                await GeocodeForDriveAsync(driveId, cancellationToken);
                processedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error geocoding drive {DriveId}", driveId);
                errorCount++;
            }
        }

        _logger.LogInformation(
            "Address backfill complete. Processed: {Processed}, Errors: {Errors}",
            processedCount, errorCount);
    }

    /// <summary>
    /// Enqueues a geocoding job for a drive.
    /// Use this method to trigger geocoding after a drive ends.
    /// </summary>
    public static void EnqueueForDrive(int driveId)
    {
        BackgroundJob.Enqueue<GeocodeAddressJob>(job => job.GeocodeForDriveAsync(driveId, CancellationToken.None));
    }

    /// <summary>
    /// Enqueues a backfill job to process drives missing addresses.
    /// </summary>
    public static void EnqueueBackfill(int maxDrives = 50)
    {
        BackgroundJob.Enqueue<GeocodeAddressJob>(job => job.BackfillAddressesAsync(maxDrives, CancellationToken.None));
    }
}
