using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;

namespace PortfolioManager.Api.Services;

/// <summary>
/// Closes the "silently missed with zero trace" gap discovered on 2026-09-15 (the Scheduled Task's
/// primary trigger never fired and left no log/DB row at all for that trading day). Independent of
/// the Scheduled Task and the orchestrator itself — runs entirely inside the always-on backend
/// process, so it still catches a miss even when the Task Scheduler trigger, wake timer, or trigger
/// script is the thing that failed. Polls every 10 minutes; once per trading day, after
/// MissedRunAlertTimeEt (default 17:30 ET — later than every other business-time window), checks
/// whether ANY Scheduled or FixMissingData run already succeeded (fully or partially) for today's
/// trading date, and sends one alert email via the existing durable, dedup-by-operationKey
/// AutomationRunNotificationService if not.
/// </summary>
public sealed class AutomationMissedRunWatchdogService(
    IServiceScopeFactory scopeFactory,
    AutomationRuntimeConfig automationConfig,
    ILogger<AutomationMissedRunWatchdogService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("[AutomationWatchdog] Background service starting.");
        await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await CheckAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "[AutomationWatchdog] Check failed."); }

            try { await Task.Delay(PollInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task CheckAsync(CancellationToken ct)
    {
        if (!automationConfig.Enabled) return;

        var tz = MarketHoursGate.GetEasternTimeZone();
        if (tz is null) return;
        var nowEt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);

        var alertTimeEt = automationConfig.MissedRunAlertTimeEt;
        if (!TimeSpan.TryParse(alertTimeEt, out var alertTime)) alertTime = new TimeSpan(17, 30, 0);
        if (nowEt.TimeOfDay < alertTime) return;

        await using var scope = scopeFactory.CreateAsyncScope();
        var tradingSessionGuard = scope.ServiceProvider.GetRequiredService<ITradingSessionGuard>();
        if (!await tradingSessionGuard.IsTodayATradingDayAsync(ct)) return; // weekends/holidays never alert

        var tradingDate = nowEt.ToString("yyyy-MM-dd");
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasGoodRun = await db.AutomationRunLogs.AsNoTracking().AnyAsync(
            r => r.TradingDate == tradingDate
                && (r.TriggerType == "Scheduled" || r.TriggerType == "FixMissingData")
                && (r.OverallStatus == AutomationStatuses.Success || r.OverallStatus == AutomationStatuses.PartialSuccess),
            ct);
        if (hasGoodRun) return;

        var notifications = scope.ServiceProvider.GetRequiredService<IAutomationRunNotificationService>();
        var subject = $"Portfolio Manager - EOD Automation MISSING for {tradingDate}";
        var html = $"""
            <!DOCTYPE html><html><body style="font-family:Segoe UI,Arial,sans-serif;color:#263238">
            <h2>No successful EOD automation run recorded for {tradingDate}</h2>
            <p>By {alertTimeEt} ET, no <strong>Scheduled</strong> or <strong>Fix Missing Data</strong> run with a
            Success/PartialSuccess outcome was found for today's trading date. Today's EOD signals, portfolio
            snapshot, and Value Screener data may be missing.</p>
            <p>Open Configuration &rarr; Automation and click <strong>Fix Missing Data</strong> to recover it now.</p>
            <p style="color:#607d8b">This is a watchdog alert — sent at most once per trading day (deduplicated by
            AutomationNotificationRecords, same mechanism as every other Automation email).</p>
            </body></html>
            """;
        await notifications.SendOperationSummaryAsync($"missedrun:{tradingDate}", "MissedRunWatchdog", subject, html, ct);
        logger.LogWarning(
            "[AutomationWatchdog] No successful run found for {TradingDate} by {AlertTime} ET — alert sent.",
            tradingDate, alertTimeEt);
    }
}
