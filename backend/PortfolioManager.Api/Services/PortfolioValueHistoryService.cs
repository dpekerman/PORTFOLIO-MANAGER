using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public interface IPortfolioValueHistoryService
{
    Task<IReadOnlyList<PortfolioValueHistoryDto>> GetLatestAsync(int count, CancellationToken ct);
    Task SaveAsync(decimal totalValue, decimal stocksValue, decimal cashValue, decimal optionsValue, string recordedDate, CancellationToken ct);
    Task<bool> ExistsForDateAsync(string recordedDate, CancellationToken ct);
    /// <summary>Calculates and persists the current portfolio value. If a record for today already exists it is overwritten
    /// and LastRecalculatedAt is stamped (this is a genuine recompute, not a first insert).
    /// <paramref name="tradingDateOverride"/> stamps the snapshot against an explicit trading day instead of
    /// today's ET calendar date — required when recovering a missed day after that day has already rolled over
    /// (e.g. clicking "Fix Missing Data" the next morning from a timezone ahead of Eastern Time).</summary>
    Task<PortfolioValueHistoryDto> RecordCurrentValueAsync(CancellationToken ct, PortfolioValueSource source, DateOnly? tradingDateOverride = null);
    /// <summary>
    /// Scans the past <paramref name="lookbackDays"/> weekdays and fills any date that has no snapshot
    /// by fetching historical closing prices from Yahoo Finance. Returns the newly created records.
    /// </summary>
    Task<IReadOnlyList<PortfolioValueHistoryDto>> BackfillMissingAsync(int lookbackDays, CancellationToken ct);

    /// <summary>Returns the list of weekday dates in the past lookbackDays that have no snapshot.</summary>
    Task<IReadOnlyList<string>> GetMissingDatesAsync(int lookbackDays, CancellationToken ct);

    /// <summary>Patches ONLY CashValue/TotalValue on existing PortfolioValueHistories rows in
    /// [max(fromDate, LedgerStartDate) .. todayEt] using the cash ledger. Never touches StocksValue or
    /// OptionsValue on an existing row (that would corrupt historically-recorded stock/options data with
    /// today's stale approximations). Stamps Source=CashRecalculation and LastRecalculatedAt=now on every
    /// row it touches. Must be called within the caller's own transaction when the caller needs atomicity
    /// with a ledger write — this method does not open its own transaction.</summary>
    Task RecalculateCashRangeAsync(DateOnly fromDate, CancellationToken ct);

    /// <summary>Returns snapshots in [fromDate..toDate] (most-recent-first), enriched with
    /// ExternalCashFlow/SnapshotStatus/HasMismatch. Defaults to the last 90 days ending today (ET) when
    /// either bound is omitted.</summary>
    Task<IReadOnlyList<PortfolioValueHistoryDto>> GetRangeAsync(DateOnly? fromDate, DateOnly? toDate, CancellationToken ct);
}

public sealed class PortfolioValueHistoryService(
    AppDbContext db,
    IMarketDataProvider marketData,
    ICashLedgerQueryService cashLedger,
    IMutationClock mutationClock,
    ILogger<PortfolioValueHistoryService> logger) : IPortfolioValueHistoryService
{
    public async Task<IReadOnlyList<PortfolioValueHistoryDto>> GetLatestAsync(int count, CancellationToken ct)
    {
        var rows = await db.PortfolioValueHistories
            .OrderByDescending(h => h.RecordedAt)
            .Take(count)
            .ToListAsync(ct);
        return await ToDtosAsync(rows, ct);
    }

    public async Task<IReadOnlyList<PortfolioValueHistoryDto>> GetRangeAsync(DateOnly? fromDate, DateOnly? toDate, CancellationToken ct)
    {
        var todayEt = DateOnly.FromDateTime(NowEt());
        var to = toDate ?? todayEt;
        var from = fromDate ?? to.AddDays(-90);
        var fromStr = from.ToString("yyyy-MM-dd");
        var toStr = to.ToString("yyyy-MM-dd");

        var rows = (await db.PortfolioValueHistories.ToListAsync(ct))
            .Where(h => string.CompareOrdinal(h.RecordedDate, fromStr) >= 0
                     && string.CompareOrdinal(h.RecordedDate, toStr) <= 0)
            .OrderByDescending(h => h.RecordedDate)
            .ToList();
        return await ToDtosAsync(rows, ct);
    }

    /// <summary>Enriches raw rows with ExternalCashFlow (from dated external ledger entries only),
    /// SnapshotStatus (derived from persisted Source/LastRecalculatedAt/RecordedDate, except today's row
    /// which may show live PendingReseal), and HasMismatch (arithmetic consistency check).</summary>
    private async Task<IReadOnlyList<PortfolioValueHistoryDto>> ToDtosAsync(List<PortfolioValueHistory> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return Array.Empty<PortfolioValueHistoryDto>();

        var minDate = DateOnly.Parse(rows.Min(r => r.RecordedDate)!).ToDateTime(TimeOnly.MinValue);
        var maxDate = DateOnly.Parse(rows.Max(r => r.RecordedDate)!).ToDateTime(TimeOnly.MaxValue);

        var externalItems = await db.CashItems
            .Where(c => c.CashFlowType == CashFlowTypeRules.Deposit || c.CashFlowType == CashFlowTypeRules.Withdrawal)
            .Where(c => (c.TransactionDate ?? c.AddedAt) >= minDate && (c.TransactionDate ?? c.AddedAt) <= maxDate)
            .Select(c => new { EffectiveDate = c.TransactionDate ?? c.AddedAt, c.Amount })
            .ToListAsync(ct);

        var externalFlowByDate = externalItems
            .GroupBy(x => x.EffectiveDate.ToString("yyyy-MM-dd"))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var todayEtStr = NowEt().ToString("yyyy-MM-dd");

        var result = new List<PortfolioValueHistoryDto>(rows.Count);
        foreach (var h in rows)
        {
            var externalFlow = externalFlowByDate.GetValueOrDefault(h.RecordedDate, 0m);
            var isTodayRow = h.RecordedDate == todayEtStr;
            var status = DeriveSnapshotStatus(h, isTodayRow);
            var hasMismatch = h.TotalValue != h.StocksValue + h.CashValue + h.OptionsValue;
            result.Add(new PortfolioValueHistoryDto(h.Id, h.RecordedAt, h.RecordedDate, h.TotalValue, h.StocksValue,
                h.CashValue, h.OptionsValue, h.Source, h.LastRecalculatedAt, externalFlow, status, hasMismatch));
        }
        return result;
    }

    /// <summary>Historical rows derive their status purely from persisted fields. Only today's row may
    /// show the live, transient "PendingReseal" state (never persisted).</summary>
    private string DeriveSnapshotStatus(PortfolioValueHistory h, bool isTodayRow)
    {
        if (isTodayRow && mutationClock.IsTodayDirty && mutationClock.SecondsSinceLastMutation() < MutationClock.QuietPeriodSeconds)
            return "PendingReseal";
        if (h.LastRecalculatedAt is null) return "Original";

        var tz = TryGetEasternTz();
        var recalcEtDate = tz is not null
            ? TimeZoneInfo.ConvertTimeFromUtc(h.LastRecalculatedAt.Value, tz)
            : h.LastRecalculatedAt.Value;
        return recalcEtDate.ToString("yyyy-MM-dd") == h.RecordedDate ? "Resealed" : "CashRecalculated";
    }

    private static DateTime NowEt()
    {
        var tz = TryGetEasternTz();
        return tz is not null ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz) : DateTime.UtcNow;
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

    public async Task<PortfolioValueHistoryDto> RecordCurrentValueAsync(CancellationToken ct, PortfolioValueSource source, DateOnly? tradingDateOverride = null)
    {
        // Use ET date to match the EOD background service and dashboard logic, unless an explicit
        // trading day was supplied (missed-day recovery).
        var tz = TryGetEasternTz();
        var recordedDate = tradingDateOverride?.ToString("yyyy-MM-dd") ?? (tz is not null
            ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz)
            : DateTime.UtcNow).ToString("yyyy-MM-dd");

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
        {
            if (item.ManualMarketValue == null)
                logger.LogWarning(
                    "[PortfolioValueHistory] Manual position {Symbol} missing ManualMarketValue in RecordCurrentValueAsync; using stale cost basis as fallback.",
                    item.Symbol);
            stocksValue += item.ManualMarketValue ?? item.AverageCostBasis;
        }

        // ── Cash ────────────────────────────────────────────────────────────
        var cashValue = await cashLedger.GetTotalAsOfAsync(DateOnly.Parse(recordedDate), null, ct);

        // ── Options ─────────────────────────────────────────────────────────
        var optionsValue = await db.OptionItems
            .Where(o => o.TransactionType != "CLOSE")
            .SumAsync(o => o.MarketPrice * o.NumberOfContracts * 100, ct);

        var total = stocksValue + cashValue + optionsValue;

        // Upsert: remove existing record for today if present, then insert fresh
        var existing = await db.PortfolioValueHistories
            .Where(h => h.RecordedDate == recordedDate)
            .ToListAsync(ct);
        var alreadyExisted = existing.Count > 0;
        if (alreadyExisted)
            db.PortfolioValueHistories.RemoveRange(existing);

        var entity = new PortfolioValueHistory
        {
            RecordedAt = DateTime.UtcNow,
            RecordedDate = recordedDate,
            TotalValue = total,
            StocksValue = stocksValue,
            CashValue = cashValue,
            OptionsValue = optionsValue,
            Source = source,
            // Only a genuine recompute of an already-sealed day counts as "recalculated" — the very
            // first insert for a date must stay Original regardless of which Source triggered it.
            LastRecalculatedAt = alreadyExisted ? DateTime.UtcNow : null
        };
        db.PortfolioValueHistories.Add(entity);
        await db.SaveChangesAsync(ct);

        return (await ToDtosAsync([entity], ct))[0];
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
        {
            if (item.ManualMarketValue == null)
                logger.LogWarning(
                    "[PortfolioValueHistory] Manual position {Symbol} missing ManualMarketValue in BackfillDateAsync for {Date}; using stale cost basis as fallback.",
                    item.Symbol, dateStr);
            stocksValue += item.ManualMarketValue ?? item.AverageCostBasis;
        }

        // Cash: dates on/after LedgerStartDate are reconstructed exactly from the dated cash ledger.
        // Earlier dates predate the ledger — cash only moves via real transactions (not market prices),
        // so the nearest existing snapshot's CashValue is a far better proxy than today's current total.
        var ledgerStartDate = await cashLedger.GetLedgerStartDateAsync(ct);
        var cashValue = DateOnly.FromDateTime(date) >= ledgerStartDate
            ? await cashLedger.GetTotalAsOfAsync(DateOnly.FromDateTime(date), null, ct)
            : await GetNearestKnownCashValueAsync(date, ct) ?? await db.CashItems.SumAsync(c => c.Amount, ct);

        // Options open on the target date: currently-open ones opened on/before it, plus
        // since-closed ones that were still open on it (mirrors the stocks filter above).
        // Without historical options pricing, MarketPrice (last known) approximates an
        // open position's value and ClosingPrice approximates one closed after the date.
        var allOptions = await db.OptionItems.ToListAsync(ct);
        var optionsValue = allOptions
            .Where(o => o.OpenDate == null || o.OpenDate.Value.Date <= date.Date)
            .Where(o => o.TransactionType != "CLOSE"
                     || (o.CloseDate.HasValue && o.CloseDate.Value.Date > date.Date))
            .Sum(o => (o.TransactionType == "CLOSE" ? o.ClosingPrice ?? o.MarketPrice : o.MarketPrice)
                      * o.NumberOfContracts * 100);

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

        return (await ToDtosAsync([entity], ct))[0];
    }

    /// <summary>Cash value from whichever existing snapshot is closest in calendar time to the
    /// target date (ties broken toward the earlier row) — cash carries forward/backward between
    /// real transactions, so this is a far better proxy than today's current total.</summary>
    private async Task<decimal?> GetNearestKnownCashValueAsync(DateTime date, CancellationToken ct)
    {
        var rows = await db.PortfolioValueHistories
            .Where(h => h.RecordedDate != date.ToString("yyyy-MM-dd"))
            .Select(h => new { h.RecordedDate, h.CashValue })
            .ToListAsync(ct);

        return rows
            .Select(h => new { h.CashValue, Diff = Math.Abs((DateOnly.Parse(h.RecordedDate).ToDateTime(TimeOnly.MinValue) - date.Date).Days) })
            .OrderBy(h => h.Diff)
            .Select(h => (decimal?)h.CashValue)
            .FirstOrDefault();
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

    public async Task RecalculateCashRangeAsync(DateOnly fromDate, CancellationToken ct)
    {
        var ledgerStartDate = await cashLedger.GetLedgerStartDateAsync(ct);
        var effectiveFrom = fromDate > ledgerStartDate ? fromDate : ledgerStartDate;

        var tz = TryGetEasternTz();
        var todayEt = DateOnly.FromDateTime(tz is not null
            ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz)
            : DateTime.UtcNow);

        var fromStr = effectiveFrom.ToString("yyyy-MM-dd");
        var toStr = todayEt.ToString("yyyy-MM-dd");
        // EF Core's SQL Server provider can't translate string.Compare(a,b,StringComparison) — filter
        // client-side instead (this table holds at most one row/day, so it's always small).
        var rows = (await db.PortfolioValueHistories.ToListAsync(ct))
            .Where(h => string.CompareOrdinal(h.RecordedDate, fromStr) >= 0
                     && string.CompareOrdinal(h.RecordedDate, toStr) <= 0)
            .ToList();

        foreach (var row in rows)
        {
            if (!DateOnly.TryParse(row.RecordedDate, out var rowDate)) continue;
            var newCashValue = await cashLedger.GetTotalAsOfAsync(rowDate, null, ct);
            row.CashValue = newCashValue;
            row.TotalValue = row.StocksValue + newCashValue + row.OptionsValue;
            row.Source = PortfolioValueSource.CashRecalculation;
            row.LastRecalculatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "[PortfolioValueHistory] RecalculateCashRangeAsync patched {Count} row(s) from {From} to {To} (StocksValue/OptionsValue untouched).",
            rows.Count, fromStr, toStr);
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
