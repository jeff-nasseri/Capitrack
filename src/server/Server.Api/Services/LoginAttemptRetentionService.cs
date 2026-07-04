using Server.Application.Common.Interfaces;

namespace Server.Api.Services;

/// <summary>
/// Periodically prunes old sign-in audit rows so the <c>LoginAttempts</c> table can't grow without
/// bound (a distributed, IP-rotating attacker would otherwise fill the disk). Runs once at startup
/// and then on a fixed interval, deleting anything past the retention horizon.
/// </summary>
public sealed class LoginAttemptRetentionService(
    IServiceScopeFactory scopeFactory,
    ILogger<LoginAttemptRetentionService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(12);
    private static readonly TimeSpan Retention = TimeSpan.FromDays(90);

    /// <summary>Sweeps expired attempts every <see cref="Interval"/> until the app shuts down.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var attempts = scope.ServiceProvider.GetRequiredService<ILoginAttemptRepository>();
                var removed = await attempts.DeleteOlderThanAsync(DateTime.UtcNow - Retention, stoppingToken);
                if (removed > 0)
                    logger.LogInformation("Pruned {Count} login attempts older than {Days} days", removed, (int)Retention.TotalDays);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Login-attempt retention sweep failed");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }
}
