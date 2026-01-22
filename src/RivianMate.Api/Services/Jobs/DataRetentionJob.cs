using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RivianMate.Api.Configuration;
using RivianMate.Infrastructure.Data;

namespace RivianMate.Api.Services.Jobs;

/// <summary>
/// Hangfire job that performs data retention cleanup based on configuration.
/// Deletes records older than the configured retention period for each table.
/// </summary>
public class DataRetentionJob
{
    private readonly RivianMateDbContext _db;
    private readonly DataRetentionConfiguration _config;
    private readonly ILogger<DataRetentionJob> _logger;

    public DataRetentionJob(
        RivianMateDbContext db,
        IOptions<DataRetentionConfiguration> config,
        ILogger<DataRetentionJob> logger)
    {
        _db = db;
        _config = config.Value;
        _logger = logger;
    }

    /// <summary>
    /// Execute the data retention cleanup job.
    /// </summary>
    [AutomaticRetry(Attempts = 1)]
    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!_config.Enabled)
        {
            _logger.LogDebug("Data retention cleanup is disabled");
            return;
        }

        if (_config.Tables.Count == 0)
        {
            _logger.LogDebug("No tables configured for data retention cleanup");
            return;
        }

        _logger.LogInformation("Starting data retention cleanup for {TableCount} table(s)", _config.Tables.Count);

        var totalDeleted = 0;

        foreach (var (tableName, tableConfig) in _config.Tables)
        {
            if (!tableConfig.Enabled)
            {
                _logger.LogDebug("Skipping disabled table: {TableName}", tableName);
                continue;
            }

            try
            {
                var deleted = await CleanupTableAsync(tableName, tableConfig, cancellationToken);
                totalDeleted += deleted;

                if (deleted > 0)
                {
                    _logger.LogInformation(
                        "Cleaned up {DeletedCount} records from {TableName} (retention: {RetentionDays} days)",
                        deleted, tableName, tableConfig.RetentionDays);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up table {TableName}", tableName);
            }
        }

        _logger.LogInformation("Data retention cleanup complete. Total records deleted: {TotalDeleted}", totalDeleted);
    }

    private async Task<int> CleanupTableAsync(
        string tableName,
        TableRetentionConfig config,
        CancellationToken cancellationToken)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-config.RetentionDays);
        var timestampColumn = SanitizeIdentifier(config.TimestampColumn);
        var sanitizedTableName = SanitizeIdentifier(tableName);

        // Determine if we're using PostgreSQL or SQLite
        var isPostgres = _db.Database.IsNpgsql();

        string sql;
        int deleted;

        if (_config.MaxRecordsPerRun > 0)
        {
            // Batch delete with limit
            if (isPostgres)
            {
                sql = $@"
                    DELETE FROM ""{sanitizedTableName}""
                    WHERE ""{timestampColumn}"" < @p0
                    AND ""Id"" IN (
                        SELECT ""Id"" FROM ""{sanitizedTableName}""
                        WHERE ""{timestampColumn}"" < @p0
                        LIMIT @p1
                    )";
                deleted = await _db.Database.ExecuteSqlRawAsync(
                    sql, new object[] { cutoffDate, _config.MaxRecordsPerRun }, cancellationToken);
            }
            else
            {
                // SQLite syntax
                sql = $@"
                    DELETE FROM ""{sanitizedTableName}""
                    WHERE ""Id"" IN (
                        SELECT ""Id"" FROM ""{sanitizedTableName}""
                        WHERE ""{timestampColumn}"" < @p0
                        LIMIT @p1
                    )";
                deleted = await _db.Database.ExecuteSqlRawAsync(
                    sql, new object[] { cutoffDate, _config.MaxRecordsPerRun }, cancellationToken);
            }
        }
        else
        {
            // Delete all matching records (no limit)
            sql = $@"DELETE FROM ""{sanitizedTableName}"" WHERE ""{timestampColumn}"" < @p0";
            deleted = await _db.Database.ExecuteSqlRawAsync(
                sql, new object[] { cutoffDate }, cancellationToken);
        }

        return deleted;
    }

    /// <summary>
    /// Sanitize an identifier to prevent SQL injection.
    /// Only allows alphanumeric characters and underscores.
    /// </summary>
    private static string SanitizeIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            throw new ArgumentException("Identifier cannot be empty", nameof(identifier));

        // Only allow alphanumeric and underscore
        var sanitized = new string(identifier.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());

        if (sanitized.Length == 0 || sanitized != identifier)
            throw new ArgumentException($"Invalid identifier: {identifier}", nameof(identifier));

        return sanitized;
    }
}
