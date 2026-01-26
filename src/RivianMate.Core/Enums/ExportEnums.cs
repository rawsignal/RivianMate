namespace RivianMate.Core.Enums;

/// <summary>
/// Status of a data export job
/// </summary>
public enum ExportStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3,
    Expired = 4
}
