using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Services;

/// <summary>
/// Persists the total portfolio value to the database once per trading day, any time between
/// 4:30 PM and midnight Eastern — the same window in every environment, so a slow start, a
/// missed poll, or an app restart during the evening still produces exactly one EOD snapshot.
/// Calculates: stocks market value + cash + options market value.
/// </summary>
public sealed class PortfolioValueEodBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<PortfolioValueEodBackgroundService> logger) : BackgroundService
{
    private static readonly string[] EasternTzIds = ["Eastern Standard Time", "America/New_York"];
    private static readonly TimeSpan EodWindowStart = new(16, 30, 0);
    private static readonly TimeSpan EodWindowEnd = new(23, 59, 59);
    private static TimeZoneInfo? _easternTz;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("[PortfolioValueEod] Background service starting.");
        await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunCheckAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "[PortfolioValueEod] Check failed."); }

            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        }
    }

    private async Task RunCheckAsync(CancellationToken ct)
    {
        var tz = GetEasternTz();
        if (tz is null) return;

        var nowEt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);

        // Weekends never have a market close — writing a row for Sat/Sun freezes stale
        // Friday quotes and corrupts every later "before this date" baseline lookup.
        if (nowEt.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return;

        // Same EOD window in every environment: wait until 4:30 PM ET so the closing quote
        // settles, then accept any poll up to midnight. ExistsForDateAsync below still
        // guarantees only the first poll in that window ever writes a row for the date.
        if (nowEt.TimeOfDay < EodWindowStart || nowEt.TimeOfDay > EodWindowEnd) return;

        var recordedDate = nowEt.ToString("yyyy-MM-dd");

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var history = scope.ServiceProvider.GetRequiredService<IPortfolioValueHistoryService>();
        var marketData = scope.ServiceProvider.GetRequiredService<IMarketDataProvider>();

        if (await history.ExistsForDateAsync(recordedDate, ct))
        {
            logger.LogDebug("[PortfolioValueEod] Already persisted for {Date}.", recordedDate);
            return;
        }

        logger.LogInformation("[PortfolioValueEod] Persisting portfolio value for {Date}.", recordedDate);

        // ── Stocks market value ───────────────────────────────────────────────
        var portfolioItems = await db.PortfolioItems
            .Where(p => p.TransactionType != "CLOSE")
            .ToListAsync(ct);

        var nonManualSymbols = portfolioItems
            .Where(p => !p.IsManual)
            .Select(p => p.Symbol)
            .Distinct()
            .ToList();

        decimal stocksValue = 0m;
        if (nonManualSymbols.Count > 0)
        {
            var quotes = await marketData.GetBatchQuotesAsync(nonManualSymbols, ct);
            foreach (var item in portfolioItems.Where(p => !p.IsManual))
            {
                var price = quotes.TryGetValue(item.Symbol, out var q) ? q.CurrentPrice : item.AverageCostBasis;
                stocksValue += price * item.Shares;
            }
        }

        // Manual positions use stored market value; warn if missing
        foreach (var item in portfolioItems.Where(p => p.IsManual))
        {
            if (item.ManualMarketValue == null)
                logger.LogWarning(
                    "[PortfolioValueEod] Manual position {Symbol} missing ManualMarketValue on {Date}; using stale cost basis as fallback.",
                    item.Symbol, recordedDate);
            stocksValue += item.ManualMarketValue ?? item.AverageCostBasis;
        }

        // ── Cash ──────────────────────────────────────────────────────────────
        var cashValue = await db.CashItems.SumAsync(c => c.Amount, ct);

        // ── Options ───────────────────────────────────────────────────────────
        var optionsValue = await db.OptionItems
            .Where(o => o.TransactionType != "CLOSE")
            .SumAsync(o => o.MarketPrice * o.NumberOfContracts * 100, ct);

        var total = stocksValue + cashValue + optionsValue;

        await history.SaveAsync(total, stocksValue, cashValue, optionsValue, recordedDate, ct);
        logger.LogInformation("[PortfolioValueEod] Persisted portfolio value {Total:C2} for {Date}.", total, recordedDate);
    }

    private static TimeZoneInfo? GetEasternTz()
    {
        if (_easternTz is not null) return _easternTz;
        foreach (var id in EasternTzIds)
        {
            try { _easternTz = TimeZoneInfo.FindSystemTimeZoneById(id); return _easternTz; }
            catch { /* try next */ }
        }
        return null;
    }
}
