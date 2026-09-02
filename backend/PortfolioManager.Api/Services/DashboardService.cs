using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public interface IDashboardService
{
    Task<DashboardResponse?> GetLatestAsync(string userId, CancellationToken ct);
    Task<DashboardResponse> RebuildAsync(string userId, CancellationToken ct, bool includeEarnings = true);
}

public sealed class DashboardService(
    AppDbContext db,
    IMarketDataProvider marketData,
    IPortfolioActionsService portfolioActions) : IDashboardService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly (string Symbol, string Name)[] IndexSymbols =
    [
        ("^DJI",    "Dow Jones"),
        ("^NDX",    "Nasdaq 100"),
        ("^GSPC",   "S&P 500"),
        ("^GSPTSE", "TSX Composite"),
        ("^VIX",    "VIX"),
        ("DX-Y.NYB","DXY (USD)"),
        ("GC=F",    "Gold"),
        ("CL=F",    "Oil (WTI)"),
    ];

    public async Task<DashboardResponse?> GetLatestAsync(string userId, CancellationToken ct)
    {
        var snapshot = await db.DashboardSnapshots.AsNoTracking().SingleOrDefaultAsync(s => s.UserId == userId, ct);
        var response = snapshot is null ? null : Deserialize<DashboardResponse>(snapshot.SnapshotJson);
        return response is null ? null : NormalizeSignalSection(response);
    }

    public async Task<DashboardResponse> RebuildAsync(string userId, CancellationToken ct, bool includeEarnings = true)
    {
        var portfolioSnapshot = await db.PortfolioSnapshots.AsNoTracking().SingleOrDefaultAsync(s => s.UserId == userId, ct);
        var watchlistSnapshot = await db.WatchlistSnapshots.AsNoTracking().SingleOrDefaultAsync(s => s.UserId == userId, ct);
        var rsiSnapshot = await db.RsiScanSnapshots.AsNoTracking().SingleOrDefaultAsync(s => s.Id == 1, ct);
        var history = await db.PortfolioValueHistories.AsNoTracking()
            .OrderByDescending(h => h.RecordedDate)
            .Take(365)
            .ToListAsync(ct);

        var portfolio = DeserializeList<PortfolioSummaryDto>(portfolioSnapshot?.SnapshotJson ?? "[]");
        var watchlist = DeserializeList<WatchlistSummaryDto>(watchlistSnapshot?.SnapshotJson ?? "[]");
        var activePortfolioSymbols = portfolio
            .Where(s => !string.Equals(s.Item.TransactionType, "CLOSE", StringComparison.OrdinalIgnoreCase))
            .Select(s => s.Item.Symbol)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var watchlistSymbols = watchlist
            .Select(s => s.Item.Symbol)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var scanner = Deserialize<ScannerResponse>(rsiSnapshot?.SnapshotJson ?? "{}")
            ?? new ScannerResponse();
        var canonicalActions = await portfolioActions.GetActionsAsync(userId, ct);
        var actionsBySymbol = canonicalActions
            .GroupBy(action => action.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var stagedSignals = await db.StagedSignals.AsNoTracking()
            .Where(s => s.IsActiveWatch)
            .ToListAsync(ct);
        var stagedBySymbol = stagedSignals
            .GroupBy(s => s.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.UpdatedAt).First(), StringComparer.OrdinalIgnoreCase);
        var indexQuotes = await marketData.GetBatchQuotesAsync(IndexSymbols.Select(i => i.Symbol), ct);
        // Skip earnings fetch during batch refresh to avoid ~9s delay (300ms/symbol)
        Dictionary<string, DateTime> providerEarnings = includeEarnings
            ? await marketData.GetEarningsDatesAsync(
                portfolio.Select(s => s.Item.Symbol).Concat(watchlist.Select(s => s.Item.Symbol)), ct)
            : [];

        var values = history.OrderBy(h => h.RecordedDate).ToList();
        var latest = values.LastOrDefault();
        var previous = values.Count > 1 ? values[^2] : null;
        var todayEt = EasternToday();
        var etTodayStr = todayEt.ToString("yyyy-MM-dd");

        // Compute live total from portfolio snapshot + DB cash/options — matches the portfolio page.
        var liveStocksValue = portfolio
            .Where(s => !string.Equals(s.Item.TransactionType, "CLOSE", StringComparison.OrdinalIgnoreCase))
            .Sum(s => s.Item.IsManual
                ? (s.Item.ManualMarketValue ?? s.Item.AverageCostBasis * s.Item.Shares)
                : (s.Quote?.CurrentPrice ?? s.Item.AverageCostBasis) * s.Item.Shares);
        var liveCashValue    = await db.CashItems.SumAsync(c => c.Amount, ct);
        var liveOptionsValue = await db.OptionItems
            .Where(o => o.TransactionType != "CLOSE")
            .SumAsync(o => o.MarketPrice * o.NumberOfContracts * 100, ct);
        var liveTotal = liveStocksValue + liveCashValue + liveOptionsValue;

        // Always show live snapshot total (matches portfolio page).
        // Use history entries only to find yesterday's close for the change computation.
        var hasTodayEntry = latest?.RecordedDate == etTodayStr;
        var summaryTotal = liveTotal;
        var yesterdayEntry = hasTodayEntry ? previous : latest;
        var todayChange = yesterdayEntry is not null ? liveTotal - yesterdayEntry.TotalValue : 0m;
        var todayPercent = Percent(todayChange, yesterdayEntry?.TotalValue);
        // Per-component breakdown: shows exactly what drove the 1-day change
        var todayStocksChange  = yesterdayEntry is not null ? liveStocksValue  - yesterdayEntry.StocksValue  : 0m;
        var todayCashChange    = yesterdayEntry is not null ? liveCashValue     - yesterdayEntry.CashValue    : 0m;
        var todayOptionsChange = yesterdayEntry is not null ? liveOptionsValue  - yesterdayEntry.OptionsValue : 0m;

        var daysSinceMonday = ((int)todayEt.DayOfWeek + 6) % 7;
        var weekStart = todayEt.AddDays(-daysSinceMonday);
        var weekStartDate = DateOnly.FromDateTime(weekStart);
        // Week baseline: last close before the week started (e.g. Friday for Monday)
        var weekBase = values.LastOrDefault(h => DateOnly.Parse(h.RecordedDate) < weekStartDate)
            ?? values.FirstOrDefault(h => DateOnly.Parse(h.RecordedDate) >= weekStartDate);
        // Month baseline: last EOD record on or before the 1st of the current month (= prior-month close)
        var monthFirstDay = new DateOnly(todayEt.Year, todayEt.Month, 1);
        var monthBase = values.LastOrDefault(h => DateOnly.Parse(h.RecordedDate) < monthFirstDay)
            ?? values.FirstOrDefault(h => DateOnly.Parse(h.RecordedDate).Month == todayEt.Month
                && DateOnly.Parse(h.RecordedDate).Year == todayEt.Year);
        var weekChange = weekBase is not null ? summaryTotal - weekBase.TotalValue : 0m;
        var monthChange = monthBase is not null ? summaryTotal - monthBase.TotalValue : 0m;

        // Exclude closed positions so CLOSE transactions don't appear as active portfolio movers
        var moverSources = portfolio
            .Where(s => !string.Equals(s.Item.TransactionType, "CLOSE", StringComparison.OrdinalIgnoreCase))
            .Select(s => (Summary: s, IsPortfolio: true, IsWatchlist: false))
            .Concat(watchlist.Select(s => (Summary: new PortfolioSummaryDto(
                new PortfolioItemDto(s.Item.Id, s.Item.Symbol, s.Item.Symbol, 0, 0, "", "", false, false, null, s.Item.AddedAt), s.Quote, s.PriceStructure),
                IsPortfolio: false, IsWatchlist: true)));
        var movers = moverSources
            .Where(s => s.Summary.Quote is not null)
            .GroupBy(s => s.Summary.Item.Symbol, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var item = g.First();
                var quote = item.Summary.Quote!;
                return new DashboardMover(
                    quote.Symbol,
                    quote.CompanyName,
                    quote.ChangePercent,
                    g.Any(s => s.IsPortfolio),
                    g.Any(s => s.IsWatchlist));
            })
            .OrderByDescending(m => m.ChangePercent)
            .ToList();

        // ── Sector allocation vs targets ────────────────────────────────────────
        var sectorTargets = await db.AllocationSectorTargets
            .AsNoTracking()
            .ToDictionaryAsync(t => t.Sector, t => t.TargetPct, StringComparer.OrdinalIgnoreCase, ct);
        // Stocks + cash only; exclude Options-role items (manual positions classified as options)
        var sectorItems = portfolio
            .Where(s => s.Quote is not null
                && !string.Equals(s.Item.TransactionType, "CLOSE", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(s.Item.HoldingRole, "Options", StringComparison.OrdinalIgnoreCase));
        var stocksValue = sectorItems.Sum(s => s.Quote!.CurrentPrice * s.Item.Shares);
        // Denominator = stocks only, matching the Allocation page anchor
        var portfolioTotal = stocksValue;
        var allocation = sectorItems
            .GroupBy(s => string.IsNullOrWhiteSpace(s.Item.Sector) ? "Unclassified" : s.Item.Sector)
            .Select(group =>
            {
                var value = group.Sum(s => s.Quote!.CurrentPrice * s.Item.Shares);
                var pct   = Percent(value, portfolioTotal);
                sectorTargets.TryGetValue(group.Key, out var target);
                var delta  = pct - target;
                var status = target == 0m ? "no-target"
                           : Math.Abs(delta) <= 2m  ? "good"
                           : Math.Abs(delta) <= 5m  ? (delta > 0 ? "watch-over" : "watch-under")
                           :                          (delta > 0 ? "over"        : "under");
                return new DashboardAllocation(group.Key, value, pct, target, Math.Round(delta, 2), status);
            })
            .ToList();

        // Add Cash row — % of (stocks + cash) so it sums naturally alongside sector rows
        if (liveCashValue > 0m)
        {
            var cashBase = stocksValue + liveCashValue;
            var cashPct  = Percent(liveCashValue, cashBase);
            sectorTargets.TryGetValue("Cash", out var cashTarget);
            var cashDelta  = cashPct - cashTarget;
            var cashStatus = cashTarget == 0m ? "no-target"
                           : Math.Abs(cashDelta) <= 2m  ? "good"
                           : Math.Abs(cashDelta) <= 5m  ? (cashDelta > 0 ? "watch-over" : "watch-under")
                           :                              (cashDelta > 0 ? "over"        : "under");
            allocation.Add(new DashboardAllocation("Cash", liveCashValue, cashPct, cashTarget, Math.Round(cashDelta, 2), cashStatus));
        }

        // ── Role allocation vs risk targets ──────────────────────────────────────
        var roleTargets = await db.AllocationRiskTargets
            .AsNoTracking()
            .ToDictionaryAsync(t => t.Role, t => t.TargetPct, StringComparer.OrdinalIgnoreCase, ct);

        // Group active stocks by role using liveTotal as denominator (includes cash + options)
        var stockRoleGroups = portfolio
            .Where(s => s.Quote is not null
                && !string.Equals(s.Item.TransactionType, "CLOSE", StringComparison.OrdinalIgnoreCase))
            .GroupBy(s => string.IsNullOrWhiteSpace(s.Item.HoldingRole) ? "Strategic" : s.Item.HoldingRole)
            .Select(group =>
            {
                var value = group.Sum(s => s.Quote!.CurrentPrice * s.Item.Shares);
                var pct   = Percent(value, liveTotal);
                roleTargets.TryGetValue(group.Key, out var target);
                var delta  = pct - target;
                var status = target == 0m ? "no-target"
                           : Math.Abs(delta) <= 2m  ? "good"
                           : Math.Abs(delta) <= 5m  ? (delta > 0 ? "watch-over" : "watch-under")
                           :                          (delta > 0 ? "over"        : "under");
                return new DashboardAllocation(group.Key, value, pct, target, Math.Round(delta, 2), status);
            })
            .ToList();

        // Merge options value into the "Options" role entry
        if (liveOptionsValue > 0m)
        {
            var optionsPct = Percent(liveOptionsValue, liveTotal);
            roleTargets.TryGetValue("Options", out var optTarget);
            var optDelta  = optionsPct - optTarget;
            var optStatus = optTarget == 0m ? "no-target"
                          : Math.Abs(optDelta) <= 2m  ? "good"
                          : Math.Abs(optDelta) <= 5m  ? (optDelta > 0 ? "watch-over" : "watch-under")
                          :                             (optDelta > 0 ? "over"        : "under");
            var existingOptions = stockRoleGroups.FirstOrDefault(r =>
                string.Equals(r.Label, "Options", StringComparison.OrdinalIgnoreCase));
            if (existingOptions is not null)
            {
                // Merge any stocks with Options role + actual options market value
                var mergedValue = existingOptions.Value + liveOptionsValue;
                var mergedPct   = Percent(mergedValue, liveTotal);
                var mergedDelta = mergedPct - optTarget;
                var mergedStatus = optTarget == 0m ? "no-target"
                                 : Math.Abs(mergedDelta) <= 2m  ? "good"
                                 : Math.Abs(mergedDelta) <= 5m  ? (mergedDelta > 0 ? "watch-over" : "watch-under")
                                 :                                 (mergedDelta > 0 ? "over"        : "under");
                stockRoleGroups[stockRoleGroups.IndexOf(existingOptions)] =
                    new DashboardAllocation("Options", mergedValue, mergedPct, optTarget, Math.Round(mergedDelta, 2), mergedStatus);
            }
            else
            {
                stockRoleGroups.Add(new DashboardAllocation("Options", liveOptionsValue, optionsPct, optTarget, Math.Round(optDelta, 2), optStatus));
            }
        }
        var roleAllocation = stockRoleGroups.OrderByDescending(a => a.Value).ToList();
        var newToday = 0;
        var actionReq = 0;

        var BuildSignal = (RsiScanResult r) =>
        {
            var isInPortfolio = activePortfolioSymbols.Contains(r.Symbol);
            var isInWatchlist = watchlistSymbols.Contains(r.Symbol);
            actionsBySymbol.TryGetValue(r.Symbol, out var canonicalAction);
            var hasCanonicalScope = isInPortfolio || isInWatchlist;
            var action = canonicalAction?.ActionLabel
                ?? (hasCanonicalScope
                    ? "—"
                    : DashboardSignalActionInterpreter.Resolve(r, false, false));
            var severity = canonicalAction?.ActionSeverity
                ?? (hasCanonicalScope ? "review" : ActionSeverityMapper.Get(action));
            var actionRequired = canonicalAction?.ActionPriority == "REQUIRED";
            var isNew = stagedBySymbol.TryGetValue(r.Symbol, out var staged)
                && staged.StagedDate == DateOnly.FromDateTime(todayEt);
            if (isNew) newToday++;
            if (actionRequired) actionReq++;
            return new DashboardRsiSignal(r.Symbol, r.CompanyName, r.Rsi,
                r.TrendShift, r.VolumeSignal, r.ChangePercent, action, r.Status.ToString(),
                isInPortfolio, isInWatchlist, isNew, actionRequired, severity, r.ChannelState);
        };

        var oversoldSignals   = scanner.OversoldChain
            .DistinctBy(r => r.Symbol, StringComparer.OrdinalIgnoreCase)
            .Select(BuildSignal)
            .ToList();
        var overboughtSignals = scanner.OverboughtChain
            .DistinctBy(r => r.Symbol, StringComparer.OrdinalIgnoreCase)
            .Select(BuildSignal)
            .ToList();

        var rsiSection = new DashboardRsiSection(
            oversoldSignals.Count, overboughtSignals.Count, newToday, actionReq,
            oversoldSignals, overboughtSignals);

        var today = EasternToday().Date;
        var earnings = portfolio.Select(s => (s.Item.Symbol, Name: s.Quote?.CompanyName ?? s.Item.Symbol, Manual: (DateTime?)null))
            .Concat(watchlist.Select(s => (s.Item.Symbol, Name: s.Quote?.CompanyName ?? s.Item.Symbol, Manual: s.Item.EarningsDate)))
            .Select(s => providerEarnings.TryGetValue(s.Symbol, out var providerDate)
                ? new DashboardEarning(s.Symbol, s.Name, s.Manual ?? providerDate, s.Manual.HasValue ? "Manual" : "Yahoo")
                : s.Manual.HasValue
                    ? new DashboardEarning(s.Symbol, s.Name, s.Manual.Value, "Manual")
                    : null)
            .Where(e => e is not null)
            .Select(e => e!)
            .Where(e => e.EarningsDate.Date >= today && e.EarningsDate.Date <= today.AddDays(7))
            .OrderBy(e => e.EarningsDate)
            .ToList();

        var response = new DashboardResponse(
            DateTime.UtcNow,
            new DashboardSummary(
                summaryTotal,
                todayChange,
                todayPercent,
                todayStocksChange,
                todayCashChange,
                todayOptionsChange,
                weekChange,
                Percent(weekChange, weekBase?.TotalValue),
                monthChange,
                Percent(monthChange, monthBase?.TotalValue),
                scanner.OversoldChain
                    .DistinctBy(r => r.Symbol, StringComparer.OrdinalIgnoreCase)
                    .Count(r => r.Status != SignalStatus.Neutral),
                scanner.OverboughtChain
                    .DistinctBy(r => r.Symbol, StringComparer.OrdinalIgnoreCase)
                    .Count(r => r.Status != SignalStatus.Neutral)),
            movers.Take(50).ToList(),
            movers.OrderBy(m => m.ChangePercent).Take(50).ToList(),
            values.Select(h => new DashboardChartPoint(h.RecordedDate, h.TotalValue)).ToList(),
            IndexSymbols.Select(index =>
            {
                indexQuotes.TryGetValue(index.Symbol, out var quote);
                return new MarketIndexDto(index.Symbol, index.Name, quote?.CurrentPrice ?? 0m,
                    quote?.Change ?? 0m, quote?.ChangePercent ?? 0m);
            }).ToList(),
            allocation,
            earnings,
            rsiSection,
            roleAllocation);

        var entity = await db.DashboardSnapshots.SingleOrDefaultAsync(s => s.UserId == userId, ct);
        if (entity is null)
        {
            db.DashboardSnapshots.Add(new DashboardSnapshot { UserId = userId, SnapshotJson = JsonSerializer.Serialize(response, JsonOptions) });
        }
        else
        {
            entity.SnapshotJson = JsonSerializer.Serialize(response, JsonOptions);
            entity.UpdatedAt = response.UpdatedAt;
        }
        await db.SaveChangesAsync(ct);
        return response;
    }

    private static decimal Percent(decimal change, decimal? baseValue)
        => baseValue is > 0m ? Math.Round(change / baseValue.Value * 100m, 2) : 0m;

    private static DashboardResponse NormalizeSignalSection(DashboardResponse response)
    {
        if (response.RsiSection is not { } section) return response;

        var oversold = section.OversoldSignals
            .DistinctBy(s => s.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var overbought = section.OverboughtSignals
            .DistinctBy(s => s.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var allSignals = oversold.Concat(overbought).ToList();

        return response with
        {
            Summary = response.Summary with
            {
                OversoldCount = oversold.Count,
                OverboughtCount = overbought.Count,
            },
            RsiSection = section with
            {
                OversoldCount = oversold.Count,
                OverboughtCount = overbought.Count,
                NewTodayCount = allSignals.Count(s => s.IsNewToday),
                ActionRequiredCount = allSignals.Count(s => s.IsActionRequired),
                OversoldSignals = oversold,
                OverboughtSignals = overbought,
            },
        };
    }

    private static DateTime EasternToday()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Eastern Standard Time" : "America/New_York");
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone).Date;
    }

    private static T? Deserialize<T>(string json)
    {
        try { return JsonSerializer.Deserialize<T>(json, JsonOptions); }
        catch (JsonException) { return default; }
    }
    private static List<T> DeserializeList<T>(string json) => Deserialize<List<T>>(json) ?? [];
}
