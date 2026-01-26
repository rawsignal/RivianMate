using System.Globalization;
using System.IO.Compression;
using System.Text;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using RivianMate.Core.Enums;
using RivianMate.Infrastructure.Data;

namespace RivianMate.Api.Services.Jobs;

/// <summary>
/// Hangfire job that generates CSV exports for user data.
/// Queries data in batches to avoid loading full datasets into memory.
/// </summary>
public class DataExportJob
{
    private readonly IDbContextFactory<RivianMateDbContext> _dbFactory;
    private readonly ILogger<DataExportJob> _logger;

    private const int BatchSize = 1000;

    public DataExportJob(
        IDbContextFactory<RivianMateDbContext> dbFactory,
        ILogger<DataExportJob> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 2)]
    [Queue("default")]
    public async Task ExecuteAsync(int exportId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting data export job for export {ExportId}", exportId);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var export = await db.DataExports.FindAsync(new object[] { exportId }, cancellationToken);
        if (export == null)
        {
            _logger.LogWarning("Export {ExportId} not found", exportId);
            return;
        }

        if (export.Status != ExportStatus.Pending)
        {
            _logger.LogWarning("Export {ExportId} is not in Pending status (was {Status})", exportId, export.Status);
            return;
        }

        export.Status = ExportStatus.Processing;
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            var vehicle = await db.Vehicles
                .AsNoTracking()
                .Where(v => v.Id == export.VehicleId)
                .Select(v => new { v.Name, v.Vin })
                .FirstOrDefaultAsync(cancellationToken);

            var vehicleName = vehicle?.Name ?? vehicle?.Vin ?? "vehicle";
            var safeVehicleName = string.Join("_", vehicleName.Split(Path.GetInvalidFileNameChars()));

            if (export.ExportType == "All")
            {
                await GenerateAllExportAsync(db, export, safeVehicleName, cancellationToken);
            }
            else
            {
                var (csvData, recordCount) = export.ExportType switch
                {
                    "Drives" => await GenerateDrivesCsvAsync(db, export.VehicleId, cancellationToken),
                    "Charging" => await GenerateChargingCsvAsync(db, export.VehicleId, cancellationToken),
                    "BatteryHealth" => await GenerateBatteryHealthCsvAsync(db, export.VehicleId, cancellationToken),
                    _ => throw new InvalidOperationException($"Unknown export type: {export.ExportType}")
                };

                export.FileData = csvData;
                export.FileName = $"rivianmate_{safeVehicleName}_{export.ExportType.ToLowerInvariant()}_{export.CreatedAt:yyyyMMdd}.csv";
                export.RecordCount = recordCount;
            }

            export.FileSizeBytes = export.FileData?.Length ?? 0;
            export.Status = ExportStatus.Completed;
            export.CompletedAt = DateTime.UtcNow;

            _logger.LogInformation(
                "Export {ExportId} completed: {RecordCount} records, {FileSize} bytes",
                exportId, export.RecordCount, export.FileSizeBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export {ExportId} failed", exportId);
            export.Status = ExportStatus.Failed;
            export.ErrorMessage = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
            export.CompletedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task GenerateAllExportAsync(
        RivianMateDbContext db, Core.Entities.DataExport export, string vehicleName, CancellationToken ct)
    {
        var totalRecords = 0;

        using var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Drives CSV
            var (drivesData, drivesCount) = await GenerateDrivesCsvAsync(db, export.VehicleId, ct);
            var drivesEntry = archive.CreateEntry("drives.csv", CompressionLevel.Optimal);
            using (var entryStream = drivesEntry.Open())
                await entryStream.WriteAsync(drivesData, ct);
            totalRecords += drivesCount;

            // Charging CSV
            var (chargingData, chargingCount) = await GenerateChargingCsvAsync(db, export.VehicleId, ct);
            var chargingEntry = archive.CreateEntry("charging.csv", CompressionLevel.Optimal);
            using (var entryStream = chargingEntry.Open())
                await entryStream.WriteAsync(chargingData, ct);
            totalRecords += chargingCount;

            // Battery Health CSV
            var (batteryData, batteryCount) = await GenerateBatteryHealthCsvAsync(db, export.VehicleId, ct);
            var batteryEntry = archive.CreateEntry("battery_health.csv", CompressionLevel.Optimal);
            using (var entryStream = batteryEntry.Open())
                await entryStream.WriteAsync(batteryData, ct);
            totalRecords += batteryCount;
        }

        export.FileData = zipStream.ToArray();
        export.FileName = $"rivianmate_{vehicleName}_all_{export.CreatedAt:yyyyMMdd}.zip";
        export.RecordCount = totalRecords;
    }

    private async Task<(byte[] Data, int RecordCount)> GenerateDrivesCsvAsync(
        RivianMateDbContext db, int vehicleId, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await using var writer = new StreamWriter(ms, Encoding.UTF8, leaveOpen: true);

        await writer.WriteLineAsync(
            "StartTime,EndTime,DistanceMiles,StartBatteryLevel,EndBatteryLevel,EnergyUsedKwh," +
            "EfficiencyMilesPerKwh,EfficiencyWhPerMile,MaxSpeedMph,AverageSpeedMph," +
            "StartAddress,EndAddress,StartLatitude,StartLongitude,EndLatitude,EndLongitude," +
            "StartOdometer,EndOdometer,StartRangeEstimate,EndRangeEstimate," +
            "StartElevation,EndElevation,ElevationGain,AverageTemperature,DriveMode,DriverName,WheelConfig");

        var recordCount = 0;
        var skip = 0;

        while (true)
        {
            var batch = await db.Drives
                .AsNoTracking()
                .Where(d => d.VehicleId == vehicleId && !d.IsActive)
                .OrderBy(d => d.StartTime)
                .Skip(skip)
                .Take(BatchSize)
                .ToListAsync(ct);

            if (batch.Count == 0)
                break;

            foreach (var d in batch)
            {
                await writer.WriteLineAsync(string.Join(",",
                    CsvField(d.StartTime.ToString("o")),
                    CsvField(d.EndTime?.ToString("o")),
                    CsvField(d.DistanceMiles),
                    CsvField(d.StartBatteryLevel),
                    CsvField(d.EndBatteryLevel),
                    CsvField(d.EnergyUsedKwh),
                    CsvField(d.EfficiencyMilesPerKwh),
                    CsvField(d.EfficiencyWhPerMile),
                    CsvField(d.MaxSpeedMph),
                    CsvField(d.AverageSpeedMph),
                    CsvField(d.StartAddress),
                    CsvField(d.EndAddress),
                    CsvField(d.StartLatitude),
                    CsvField(d.StartLongitude),
                    CsvField(d.EndLatitude),
                    CsvField(d.EndLongitude),
                    CsvField(d.StartOdometer),
                    CsvField(d.EndOdometer),
                    CsvField(d.StartRangeEstimate),
                    CsvField(d.EndRangeEstimate),
                    CsvField(d.StartElevation),
                    CsvField(d.EndElevation),
                    CsvField(d.ElevationGain),
                    CsvField(d.AverageTemperature),
                    CsvField(d.DriveMode),
                    CsvField(d.DriverName),
                    CsvField(d.WheelConfig)));
            }

            recordCount += batch.Count;
            skip += BatchSize;

            if (batch.Count < BatchSize)
                break;
        }

        await writer.FlushAsync(ct);
        return (ms.ToArray(), recordCount);
    }

    private async Task<(byte[] Data, int RecordCount)> GenerateChargingCsvAsync(
        RivianMateDbContext db, int vehicleId, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await using var writer = new StreamWriter(ms, Encoding.UTF8, leaveOpen: true);

        await writer.WriteLineAsync(
            "StartTime,EndTime,StartBatteryLevel,EndBatteryLevel,ChargeLimit,EnergyAddedKwh," +
            "PeakPowerKw,AveragePowerKw,StartRangeEstimate,EndRangeEstimate,RangeAdded," +
            "ChargeType,Latitude,Longitude,LocationName,IsHomeCharging,Cost," +
            "OdometerAtStart,TemperatureAtStart,CalculatedCapacityKwh,CapacityConfidence");

        var recordCount = 0;
        var skip = 0;

        while (true)
        {
            var batch = await db.ChargingSessions
                .AsNoTracking()
                .Where(c => c.VehicleId == vehicleId && !c.IsActive)
                .OrderBy(c => c.StartTime)
                .Skip(skip)
                .Take(BatchSize)
                .ToListAsync(ct);

            if (batch.Count == 0)
                break;

            foreach (var c in batch)
            {
                await writer.WriteLineAsync(string.Join(",",
                    CsvField(c.StartTime.ToString("o")),
                    CsvField(c.EndTime?.ToString("o")),
                    CsvField(c.StartBatteryLevel),
                    CsvField(c.EndBatteryLevel),
                    CsvField(c.ChargeLimit),
                    CsvField(c.EnergyAddedKwh),
                    CsvField(c.PeakPowerKw),
                    CsvField(c.AveragePowerKw),
                    CsvField(c.StartRangeEstimate),
                    CsvField(c.EndRangeEstimate),
                    CsvField(c.RangeAdded),
                    CsvField(c.ChargeType.ToString()),
                    CsvField(c.Latitude),
                    CsvField(c.Longitude),
                    CsvField(c.LocationName),
                    CsvField(c.IsHomeCharging),
                    CsvField(c.Cost),
                    CsvField(c.OdometerAtStart),
                    CsvField(c.TemperatureAtStart),
                    CsvField(c.CalculatedCapacityKwh),
                    CsvField(c.CapacityConfidence)));
            }

            recordCount += batch.Count;
            skip += BatchSize;

            if (batch.Count < BatchSize)
                break;
        }

        await writer.FlushAsync(ct);
        return (ms.ToArray(), recordCount);
    }

    private async Task<(byte[] Data, int RecordCount)> GenerateBatteryHealthCsvAsync(
        RivianMateDbContext db, int vehicleId, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await using var writer = new StreamWriter(ms, Encoding.UTF8, leaveOpen: true);

        await writer.WriteLineAsync(
            "Timestamp,Odometer,ReportedCapacityKwh,OriginalCapacityKwh,HealthPercent," +
            "CapacityLostKwh,DegradationPercent,SmoothedCapacityKwh,SmoothedHealthPercent," +
            "ReadingConfidence,StateOfCharge,Temperature," +
            "DegradationRatePer10kMiles,ProjectedHealthAt100kMiles,ProjectedMilesTo70Percent");

        var recordCount = 0;
        var skip = 0;

        while (true)
        {
            var batch = await db.BatteryHealthSnapshots
                .AsNoTracking()
                .Where(b => b.VehicleId == vehicleId)
                .OrderBy(b => b.Timestamp)
                .Skip(skip)
                .Take(BatchSize)
                .ToListAsync(ct);

            if (batch.Count == 0)
                break;

            foreach (var b in batch)
            {
                await writer.WriteLineAsync(string.Join(",",
                    CsvField(b.Timestamp.ToString("o")),
                    CsvField(b.Odometer),
                    CsvField(b.ReportedCapacityKwh),
                    CsvField(b.OriginalCapacityKwh),
                    CsvField(b.HealthPercent),
                    CsvField(b.CapacityLostKwh),
                    CsvField(b.DegradationPercent),
                    CsvField(b.SmoothedCapacityKwh),
                    CsvField(b.SmoothedHealthPercent),
                    CsvField(b.ReadingConfidence),
                    CsvField(b.StateOfCharge),
                    CsvField(b.Temperature),
                    CsvField(b.DegradationRatePer10kMiles),
                    CsvField(b.ProjectedHealthAt100kMiles),
                    CsvField(b.ProjectedMilesTo70Percent)));
            }

            recordCount += batch.Count;
            skip += BatchSize;

            if (batch.Count < BatchSize)
                break;
        }

        await writer.FlushAsync(ct);
        return (ms.ToArray(), recordCount);
    }

    private static string CsvField(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    private static string CsvField(double? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? "";

    private static string CsvField(double value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static string CsvField(bool? value) =>
        value?.ToString() ?? "";

    /// <summary>
    /// Enqueues an export job for the given export record.
    /// </summary>
    public static void Enqueue(int exportId)
    {
        BackgroundJob.Enqueue<DataExportJob>(job => job.ExecuteAsync(exportId, CancellationToken.None));
    }
}
