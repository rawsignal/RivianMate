using Hangfire;
using Microsoft.EntityFrameworkCore;
using RivianMate.Core.Entities;
using RivianMate.Infrastructure.Data;

namespace RivianMate.Api.Services.Email;

/// <summary>
/// Hangfire job for sending broadcast emails to all users.
/// </summary>
public class BroadcastEmailJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IBackgroundJobClient _jobClient;
    private readonly ILogger<BroadcastEmailJob> _logger;

    public BroadcastEmailJob(
        IServiceScopeFactory scopeFactory,
        IBackgroundJobClient jobClient,
        ILogger<BroadcastEmailJob> logger)
    {
        _scopeFactory = scopeFactory;
        _jobClient = jobClient;
        _logger = logger;
    }

    /// <summary>
    /// Executes the broadcast email job.
    /// </summary>
    /// <param name="broadcastId">ID of the BroadcastEmail record</param>
    [DisableConcurrentExecution(timeoutInSeconds: 3600)]
    [AutomaticRetry(Attempts = 0)] // Don't retry the whole broadcast, individual emails have their own retries
    [Queue("default")]
    public async Task ExecuteAsync(int broadcastId)
    {
        _logger.LogInformation("Starting broadcast email job: BroadcastId={BroadcastId}", broadcastId);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RivianMateDbContext>();

        // Get the broadcast record
        var broadcast = await db.BroadcastEmails.FindAsync(broadcastId);
        if (broadcast == null)
        {
            _logger.LogError("Broadcast not found: BroadcastId={BroadcastId}", broadcastId);
            return;
        }

        // Check if already processing or completed
        if (broadcast.Status != BroadcastStatus.Pending)
        {
            _logger.LogWarning("Broadcast already processed: BroadcastId={BroadcastId}, Status={Status}", broadcastId, broadcast.Status);
            return;
        }

        try
        {
            // Mark as in progress
            broadcast.Status = BroadcastStatus.InProgress;
            await db.SaveChangesAsync();

            // Get all users with verified emails
            var users = await db.Users
                .Where(u => u.Email != null && u.EmailConfirmed)
                .Select(u => new { u.Id, u.Email, u.DisplayName })
                .ToListAsync();

            // If no email confirmation required, get all users with emails
            if (!users.Any())
            {
                users = await db.Users
                    .Where(u => u.Email != null)
                    .Select(u => new { u.Id, u.Email, u.DisplayName })
                    .ToListAsync();
            }

            broadcast.TotalRecipients = users.Count;
            await db.SaveChangesAsync();

            _logger.LogInformation("Broadcasting to {Count} recipients: BroadcastId={BroadcastId}", users.Count, broadcastId);

            // Enqueue individual email jobs for each user
            foreach (var user in users)
            {
                var request = new EmailJobRequest
                {
                    To = user.Email!,
                    Trigger = EmailTriggers.AdminBroadcast,
                    Template = "AdminBroadcast",
                    SubjectTemplate = broadcast.Subject,
                    Tokens = new Dictionary<string, string>
                    {
                        ["Subject"] = broadcast.Subject,
                        ["Message"] = broadcast.Message,
                        ["UserName"] = user.DisplayName ?? "there"
                    },
                    UserId = user.Id
                };

                _jobClient.Enqueue<SendEmailJob>(job => job.ExecuteAsync(request));
            }

            // Mark as completed
            broadcast.Status = BroadcastStatus.Completed;
            broadcast.SentCount = users.Count; // Actual count updated by individual jobs
            broadcast.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            _logger.LogInformation("Broadcast completed: BroadcastId={BroadcastId}, Recipients={Count}", broadcastId, users.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Broadcast failed: BroadcastId={BroadcastId}", broadcastId);

            broadcast.Status = BroadcastStatus.Failed;
            broadcast.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            throw;
        }
    }
}
