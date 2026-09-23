using Server.Application.Common.Interfaces;

namespace Server.Api.Services;

/// <summary>
/// Keeps the cached daily closes current in the background: shortly after startup, then hourly (so a new
/// UTC day's close is fetched soon after midnight), instead of on the first chart request of the day.
/// Providers receive only symbols and dates.
/// </summary>
public sealed class PriceCacheWarmupService(
    IServiceScopeFactory scopeFactory,
    ILogger<PriceCacheWarmupService> logger) : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(StartupDelay, stoppingToken); }
        catch (TaskCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var started = DateTime.UtcNow;
                await scope.ServiceProvider.GetRequiredService<IWealthService>().WarmPriceCacheAsync(stoppingToken);
                logger.LogInformation("Price cache topped up in {Ms} ms", (int)(DateTime.UtcNow - started).TotalMilliseconds);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Price cache top-up failed");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }
}
