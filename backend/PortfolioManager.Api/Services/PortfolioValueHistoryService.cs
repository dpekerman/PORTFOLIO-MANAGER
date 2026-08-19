using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public interface IPortfolioValueHistoryService
{
    Task<IReadOnlyList<PortfolioValueHistoryDto>> GetLatestAsync(int count, CancellationToken ct);
    Task SaveAsync(decimal totalValue, decimal stocksValue, decimal cashValue, decimal optionsValue, string recordedDate, CancellationToken ct);
    Task<bool> ExistsForDateAsync(string recordedDate, CancellationToken ct);
    /// <summary>Calculates and persists the current portfolio value. If a record for today already exists it is overwritten.</summary>
    Task<PortfolioValueHistoryDto> RecordCurrentValueAsync(CancellationToken ct);
    /// <summary>
    /// Scans the past <paramref name="lookbackDays"/> weekdays and fills any date that has no snapshot
    /// by fetching historical closing prices from Yahoo Finance. Returns the newly created records.
    /// </summary>
    Task<IReadOnlyList<PortfolioValueHistoryDto>> BackfillMissingAsync(int lookbackDays, CancellationToken ct);

    /// <summary>Returns the list of weekday dates in the past lookbackDays that have no snapshot.</summary>
    Task<IReadOnlyList<string>> GetMissingDatesAsync(int lookbackDays, CancellationToken ct);
}

public sealed class PortfolioValueHistoryService(
    AppDbContext db,
    IMarketDataProvider marketData,
    ILogger<PortfolioValueHistoryService> logger) : IPortfolioValueHistoryService
{
    public async Task<IReadOnlyList<PortfolioValueHistoryDto>> GetLatestAsync(int count, CancellationToken ct)
    {
        return await db.PortfolioValueHistories
            .OrderByDescending(h => h.RecordedAt)
            .Take(count)
            .Select(h => new PortfolioValueHistoryDto(h.Id, h.RecordedAt, h.RecordedDate, h.TotalValue, h.StocksValue, h.CashValue, h.OptionsValue))
            .ToListAsync(ct);
    }

    public async Task SaveAsync(decimal totalValue, decimal stocksValue, decimal cashValue, decimal optionsValue, string recordedDate, CancellationToken ct)
    {
        db.PortfolioValueHistories.Add(new PortfolioValueHistory
        {
            RecordedAt = DateTime.UtcNow,
            RecordedDate = recordedDate,
            TotalValue = totalValue,
            StocksValue = stocksValue,
            CashValue = cashValue,
            OptionsValue = optionsValue
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> ExistsForDateAsync(string recordedDate, CancellationToken ct)
        => await db.PortfolioValueHistories.AnyAsync(h => h.RecordedDate == recordedDate, ct);

    public async Task<PortfolioValueHistoryDto> RecordCurrentValueAsync(CancellationToken ct)
    {
        var recordedDate = DateTime.UtcNow.ToString("yyyy-MM-dd");

        // ── Stocks market value ─────────────────────────────────────────────
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
        foreach (var item in portfolioItems.Where(p => p.IsManual))
            stocksValue += item.ManualMarketValue ?? item.AverageCostBasis;

        // ── Cash ────────────────────────────────────────────────────────────
        var cashValue = await db.CashItems.SumAsync(c => c.Amount, ct);

        // ── Options ─────────────────────────────────────────────────────────
        var optionsValue = await db.OptionItems
            .Where(o => o.TransactionType != "CLOSE")
            .SumAsync(o => o.MarketPrice * o.NumberOfContracts * 100, ct);

        var total = stocksValue + cashValue + optionsValue;

        // Upsert: remove existing record for today if present, then insert fresh
        var existing = await db.PortfolioValueHistories
            .Where(h => h.RecordedDate == recordedDate)
            .ToListAsync(ct);
        if (existing.Count > 0)
            db.PortfolioValueHistories.RemoveRange(existing);

        var entity = new PortfolioValueHistory
        {
            RecordedAt = DateTime.UtcNow,
            RecordedDate = recordedDate,
            TotalValue = total,
            StocksValue = stocksValue,
            CashValue = cashValue,
            OptionsValue = optionsValue
        };
        db.PortfolioValueHistories.Add(entity);
        await db.SaveChangesAsync(ct);

        return new PortfolioValueHistoryDto(entity.Id, entity.RecordedAt, entity.RecordedDate,
            entity.TotalValue, entity.StocksValue, entity.CashValue, entity.OptionsValue);
    }

    public async Task<IReadOnlyList<PortfolioValueHistoryDto>> BackfillMissingAsync(int lookbackDays, CancellationToken ct)
    {
        var tz = TryGetEasternTz();
        var todayEt = tz is not null
            ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz).Date
            : DateTime.UtcNow.Date;

        var existingDates = (await db.PortfolioValueHistories
            .Select(h => h.RecordedDate)
            .ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

        var filled = new List<PortfolioValueHistoryDto>();
        for (int i = 1; i <= lookbackDays; i++)
        {
            var candidate = todayEt.AddDays(-i);
            if (candidate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            var dateStr = candidate.ToString("yyyy-MM-dd");
            if (existingDates.Contains(dateStr)) continue;

            logger.LogInformation("[PortfolioValueHistory] Backfilling {Date}", dateStr);
            var dto = await BackfillDateAsync(dateStr, candidate, ct);
            if (dto is null)
            {
                logger.LogWarning("[PortfolioValueHistory] No market data for {Date} — skipping", dateStr);
                continue;
            }
            filled.Add(dto);
            existingDates.Add(dateStr);
        }
        return filled;
    }

    private async Task<PortfolioValueHistoryDto?> BackfillDateAsync(string dateStr, DateTime date, CancellationToken ct)
    {
        var allItems = await db.PortfolioItems.ToListAsync(ct);

        // Currently-open positions that existed on the target date
        var openOnDate = allItems
            .Where(p => p.TransactionType != "CLOSE")
            .Where(p => p.OpenDate == null || p.OpenDate.Value.Date <= date.Date)
            .ToList();

        // Positions since closed but still open on the target date
        var closedAfterDate = allItems
            .Where(p => p.TransactionType == "CLOSE"
                     && p.CloseDate.HasValue && p.CloseDate.Value.Date > date.Date
                     && (p.OpenDate == null || p.OpenDate.Value.Date <= date.Date))
            .ToList();

        var portfolioItems = openOnDate.Concat(closedAfterDate).ToList();
        var nonManualSymbols = portfolioItems
            .Where(p => !p.IsManual)
            .Select(p => p.Symbol)
            .Distinct()
            .ToList();

        decimal stocksValue = 0m;
        if (nonManualSymbols.Count > 0)
        {
            var prices = await marketData.GetHistoricalClosingPricesAsync(dateStr, nonManualSymbols, ct);
            if (prices.Count == 0)
                return null; // no market data — likely a holiday

            foreach (var item in portfolioItems.Where(p => !p.IsManual))
            {
                var price = prices.TryGetValue(item.Symbol, out var p) ? p : item.AverageCostBasis;
                stocksValue += price * item.Shares;
            }
        }
        foreach (var item in portfolioItems.Where(p => p.IsManual))
            stocksValue += item.ManualMarketValue ?? item.AverageCostBasis;

        var cashValue    = await db.CashItems.SumAsync(c => c.Amount, ct);
        var optionsValue = await db.OptionItems
            .Where(o => o.TransactionType != "CLOSE")
            .SumAsync(o => o.MarketPrice * o.NumberOfContracts * 100, ct);

        var total = stocksValue + cashValue + optionsValue;
        if (total == 0) return null;

        // Approximate 4:30 PM ET (EDT = UTC-4 → 20:30 UTC)
        var recordedAt = DateTime.SpecifyKind(date.Date.AddHours(20).AddMinutes(30), DateTimeKind.Utc);
        var entity = new PortfolioValueHistory
        {
            RecordedAt   = recordedAt,
            RecordedDate = dateStr,
            TotalValue   = total,
            StocksValue  = stocksValue,
            CashValue    = cashValue,
            OptionsValue = optionsValue,
        };
        db.PortfolioValueHistories.Add(entity);
        await db.SaveChangesAsync(ct);

        return new PortfolioValueHistoryDto(entity.Id, entity.RecordedAt, entity.RecordedDate,
            entity.TotalValue, entity.StocksValue, entity.CashValue, entity.OptionsValue);
    }

    private static TimeZoneInfo? TryGetEasternTz()
    {
        foreach (var id in new[] { "Eastern Standard Time", "America/New_York" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch { /* ignored */ }
        }
        return null;
    }

    public async Task<IReadOnlyList<string>> GetMissingDatesAsync(int lookbackDays, CancellationToken ct)
    {
        var tz = TryGetEasternTz();
        var todayEt = tz is not null
            ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz).Date
            : DateTime.UtcNow.Date;

        var existingDates = (await db.PortfolioValueHistories
            .Select(h => h.RecordedDate)
            .ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

        var missing = new List<string>();
        for (int i = 1; i <= lookbackDays; i++)
        {
            var candidate = todayEt.AddDays(-i);
            if (candidate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            var dateStr = candidate.ToString("yyyy-MM-dd");
            if (!existingDates.Contains(dateStr))
                missing.Add(dateStr);
        }
        return missing;
    }
}
