using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

/// <summary>
/// Background service that runs the Value Screener for Portfolio and Watchlist at a
/// configurable time (default 5:00 PM Eastern) on weekdays and persists results to the DB.
/// The schedule is configurable from the Configuration page.
/// </summary>
public sealed class ValueScreenerSchedulerService(
    IServiceScopeFactory scopeFactory,
    ValueScreenerPersistenceService persistence,
    ILogger<ValueScreenerSchedulerService> logger) : BackgroundService
{
    private static readonly string[] EasternTzIds = ["Eastern Standard Time", "America/New_York"];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("[ValueScreenerScheduler] Background service starting.");
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); // startup delay

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCheckAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "[ValueScreenerScheduler] Error during schedule check.");
            }

            // Check every 2 minutes
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        }

        logger.LogInformation("[ValueScreenerScheduler] Background service stopped.");
    }

    private async Task RunCheckAsync(CancellationToken ct)
    {
        var cfg = await persistence.GetOrCreateScheduleConfigAsync(ct);
        if (!cfg.Enabled) return;

        var tz = GetEasternTz();
        if (tz is null)
        {
            logger.LogWarning("[ValueScreenerScheduler] Eastern timezone not found — cannot schedule.");
            return;
        }

        var nowEt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);

        // Only run on weekdays
        if (nowEt.DayOfWeek == DayOfWeek.Saturday || nowEt.DayOfWeek == DayOfWeek.Sunday) return;

        if (!TimeSpan.TryParse(cfg.ScheduledTimeEt, out var scheduledTime)) return;

        // Fire when current time is within a 2-minute window of the scheduled time
        var diff = nowEt.TimeOfDay - scheduledTime;
        bool inWindow = diff.TotalMinutes >= 0 && diff.TotalMinutes < 2;
        if (!inWindow) return;

        // Avoid re-running if we already ran today
        var etToday = nowEt.Date;
        if (cfg.LastPortfolioRunAt.HasValue &&
            TimeZoneInfo.ConvertTimeFromUtc(cfg.LastPortfolioRunAt.Value, tz).Date == etToday &&
            cfg.LastWatchlistRunAt.HasValue &&
            TimeZoneInfo.ConvertTimeFromUtc(cfg.LastWatchlistRunAt.Value, tz).Date == etToday)
        {
            return;
        }

        logger.LogInformation("[ValueScreenerScheduler] Scheduled run firing at {TimeEt} ET.", nowEt.ToString("HH:mm"));

        using var scope = scopeFactory.CreateScope();
        var screener = scope.ServiceProvider.GetRequiredService<ValueScreenerService>();

        // Run Portfolio
        if (!cfg.LastPortfolioRunAt.HasValue ||
            TimeZoneInfo.ConvertTimeFromUtc(cfg.LastPortfolioRunAt.Value, tz).Date != etToday)
        {
            try
            {
                logger.LogInformation("[ValueScreenerScheduler] Running Portfolio screener...");
                var results = await screener.RunAsync(
                    new ValueScreenerRequest { IncludePortfolio = true, IncludeWatchlist = false }, ct);
                await persistence.SaveAsync("Portfolio", results, ct);
                logger.LogInformation("[ValueScreenerScheduler] Portfolio screener complete ({Count} results).", results.Count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[ValueScreenerScheduler] Portfolio screener failed.");
            }
        }

        // Run Watchlist
        if (!cfg.LastWatchlistRunAt.HasValue ||
            TimeZoneInfo.ConvertTimeFromUtc(cfg.LastWatchlistRunAt.Value, tz).Date != etToday)
        {
            try
            {
                logger.LogInformation("[ValueScreenerScheduler] Running Watchlist screener...");
                var results = await screener.RunAsync(
                    new ValueScreenerRequest { IncludePortfolio = false, IncludeWatchlist = true }, ct);
                await persistence.SaveAsync("Watchlist", results, ct);
                logger.LogInformation("[ValueScreenerScheduler] Watchlist screener complete ({Count} results).", results.Count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[ValueScreenerScheduler] Watchlist screener failed.");
            }
        }
    }

    private static TimeZoneInfo? GetEasternTz()
    {
        foreach (var id in EasternTzIds)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch { /* try next */ }
        }
        return null;
    }
}
