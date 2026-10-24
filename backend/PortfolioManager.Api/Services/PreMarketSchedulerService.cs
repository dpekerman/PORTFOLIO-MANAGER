using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

/// <summary>
/// Fires the pre-market run once per day at the configured Toronto time. It sleeps between checks (no polling of
/// external services), is idempotent through the REAL run key and does nothing unless scheduling is enabled.
/// </summary>
public sealed class PreMarketSchedulerService(IServiceScopeFactory scopes, ILogger<PreMarketSchedulerService> logger) : BackgroundService
{
    public static bool ShouldRun(PreMarketAgentConfig cfg, DateTime nowUtc, bool realRunExistsToday, bool isTradingDay)
    {
        if (!cfg.ScheduleEnabled || cfg.AsOfDate is not null) return false;
        var today = PreMarketAgentService.TorontoToday(nowUtc);
        if (cfg.FirstRunDate is { } first && today < first) return false;
        if (realRunExistsToday || !isTradingDay) return false;
        var start = PreMarketAgentService.TorontoToUtc(today, cfg.StartTimeLocal);
        var deadline = PreMarketAgentService.TorontoToUtc(today, cfg.DeadlineLocal);
        // Catch-up window: a late start still runs (report is saved even if the email cutoff has passed).
        return nowUtc >= start && nowUtc < deadline.AddHours(4);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<IPreMarketAgentService>();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var cfg = await svc.GetConfigAsync(stoppingToken);
                var today = PreMarketAgentService.TorontoToday();
                var key = $"{today:yyyy-MM-dd}|REAL";
                var exists = await db.PreMarketAgentRuns.AnyAsync(r => r.RunKey == key && r.Status != "FAILED", stoppingToken);
                var trading = TradingCalendar.IsOpen(MarketExchange.Tsx, today) || TradingCalendar.IsOpen(MarketExchange.Us, today);
                if (ShouldRun(cfg, DateTime.UtcNow, exists, trading))
                {
                    logger.LogInformation("Starting scheduled pre-market run for {Date}", today);
                    await svc.RunAsync(new PreMarketRunOptions(today, DryRun: false, SendEmail: true, Force: true, IsTest: false), stoppingToken);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.LogError(ex, "Pre-market scheduler tick failed"); }
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
