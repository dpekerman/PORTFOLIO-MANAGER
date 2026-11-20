using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services.FinalAction;

namespace PortfolioManager.Api.Services;

public interface IPortfolioActionsService
{
    Task<IReadOnlyList<PortfolioActionDto>> GetActionsAsync(string userId, CancellationToken ct = default);
}

public sealed class PortfolioActionsService(AppDbContext db, IFinalActionService? finalActions = null) : IPortfolioActionsService
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<PortfolioActionDto>> GetActionsAsync(string userId, CancellationToken ct = default)
    {
        var nowUtc = DateTime.UtcNow;
        // Cache-only: a read never triggers a market-data scan and never renews a recommendation.
        var canonicalSnapshot = finalActions?.TryGetCached(userId);
        var evalStatus = await EvaluationStatusReader.ReadAsync(db, userId, nowUtc, ct);
        var activeWatchlistEvents = (await db.SignalAuditEvents.AsNoTracking()
                .Where(e => e.UserId == userId && e.Status == SignalStatuses.Active && e.EpisodeEndedDate == null
                            && e.Source == SignalSources.Watchlist && e.SignalType.StartsWith(SignalTypeCatalog.FinalActionPrefix))
                .ToListAsync(ct))
            .GroupBy(e => e.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.LastValidatedAtUtc ?? e.CapturedAtUtc).First(), StringComparer.OrdinalIgnoreCase);

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
            var persistedFacts = pos?.TechnicalFacts ?? wlItem?.TechnicalFacts;
            var persistedStructure = persistedFacts?.PriceStructure ?? pos?.PriceStructure ?? wlItem?.PriceStructure;
            var hasScannerData = scan is not null;
            scan ??= new RsiScanResult
            {
                Symbol = symbol,
                CompanyName = pos?.Item.CompanyName ?? wlItem?.Item.Symbol ?? symbol,
                ScanType = ScanType.Neutral,
                Status = SignalStatus.Neutral,
                TrendShift = string.Empty,
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
            var priceStructure = scan.PriceStructure?.HasHardStructuralNegative == true
                ? scan.PriceStructure
                : persistedStructure?.HasHardStructuralNegative == true
                    ? persistedStructure
                    : HasPriceStructure(scan.PriceStructure)
                        ? scan.PriceStructure
                        : persistedStructure;
            var hasPriceStructure = HasPriceStructure(priceStructure);
            var hasActiveEod = IsActiveEod(persistedFacts);

            if (!isHolding && scan.Status == SignalStatus.Neutral && !hasPriceStructure && !hasActiveEod) continue;

            var inclusionReason = InclusionReason(scan, priceStructure, hasScannerData);
            var decision = ResolveDecision(symbol, isHolding, pos, holdingRole, canonicalSnapshot,
                isHolding ? null : activeWatchlistEvents.GetValueOrDefault(symbol), evalStatus, nowUtc);
            var actionLabel = decision.Label;
            var severity = decision.Severity;
            var priority = decision.Priority;

            results.Add(new PortfolioActionDto(
                Symbol:           symbol,
                CompanyName:      scan.CompanyName,
                HoldingRole:      holdingRole,
                ScanType:         scan.ScanType.ToString(),
                Rsi:              hasScannerData ? scan.Rsi : persistedFacts?.Rsi,
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
                ChannelTouchDetails: scan.ChannelTouchDetails,
                MaStructure: scan.MaStructure ?? persistedFacts?.MaStructure,
                MomentumState: scan.MomentumState ?? persistedFacts?.MomentumState,
                PriceStructure: priceStructure,
                InclusionReason: inclusionReason,
                TechnicalCalculatedAt: priceStructure?.CalculatedAt ?? persistedFacts?.CalculatedAt,
                LatestEodSignalState: persistedFacts?.LatestEodSignalState,
                LatestEodScanType: persistedFacts?.LatestEodScanType,
                LatestEodTrendShift: persistedFacts?.LatestEodTrendShift,
                LatestEodIsNew: persistedFacts?.LatestEodIsNew ?? false,
                LatestEodIsInvalidated: persistedFacts?.LatestEodIsInvalidated ?? false,
                EvaluationState: decision.State,
                IsActionable: decision.IsActionable,
                LastValidAction: decision.LastValidAction,
                LastEvaluatedAtUtc: decision.LastEvaluatedAtUtc,
                ReasonCodes: decision.Codes));
        }

        return results
            .OrderBy(r => PriorityOrder(r.ActionPriority))
            .ThenByDescending(r => r.IsInPortfolio)
            .ThenBy(r => SeverityOrder(r.ActionSeverity))
            .ThenBy(r => r.Rsi.HasValue ? r.ScanType == "Oversold" ? r.Rsi.Value : 100 - r.Rsi.Value : decimal.MaxValue)
            .ToList()
            .AsReadOnly();
    }

    // The decision shown for a symbol (label, severity, priority, state) is resolved from canonical Final Action
    // evaluations and persisted recommendations only — see ResolveDecision. No decision rules live in this service.

    private sealed record Decision(
        string Label, string Severity, string Priority, string State, bool IsActionable,
        string? LastValidAction, DateTime? LastEvaluatedAtUtc, IReadOnlyList<string> Codes);

    private const string AwaitingLabel = "Awaiting Data";

    /// <summary>
    /// The one place this service picks a label. A fresh canonical result wins; otherwise the last persisted
    /// recommendation (published holding action, or the active Signal Tracker event) is retained only while the status
    /// resolver says it is still valid; everything else is Awaiting Data. Absence of evidence never becomes a Hold or a trade.
    /// </summary>
    private static Decision ResolveDecision(
        string symbol, bool isHolding, PortfolioSummaryDto? pos, string? role, FinalActionSnapshotDto? snapshot,
        SignalAuditEvent? activeEvent, EvaluationStatus eval, DateTime utcNow)
    {
        var source = isHolding ? SignalSources.Portfolio : SignalSources.Watchlist;
        var results = isHolding ? snapshot?.Portfolio : snapshot?.Watchlist;
        var canonical = results?.Where(r => string.Equals(r.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r.EvaluationState == nameof(EffectiveState.AwaitingData) ? 1 : 0).FirstOrDefault();

        if (canonical is { EvaluationState: nameof(EffectiveState.CurrentValid) })
            return new Decision(canonical.FinalAction, canonical.Severity, canonical.Priority, canonical.EvaluationState!,
                canonical.IsActionable, null, canonical.LastSuccessfulEvaluationUtc, canonical.ReasonCodes ?? []);
        if (canonical is not null)
            return Awaiting(canonical.LastValidAction ?? PublishedAction(pos), canonical.LastSuccessfulEvaluationUtc ?? pos?.Item.FinalActionUpdatedAt,
                canonical.ReasonCodes ?? []);

        PriorRecommendation? prior = null;
        string? priorLabel = null, severity = null, priority = null;
        if (isHolding && pos is { Item.FinalAction: { Length: > 0 } published, Item.FinalActionUpdatedAt: { } stamp })
        {
            prior = new PriorRecommendation(published, source, pos.Item.HoldingRole ?? role, SignalStatuses.Active, stamp, stamp);
            priorLabel = published; severity = pos.Item.FinalActionSeverity; priority = pos.Item.FinalActionPriority;
        }
        else if (!isHolding && activeEvent is not null)
        {
            var id = activeEvent.SignalType[SignalTypeCatalog.FinalActionPrefix.Length..];
            prior = new PriorRecommendation(id, source, activeEvent.Role ?? role, activeEvent.Status, activeEvent.CapturedAtUtc, activeEvent.LastValidatedAtUtc);
            priorLabel = FinalActionCatalog.Describe(id).Label;
        }

        var status = FinalActionStatusResolver.Resolve(new ResolveInput(
            null, DecisionBucket.None, prior, eval.Run, eval.LastCompletePassUtc, eval.LastAttemptFailed,
            TradingCalendar.ExchangeFor(symbol), utcNow));

        if (status.State == EffectiveState.PriorSessionValid && priorLabel is not null)
        {
            var (sev, pri) = FinalActionEngine.ActionCenterClassification(FinalActionEngine.FinalActionClass(priorLabel));
            return new Decision(priorLabel, severity ?? sev, priority ?? pri, status.State.ToString(), status.IsActionable,
                null, prior?.LastValidatedAtUtc, status.Codes);
        }
        return Awaiting(priorLabel, prior?.LastValidatedAtUtc, status.Codes, status.State == EffectiveState.Evaluating);
    }

    private static string? PublishedAction(PortfolioSummaryDto? pos) => pos?.Item.FinalAction;

    private static Decision Awaiting(string? lastValid, DateTime? lastEvaluated, IReadOnlyList<string> codes, bool evaluating = false) =>
        new(evaluating ? "Evaluating" : AwaitingLabel, "hold", "INFORMATIONAL",
            evaluating ? nameof(EffectiveState.Evaluating) : nameof(EffectiveState.AwaitingData), false, lastValid, lastEvaluated, codes);

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

    private static bool HasPriceStructure(PriceStructureResult? structure) =>
        structure is not null && (structure.PrimaryPatternType != "NONE" || structure.KeyLevelState != "NONE");

    private static bool IsActiveEod(SharedTechnicalFacts? facts) =>
        string.Equals(facts?.LatestEodSignalState, "Active", StringComparison.OrdinalIgnoreCase);

    private static string InclusionReason(RsiScanResult scan, PriceStructureResult? structure, bool hasScannerData)
    {
        var state = structure?.KeyLevelState ?? "NONE";
        if (state == "SUPPORT_TEST") return "PRICE_STRUCTURE_SUPPORT_TEST";
        if (state is "RESISTANCE_TEST" or "APPROACHING_RESISTANCE") return "PRICE_STRUCTURE_BREAKOUT_WATCH";
        if (structure?.HasHardStructuralNegative == true) return "HARD_STRUCTURE_NEGATIVE";
        if (HasPriceStructure(structure)) return "PRIORITY_TECHNICAL_SETUP";
        return hasScannerData && scan.Status != SignalStatus.Neutral ? "RSI_SIGNAL" : "NO_CURRENT_DECISION_LEVEL";
    }

    private static T? Deserialize<T>(string json)
    {
        try { return JsonSerializer.Deserialize<T>(json, JsonOpts); }
        catch { return default; }
    }
}
