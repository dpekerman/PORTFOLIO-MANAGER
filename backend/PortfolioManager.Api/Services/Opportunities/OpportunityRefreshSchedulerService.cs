using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services.Opportunities;

/// <summary>
/// Runs the Trading Opportunities refresh once on each configured market day, at the configured Eastern time.
/// A day counts only once its session is "completed" (see <see cref="TradingCalendar.EvidenceGrace"/>), so the new snapshot is
/// scored against today's session rather than the previous one. Missed ticks catch up later the same day.
/// </summary>
public sealed class OpportunityRefreshSchedulerService(
    IServiceScopeFactory scopeFactory,
    OpportunityScheduleConfig config,
    ILogger<OpportunityRefreshSchedulerService> logger) : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunIfDueAsync(DateTime.UtcNow, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "[OpportunityScheduler] Schedule check failed."); }
            await Task.Delay(Tick, stoppingToken);
        }
    }

    /// <summary>Pure due-check, kept static so it can be unit tested.</summary>
    internal static bool IsDue(OpportunityScheduleConfig cfg, DateTime utcNow)
    {
        if (!cfg.Enabled || !TimeOnly.TryParseExact(cfg.TimeEt, "HH:mm", out var at)) return false;
        var nowEt = TradingCalendar.TorontoNow(utcNow);
        var today = DateOnly.FromDateTime(nowEt);
        if (!cfg.Days.Contains(today.DayOfWeek) || !TradingCalendar.IsTradingDay(today, MarketExchange.Tsx)) return false;
        if (TimeOnly.FromDateTime(nowEt) < at) return false;
        if (cfg.LastRunDateEt == today.ToString("yyyy-MM-dd")) return false;
        return TradingCalendar.LastCompletedSession(utcNow, MarketExchange.Tsx) == today;
    }

    private async Task RunIfDueAsync(DateTime utcNow, CancellationToken ct)
    {
        if (!IsDue(config, utcNow)) return;
        var dateEt = DateOnly.FromDateTime(TradingCalendar.TorontoNow(utcNow)).ToString("yyyy-MM-dd");
        logger.LogInformation("[OpportunityScheduler] Scheduled Trading Opportunities refresh firing for {Date}.", dateEt);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userIds = await db.OpportunityRefreshStates.AsNoTracking().Select(s => s.UserId).ToListAsync(ct);
        if (userIds.Count == 0)
        {
            // No HTTP user here: fall back to the automation owner (first Admin, same rule as the other schedulers).
            var admins = await scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>()
                .GetUsersInRoleAsync("Admin");
            if (admins.FirstOrDefault()?.Id is { } adminId) userIds.Add(adminId);
        }
        if (userIds.Count == 0)
        {
            config.RecordRun(dateEt, "Failed", "No user to refresh opportunities for.");
            return;
        }

        var service = scope.ServiceProvider.GetRequiredService<IOpportunityService>();
        var failures = new List<string>();
        foreach (var userId in userIds)
        {
            try
            {
                var snap = await service.RefreshAsync(userId, ct);
                if (snap.LastAttempt?.Status == "Failed") failures.Add(snap.LastAttempt.ErrorSummary ?? "Refresh failed.");
            }
            catch (OpportunityRefreshInProgressException)
            {
                // A manual refresh is running; leave today unrecorded so the next tick tries again.
                logger.LogInformation("[OpportunityScheduler] Refresh already running; will retry.");
                return;
            }
        }
        config.RecordRun(dateEt, failures.Count == 0 ? "Succeeded" : "Failed", failures.FirstOrDefault());
        logger.LogInformation("[OpportunityScheduler] Scheduled refresh finished: {Status}.", failures.Count == 0 ? "Succeeded" : "Failed");
    }
}
