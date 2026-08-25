using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public interface IDashboardService
{
    Task<DashboardResponse?> GetLatestAsync(string userId, CancellationToken ct);
    Task<DashboardResponse> RebuildAsync(string userId, CancellationToken ct);
}

public sealed class DashboardService(AppDbContext db, IMarketDataProvider marketData) : IDashboardService
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
        return snapshot is null ? null : Deserialize<DashboardResponse>(snapshot.SnapshotJson);
    }

    public async Task<DashboardResponse> RebuildAsync(string userId, CancellationToken ct)
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
        var scanner = Deserialize<ScannerResponse>(rsiSnapshot?.SnapshotJson ?? "{}")
            ?? new ScannerResponse();
        var indexQuotes = await marketData.GetBatchQuotesAsync(IndexSymbols.Select(i => i.Symbol), ct);
        var trackedSymbols = portfolio.Select(s => s.Item.Symbol).Concat(watchlist.Select(s => s.Item.Symbol));
        var providerEarnings = await marketData.GetEarningsDatesAsync(trackedSymbols, ct);

        var values = history.OrderBy(h => h.RecordedDate).ToList();
        var latest = values.LastOrDefault();
        var previous = values.Count > 1 ? values[^2] : null;
        var todayChange = latest is not null && previous is not null ? latest.TotalValue - previous.TotalValue : 0m;
        var todayPercent = Percent(todayChange, previous?.TotalValue);
        var todayEt = EasternToday();
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
        var weekChange = latest is not null && weekBase is not null ? latest.TotalValue - weekBase.TotalValue : 0m;
        var monthChange = latest is not null && monthBase is not null ? latest.TotalValue - monthBase.TotalValue : 0m;

        // Exclude closed positions so CLOSE transactions don't appear as active portfolio movers
        var moverSources = portfolio
            .Where(s => !string.Equals(s.Item.TransactionType, "CLOSE", StringComparison.OrdinalIgnoreCase))
            .Select(s => (Summary: s, IsPortfolio: true, IsWatchlist: false))
            .Concat(watchlist.Select(s => (Summary: new PortfolioSummaryDto(
                new PortfolioItemDto(s.Item.Id, s.Item.Symbol, s.Item.Symbol, 0, 0, "", "", false, false, null, s.Item.AddedAt), s.Quote),
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
        var portfolioTotal = portfolio
            .Where(s => s.Quote is not null)
            .Sum(s => s.Quote!.CurrentPrice * s.Item.Shares);
        var allocation = portfolio
            .Where(s => s.Quote is not null)
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
        var etTodayStr = todayEt.ToString("yyyy-MM-dd");
        var newToday   = await db.DailySignals.CountAsync(s => s.SignalDate == etTodayStr, ct);
        var actionReq  = scanner.OversoldChain.Count(r => r.Status == SignalStatus.Confirmed || r.Status == SignalStatus.EodConfirm)
                       + scanner.OverboughtChain.Count(r => r.Status == SignalStatus.Confirmed || r.Status == SignalStatus.EodConfirm);

        static string RsiAction(RsiScanResult r)
        {
            if (r.ScanType == ScanType.Oversold)
                return r.Status == SignalStatus.Confirmed || r.Status == SignalStatus.EodConfirm ? "BUY WATCH"
                     : r.TrendShift.Contains("Bull Turn")   ? "WATCH"
                     : r.TrendShift.Contains("Stabilizing") ? "MONITOR"
                     : "WAIT";
            return r.Status == SignalStatus.Confirmed || r.Status == SignalStatus.EodConfirm ? "TRIM WATCH"
                 : r.TrendShift.Contains("Bear Turn")   ? "REVIEW"
                 : "MONITOR";
        }

        var oversoldSignals   = scanner.OversoldChain
            .Select(r => new DashboardRsiSignal(r.Symbol, r.CompanyName, r.Rsi,
                r.TrendShift, r.VolumeSignal, r.ChangePercent, RsiAction(r), r.Status.ToString()))
            .ToList();
        var overboughtSignals = scanner.OverboughtChain
            .Select(r => new DashboardRsiSignal(r.Symbol, r.CompanyName, r.Rsi,
                r.TrendShift, r.VolumeSignal, r.ChangePercent, RsiAction(r), r.Status.ToString()))
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
                latest?.TotalValue ?? 0m,
                todayChange,
                todayPercent,
                weekChange,
                Percent(weekChange, weekBase?.TotalValue),
                monthChange,
                Percent(monthChange, monthBase?.TotalValue),
                scanner.OversoldChain.Count(r => r.Status != SignalStatus.Neutral),
                scanner.OverboughtChain.Count(r => r.Status != SignalStatus.Neutral)),
            movers.Take(10).ToList(),
            movers.OrderBy(m => m.ChangePercent).Take(10).ToList(),
            values.Select(h => new DashboardChartPoint(h.RecordedDate, h.TotalValue)).ToList(),
            IndexSymbols.Select(index =>
            {
                indexQuotes.TryGetValue(index.Symbol, out var quote);
                return new MarketIndexDto(index.Symbol, index.Name, quote?.CurrentPrice ?? 0m,
                    quote?.Change ?? 0m, quote?.ChangePercent ?? 0m);
            }).ToList(),
            allocation,
            earnings,
            rsiSection);

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
