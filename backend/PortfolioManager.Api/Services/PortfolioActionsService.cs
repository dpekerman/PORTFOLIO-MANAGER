using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public interface IPortfolioActionsService
{
    Task<IReadOnlyList<PortfolioActionDto>> GetActionsAsync(string userId, CancellationToken ct = default);
}

public sealed class PortfolioActionsService(AppDbContext db) : IPortfolioActionsService
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    // Strict bull = confirmed reversal candle; any bullish includes Stabilizing
    private static readonly HashSet<string> StrictBullish = new(StringComparer.OrdinalIgnoreCase)
        { "🟢 Bull Turn" };
    private static readonly HashSet<string> AnyBullish = new(StringComparer.OrdinalIgnoreCase)
        { "🟢 Bull Turn", "🟡 Stabilizing" };
    private static readonly HashSet<string> BearishShifts = new(StringComparer.OrdinalIgnoreCase)
        { "🟢 Bear Turn", "🔴 Still Rising" };

    public async Task<IReadOnlyList<PortfolioActionDto>> GetActionsAsync(string userId, CancellationToken ct = default)
    {
        var portfolioSnap = await db.PortfolioSnapshots.AsNoTracking()
            .SingleOrDefaultAsync(s => s.UserId == userId, ct);
        var watchlistSnap = await db.WatchlistSnapshots.AsNoTracking()
            .SingleOrDefaultAsync(s => s.UserId == userId, ct);
        var rsiSnap = await db.RsiScanSnapshots.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == 1, ct);
        var sectorTargets = await db.AllocationSectorTargets.AsNoTracking().ToListAsync(ct);

        var portfolio = Deserialize<List<PortfolioSummaryDto>>(portfolioSnap?.SnapshotJson ?? "[]") ?? [];
        var watchlist = Deserialize<List<WatchlistSummaryDto>>(watchlistSnap?.SnapshotJson ?? "[]") ?? [];
        var scanner   = Deserialize<ScannerResponse>(rsiSnap?.SnapshotJson ?? "{}") ?? new ScannerResponse();
        var channels = await db.TechnicalChannels.AsNoTracking()
            .Where(c => c.Timeframe == "1D")
            .ToDictionaryAsync(c => c.Ticker, StringComparer.OrdinalIgnoreCase, ct);

        var signals = scanner.OversoldChain
            .Concat(scanner.OverboughtChain)
            .GroupBy(r => r.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var totalValue = portfolio
            .Where(p => !IsClose(p.Item.TransactionType))
            .Sum(p => MarketValue(p));
        var sectorActuals = portfolio
            .Where(p => !IsClose(p.Item.TransactionType))
            .GroupBy(p => p.Item.Sector ?? "", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key,
                g => totalValue > 0 ? g.Sum(p => MarketValue(p)) / totalValue * 100m : 0m,
                StringComparer.OrdinalIgnoreCase);

        var portfolioBySymbol = portfolio
            .Where(p => !IsClose(p.Item.TransactionType))
            .GroupBy(p => p.Item.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var activeWatchlist = watchlist
            .Where(w => string.Equals(w.Item.WatchlistTier, "Active", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var universe = portfolioBySymbol.Keys
            .Concat(activeWatchlist.Select(w => w.Item.Symbol))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var results = new List<PortfolioActionDto>();

        foreach (var symbol in universe)
        {
            signals.TryGetValue(symbol, out var scan);
            channels.TryGetValue(symbol, out var channel);
            portfolioBySymbol.TryGetValue(symbol, out var pos);
            var wlItem = activeWatchlist.FirstOrDefault(w =>
                string.Equals(w.Item.Symbol, symbol, StringComparison.OrdinalIgnoreCase));

            if (pos is null && wlItem is null) continue;
            scan ??= new RsiScanResult
            {
                Symbol = symbol,
                CompanyName = pos?.Item.CompanyName ?? wlItem?.Item.Symbol ?? symbol,
                ScanType = ScanType.Neutral,
                Status = SignalStatus.Neutral,
                TrendShift = "Waiting",
                ChannelDirection = channel?.Direction ?? "NONE",
                ChannelState = channel?.ChannelState ?? "NONE",
                ChannelQuality = channel?.ChannelQuality ?? 0,
                PriorConfirmedLowerTouches = channel?.LowerTouchCount ?? 0,
                LowerRailToday = channel?.LowerRailCurrent ?? 0,
                DistanceToLowerRailPercent = channel?.DistanceToLowerRailPercent ?? 0,
                DistanceToLowerRailATR = channel?.DistanceToLowerRailATR ?? 0,
                LastLowerTouchDate = channel?.LastLowerTouchDate,
                NearestOpenGapAbove = channel?.NearestOpenGapAbove,
            };
            if (channel is not null && scan.ChannelState == "NONE")
            {
                scan.ChannelDirection = channel.Direction;
                scan.ChannelState = channel.ChannelState;
                scan.ChannelQuality = channel.ChannelQuality;
                scan.PriorConfirmedLowerTouches = channel.LowerTouchCount;
                scan.LowerRailToday = channel.LowerRailCurrent;
                scan.DistanceToLowerRailPercent = channel.DistanceToLowerRailPercent;
                scan.DistanceToLowerRailATR = channel.DistanceToLowerRailATR;
                scan.LastLowerTouchDate = channel.LastLowerTouchDate;
                scan.NearestOpenGapAbove = channel.NearestOpenGapAbove;
            }

            var holdingRole      = pos?.Item.HoldingRole ?? wlItem?.Item.Role ?? "Strategic";
            var sector           = pos?.Item.Sector ?? scan.Sector ?? "";
            var allocationStatus = ComputeAllocationStatus(sector, sectorActuals, sectorTargets);
            var isHolding        = pos is not null;

            if (channel is null && scan.Status == SignalStatus.Neutral && allocationStatus != "over") continue;

            var (actionLabel, severity, priority) =
                DeriveAction(scan, holdingRole, allocationStatus, isHolding);
            severity = ActionSeverityMapper.Get(actionLabel, allocationStatus == "over" && severity == "buy");

            results.Add(new PortfolioActionDto(
                Symbol:           symbol,
                CompanyName:      scan.CompanyName,
                HoldingRole:      holdingRole,
                ScanType:         scan.ScanType.ToString(),
                Rsi:              scan.Rsi,
                TrendShift:       scan.TrendShift ?? "",
                FibZone:          scan.FibZone ?? "",
                ChaseRisk:        scan.ChaseRisk ?? "",
                AllocationStatus: allocationStatus,
                ActionLabel:      actionLabel,
                ActionSeverity:   severity,
                ActionPriority:   priority,
                IsInPortfolio:    pos is not null,
                IsInWatchlist:    wlItem is not null,
                ChannelState:     scan.ChannelState,
                ChannelDirection: scan.ChannelDirection,
                ChannelQuality:   scan.ChannelQuality,
                PriorConfirmedLowerTouches: scan.PriorConfirmedLowerTouches,
                LowerRailToday:   scan.LowerRailToday,
                EodClose:         scan.CurrentPrice,
                DistanceToLowerRailPercent: scan.DistanceToLowerRailPercent,
                DistanceToLowerRailATR: scan.DistanceToLowerRailATR,
                LastLowerTouchDate: scan.LastLowerTouchDate,
                NearestOpenGapAbove: scan.NearestOpenGapAbove,
                ChannelTouchDetails: scan.ChannelTouchDetails));
        }

        return results
            .OrderBy(r => PriorityOrder(r.ActionPriority))
            .ThenByDescending(r => r.IsInPortfolio)
            .ThenBy(r => SeverityOrder(r.ActionSeverity))
            .ThenBy(r => r.ScanType == "Oversold" ? r.Rsi : 100 - r.Rsi)
            .ToList()
            .AsReadOnly();
    }

    private static (string label, string severity, string priority)
        DeriveAction(RsiScanResult scan, string role, string allocationStatus, bool isHolding)
    {
        if (!string.IsNullOrEmpty(scan.ChaseRisk))
            return ("DO NOT CHASE", "danger", "REQUIRED");

        var isBullish     = AnyBullish.Contains(scan.TrendShift ?? "");
        var isStrictBull  = StrictBullish.Contains(scan.TrendShift ?? "");
        var isBearish     = BearishShifts.Contains(scan.TrendShift ?? "");
        var isTrendDamage = string.Equals(scan.FibZone, "Trend Damage", StringComparison.OrdinalIgnoreCase);
        var isOversold    = scan.ScanType == ScanType.Oversold;
        var r             = (role ?? "Strategic").Trim();
        var isCore        = string.Equals(r, "Core",        StringComparison.OrdinalIgnoreCase);
        var isSwing       = string.Equals(r, "Swing",       StringComparison.OrdinalIgnoreCase);
        var isSpec        = string.Equals(r, "Speculative", StringComparison.OrdinalIgnoreCase);
        var channelState  = scan.ChannelState ?? "NONE";

        if (channelState is "THIRD_TOUCH_APPROACHING" or "THIRD_TOUCH_TEST"
            or "LOWER_RAIL_APPROACHING" or "LOWER_RAIL_RETEST"
            or "REVERSAL_DEVELOPING" or "BOUNCE_CONFIRMED" or "CHANNEL_BROKEN")
        {
            var channelAction = DeriveChannelAction(channelState, scan.TrendShift ?? "", r, isHolding, scan.ScanType);
            if (channelAction.HasValue)
            {
                var (label, severity, priority) = channelAction.Value;
                if (allocationStatus == "over" && severity == "buy")
                    return (isHolding ? "HOLD — ALLOCATION FULL" : "WATCH — ALLOCATION BLOCKED", "hold", "INFORMATIONAL");
                return channelAction.Value;
            }
        }

        // ── WATCHLIST ITEMS (no position) ────────────────────────────────────
        if (!isHolding)
        {
            if (!isOversold)
                return ("WAIT FOR PULLBACK", "wait", "INFORMATIONAL");

            if (isBullish && !isTrendDamage)
                return isStrictBull
                    ? ("ENTRY CANDIDATE", "buy", "REQUIRED")
                    : ("STARTER ENTRY",   "buy", "REQUIRED");

            if (isBullish && isTrendDamage)
                return ("BUY WATCH", "buy", "DEVELOPING");

            return isTrendDamage
                ? ("AVOID — TREND DAMAGE", "wait", "INFORMATIONAL")
                : ("WAIT FOR REVERSAL",     "wait", "DEVELOPING");
        }

        // ── PORTFOLIO HOLDINGS ──────────────────────────────────────────────
        if (!isOversold) // Overbought territory
        {
            if (isCore)
                return isBearish
                    ? ("TRIM WATCH",      "trim", "DEVELOPING")
                    : ("HOLD — EXTENDED", "hold", "INFORMATIONAL");

            if (isSwing || isSpec)
                return isBearish
                    ? ("TRIM",       "trim", "REQUIRED")
                    : ("TRIM WATCH", "trim", "DEVELOPING");

            return isBearish
                ? ("TRIM WATCH",      "trim", "DEVELOPING")
                : ("HOLD — EXTENDED", "hold", "INFORMATIONAL");
        }

        // Oversold territory
        if (isCore)
        {
            if (isBullish)
                return allocationStatus == "over"
                    ? ("HOLD — SECTOR FULL", "hold", "INFORMATIONAL")
                    : ("ADD WATCH",           "buy",  "DEVELOPING");
            return isTrendDamage
                ? ("HOLD — WAIT",    "hold", "INFORMATIONAL")
                : ("HOLD — WEAKNESS", "hold", "INFORMATIONAL");
        }

        if (isSwing)
        {
            if (isBullish) return ("REVERSAL WATCH", "buy",    "DEVELOPING");
            return isTrendDamage
                ? ("EXIT REVIEW", "review", "REQUIRED")
                : ("HOLD — WAIT", "hold",   "INFORMATIONAL");
        }

        if (isSpec)
        {
            if (isBullish) return ("REVERSAL WATCH", "buy",    "DEVELOPING");
            return isTrendDamage
                ? ("RISK REVIEW", "review", "REQUIRED")
                : ("HOLD — WAIT", "hold",   "INFORMATIONAL");
        }

        // Strategic (default)
        if (isBullish) return ("BUY WATCH", "buy", "DEVELOPING");
        return isTrendDamage
            ? ("HOLD / REVIEW THESIS", "review", "DEVELOPING")
            : ("HOLD — WEAKNESS",     "hold",   "INFORMATIONAL");
    }

    private static (string label, string severity, string priority)? DeriveChannelAction(
        string channelState, string trendShift, string role, bool isHolding, ScanType scanType)
    {
        var isBullTurn = string.Equals(trendShift, "🟢 Bull Turn", StringComparison.OrdinalIgnoreCase);
        var isStabilizing = trendShift.Contains("Stabilizing", StringComparison.OrdinalIgnoreCase);
        var isStillFalling = trendShift.Contains("Still Falling", StringComparison.OrdinalIgnoreCase);
        var isCore = string.Equals(role, "Core", StringComparison.OrdinalIgnoreCase);
        var isStrategic = string.Equals(role, "Strategic", StringComparison.OrdinalIgnoreCase);
        var isSwing = string.Equals(role, "Swing", StringComparison.OrdinalIgnoreCase);

        if (scanType == ScanType.Overbought) return null;

        if (channelState == "CHANNEL_BROKEN")
            return isHolding
                ? isSwing ? ("EXIT REVIEW", "review", "REQUIRED") : ("TECHNICAL REVIEW", "review", "REQUIRED")
                : ("AVOID", "danger", "REQUIRED");

        if (!isHolding && (channelState == "THIRD_TOUCH_APPROACHING" || channelState == "LOWER_RAIL_APPROACHING"))
            return ("WATCH CHANNEL", "wait", "DEVELOPING");

        if (channelState is "THIRD_TOUCH_TEST" or "LOWER_RAIL_RETEST")
        {
            if (isStillFalling) return ("WAIT FOR REVERSAL", "wait", "DEVELOPING");
            if (isStabilizing) return ("REVERSAL WATCH", "wait", "DEVELOPING");
            if (isBullTurn)
            {
                if (!isHolding) return ("BUY WATCH", "buy", "DEVELOPING");
                if (isSwing) return ("STAGED ADD / HOLD", "buy", "DEVELOPING");
                if (isCore || isStrategic) return ("ADD CANDIDATE", "buy", "REQUIRED");
            }
            return null;
        }

        if (channelState == "REVERSAL_DEVELOPING")
            return (isHolding ? "ADD WATCH" : "REVERSAL WATCH", "wait", "DEVELOPING");

        if (channelState == "BOUNCE_CONFIRMED")
        {
            if (!isHolding) return ("ENTRY CANDIDATE", "buy", "REQUIRED");
            if (isSwing) return ("STAGED ADD / HOLD", "buy", "DEVELOPING");
            return ("ADD CANDIDATE", "buy", "REQUIRED");
        }

        return null;
    }

    private static string ComputeAllocationStatus(
        string sector,
        Dictionary<string, decimal> actuals,
        List<AllocationSectorTarget> targets)
    {
        if (string.IsNullOrEmpty(sector)) return "";
        var target = targets.FirstOrDefault(t =>
            string.Equals(t.Sector, sector, StringComparison.OrdinalIgnoreCase));
        if (target is null) return "";
        actuals.TryGetValue(sector, out var actual);
        var delta = actual - target.TargetPct;
        return delta > 2m ? "over" : delta < -2m ? "under" : "on-target";
    }

    private static bool IsClose(string? txType)
        => string.Equals(txType, "CLOSE", StringComparison.OrdinalIgnoreCase);

    private static decimal MarketValue(PortfolioSummaryDto p)
    {
        if (p.Item.IsManual)
            return p.Item.ManualMarketValue ?? p.Item.AverageCostBasis * p.Item.Shares;
        return (p.Quote?.CurrentPrice ?? p.Item.AverageCostBasis) * p.Item.Shares;
    }

    private static int PriorityOrder(string p) => p switch
    {
        "REQUIRED"      => 0,
        "DEVELOPING"    => 1,
        "INFORMATIONAL" => 2,
        _               => 3,
    };

    private static int SeverityOrder(string severity) => severity switch
    {
        "buy"    => 0,
        "trim"   => 1,
        "review" => 2,
        "danger" => 3,
        "wait"   => 4,
        _        => 5,
    };

    private static T? Deserialize<T>(string json)
    {
        try { return JsonSerializer.Deserialize<T>(json, JsonOpts); }
        catch { return default; }
    }
}
