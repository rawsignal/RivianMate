namespace RivianMate.Core.Entities;

/// <summary>
/// Record of a broadcast email sent to all users.
/// </summary>
public class BroadcastEmail
{
    public int Id { get; set; }

    /// <summary>
    /// The admin user who sent this broadcast.
    /// </summary>
    public Guid AdminUserId { get; set; }

    /// <summary>
    /// Email subject line.
    /// </summary>
    public string Subject { get; set; } = "";

    /// <summary>
    /// Email message body (plain text, line breaks preserved).
    /// </summary>
    public string Message { get; set; } = "";

    /// <summary>
    /// Current status of the broadcast.
    /// </summary>
    public BroadcastStatus Status { get; set; } = BroadcastStatus.Pending;

    /// <summary>
    /// Total number of recipients when broadcast was initiated.
    /// </summary>
    public int TotalRecipients { get; set; }

    /// <summary>
    /// Number of emails successfully sent.
    /// </summary>
    public int SentCount { get; set; }

    /// <summary>
    /// Number of emails that failed to send.
    /// </summary>
    public int FailedCount { get; set; }

    /// <summary>
    /// When the broadcast was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the broadcast finished processing (success or failure).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Navigation property to admin user.
    /// </summary>
    public ApplicationUser? AdminUser { get; set; }
}

/// <summary>
/// Status of a broadcast email job.
/// </summary>
public enum BroadcastStatus
{
    Pending,
    InProgress,
    Completed,
    Failed
}
