using Microsoft.EntityFrameworkCore;
using RivianMate.Core.Entities;
using RivianMate.Core.Enums;
using RivianMate.Infrastructure.Data;

namespace RivianMate.Api.Services;

/// <summary>
/// Orchestrates data export requests, listing, retrieval, and rate limiting.
/// </summary>
public class DataExportService
{
    private readonly RivianMateDbContext _db;
    private readonly ILogger<DataExportService> _logger;

    private const int MaxPendingExports = 3;
    private static readonly TimeSpan CooldownPeriod = TimeSpan.FromMinutes(5);

    public DataExportService(RivianMateDbContext db, ILogger<DataExportService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new export request after checking rate limits.
    /// Returns the created export or null if rate limited.
    /// </summary>
    public async Task<(DataExport? Export, string? Error)> RequestExportAsync(
        Guid userId, int vehicleId, string exportType, CancellationToken ct = default)
    {
        // Validate export type
        if (exportType is not ("Drives" or "Charging" or "BatteryHealth" or "All"))
            return (null, "Invalid export type.");

        // Check rate limits
        var pendingCount = await _db.DataExports
            .CountAsync(e => e.UserId == userId
                && (e.Status == ExportStatus.Pending || e.Status == ExportStatus.Processing), ct);

        if (pendingCount >= MaxPendingExports)
            return (null, $"You have {pendingCount} exports in progress. Please wait for them to complete.");

        var lastExport = await _db.DataExports
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (lastExport != null && DateTime.UtcNow - lastExport.CreatedAt < CooldownPeriod)
        {
            var remaining = CooldownPeriod - (DateTime.UtcNow - lastExport.CreatedAt);
            return (null, $"Please wait {remaining.Minutes}m {remaining.Seconds}s before requesting another export.");
        }

        var export = new DataExport
        {
            UserId = userId,
            VehicleId = vehicleId,
            ExportType = exportType,
            Status = ExportStatus.Pending,
            DownloadToken = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        };

        _db.DataExports.Add(export);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Export {ExportId} requested by user {UserId} for vehicle {VehicleId}, type {ExportType}",
            export.Id, userId, vehicleId, exportType);

        return (export, null);
    }

    /// <summary>
    /// Gets all exports for a user, ordered by most recent first.
    /// </summary>
    public async Task<List<DataExport>> GetExportsForUserAsync(Guid userId, CancellationToken ct = default)
    {
        return await _db.DataExports
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.CreatedAt)
            .Take(20)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Retrieves a completed export by download token, validating ownership and expiration.
    /// </summary>
    public async Task<DataExport?> GetExportForDownloadAsync(Guid downloadToken, Guid userId, CancellationToken ct = default)
    {
        var export = await _db.DataExports
            .FirstOrDefaultAsync(e => e.DownloadToken == downloadToken && e.UserId == userId, ct);

        if (export == null)
            return null;

        if (export.Status != ExportStatus.Completed)
            return null;

        if (DateTime.UtcNow > export.ExpiresAt)
            return null;

        // Mark as downloaded
        export.DownloadedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return export;
    }
}
