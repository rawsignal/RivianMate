using RivianMate.Core.Enums;
using RivianMate.Core.Interfaces;

namespace RivianMate.Core.Entities;

/// <summary>
/// Represents a user-requested data export (CSV/ZIP).
/// File data is stored as a BLOB for stateless deployment.
/// </summary>
public class DataExport : IUserOwnedEntity
{
    public int Id { get; set; }

    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public int VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;

    /// <summary>
    /// Type of export: "Drives", "Charging", "BatteryHealth", or "All"
    /// </summary>
    public string ExportType { get; set; } = string.Empty;

    /// <summary>
    /// Current status of the export job
    /// </summary>
    public ExportStatus Status { get; set; } = ExportStatus.Pending;

    /// <summary>
    /// Random token for secure download URL (non-sequential)
    /// </summary>
    public Guid DownloadToken { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Generated file data (CSV or ZIP)
    /// </summary>
    public byte[]? FileData { get; set; }

    /// <summary>
    /// Name of the generated file
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long? FileSizeBytes { get; set; }

    /// <summary>
    /// Number of data records exported
    /// </summary>
    public int? RecordCount { get; set; }

    /// <summary>
    /// Error message if the export failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddHours(24);
    public DateTime? DownloadedAt { get; set; }
}
