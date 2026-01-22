namespace RivianMate.Api.Configuration;

/// <summary>
/// Configuration for automatic data retention and cleanup.
/// </summary>
public class DataRetentionConfiguration
{
    public const string SectionName = "DataRetention";

    /// <summary>
    /// Whether data retention cleanup is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Cron expression for when to run the cleanup job.
    /// Default: daily at 3 AM.
    /// </summary>
    public string Schedule { get; set; } = "0 3 * * *";

    /// <summary>
    /// Maximum number of records to delete per table per run.
    /// Prevents long-running operations. Set to 0 for unlimited.
    /// </summary>
    public int MaxRecordsPerRun { get; set; } = 10000;

    /// <summary>
    /// Table-specific retention settings.
    /// Key is the table name, value is the retention configuration.
    /// </summary>
    public Dictionary<string, TableRetentionConfig> Tables { get; set; } = new();
}

/// <summary>
/// Retention configuration for a specific table.
/// </summary>
public class TableRetentionConfig
{
    /// <summary>
    /// Number of days to retain data. Records older than this will be deleted.
    /// </summary>
    public int RetentionDays { get; set; }

    /// <summary>
    /// The column name containing the timestamp to check against.
    /// Defaults to "Timestamp" if not specified.
    /// </summary>
    public string TimestampColumn { get; set; } = "Timestamp";

    /// <summary>
    /// Whether cleanup is enabled for this table.
    /// Allows temporarily disabling cleanup for specific tables.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
