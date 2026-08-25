using Microsoft.Extensions.Hosting;
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
    IHostEnvironment env,
    ILogger<ValueScreenerSchedulerService> logger) : BackgroundService
{
    private static readonly string[] EasternTzIds = ["Eastern Standard Time", "America/New_York"];
    private static readonly TimeSpan ConfigCacheTtl = TimeSpan.FromMinutes(10);

    private ValueScreenerScheduleConfig? _cachedConfig;
    private DateTime _cachedConfigAt = DateTime.MinValue;

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
        var tz = GetEasternTz();
        if (tz is null)
        {
            logger.LogWarning("[ValueScreenerScheduler] Eastern timezone not found — cannot schedule.");
            return;
        }

        var nowEt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);

        // Skip weekends without touching the DB at all — dev bypasses so local testing works anytime.
        if (!env.IsDevelopment() && nowEt.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return;

        var cfg = await GetConfigCachedAsync(ct);
        if (!cfg.Enabled) return;

        if (!TimeSpan.TryParse(cfg.ScheduledTimeEt, out var scheduledTime)) return;

        // Fire when current time is within a 2-minute window of the scheduled time.
        // Refresh from the DB here (bypassing the cache) so Configuration-page edits apply promptly.
        var diff = nowEt.TimeOfDay - scheduledTime;
        bool inWindow = diff.TotalMinutes >= 0 && diff.TotalMinutes < 2;
        if (!inWindow) return;
        cfg = await RefreshConfigCacheAsync(ct);
        if (!cfg.Enabled) return;

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
                cfg.LastPortfolioRunAt = DateTime.UtcNow;
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
                cfg.LastWatchlistRunAt = DateTime.UtcNow;
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

    /// <summary>Returns the schedule config, refreshing from the DB at most every <see cref="ConfigCacheTtl"/> instead of every 2-minute tick.</summary>
    private async Task<ValueScreenerScheduleConfig> GetConfigCachedAsync(CancellationToken ct)
    {
        if (_cachedConfig is null || DateTime.UtcNow - _cachedConfigAt > ConfigCacheTtl)
            return await RefreshConfigCacheAsync(ct);
        return _cachedConfig;
    }

    private async Task<ValueScreenerScheduleConfig> RefreshConfigCacheAsync(CancellationToken ct)
    {
        _cachedConfig = await persistence.GetOrCreateScheduleConfigAsync(ct);
        _cachedConfigAt = DateTime.UtcNow;
        return _cachedConfig;
    }
}
