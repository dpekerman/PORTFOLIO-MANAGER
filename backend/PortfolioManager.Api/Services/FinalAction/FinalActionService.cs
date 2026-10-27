using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services.FinalAction;

/// <summary>One canonical Final Action evaluation for a watchlist symbol or a portfolio position.</summary>
public sealed record FinalActionResultDto(
    string Symbol,
    string Source,
    int? ItemId,
    string? Role,
    string FinalAction,
    string FinalActionId,
    string Category,
    string Direction,
    string Severity,
    string Priority,
    string FinalActionClass,
    string TrendSetup,
    string MomentumShift,
    string BaseAction,
    string GapStatus,
    string HoverDescription,
    string? Reason,
    bool HasHardStructuralNegative,
    /// <summary>Analysis-security price the evaluation used (signal reference price).</summary>
    decimal AnalysisPrice,
    string AnalysisTicker,
    string? AnalysisCurrency,
    string TrendSetupReason = "",
    string MomentumShiftReason = "",
    string TrendSetupClass = "",
    string MomentumShiftClass = "",
    int GapScore = 0,
    double GapPct = 0,
    FinalActionDiagnosticsDto? Diagnostics = null,
    FinalActionTechnicalsDto? Technicals = null,
    /// <summary>True when the result is an Entry/Add, Trim/Exit or Risk Review decision (one rule for every consumer).</summary>
    bool IsActionable = false,
    IReadOnlyList<string>? ReasonCodes = null,
    /// <summary>When the technical evidence was computed.</summary>
    DateTime? EvidenceAsOfUtc = null,
    DateOnly? EntryDate = null,
    /// <summary>The technical action the portfolio gate replaced, if any.</summary>
    string? OriginalAction = null);

/// <summary>Technical readings the decision was based on, kept with the audited signal for later review.</summary>
public sealed record FinalActionTechnicalsDto(
    decimal Rsi,
    string TrendShift,
    string? MaStructure,
    string? MaCrossState,
    PriceStructureResult PriceStructure,
    string FibZone,
    string FibStatus,
    decimal Fib61_8,
    decimal DynamicStopLoss);

/// <summary>Why a watchlist entry decision came out as it did (shown in tooltips).</summary>
public sealed record FinalActionDiagnosticsDto(
    string PriceStructureState,
    string MomentumState,
    string MaStructure,
    int? BuyScore,
    string Role);

public sealed record FinalActionSnapshotDto(
    IReadOnlyList<FinalActionResultDto> Watchlist,
    IReadOnlyList<FinalActionResultDto> Portfolio,
    DateTime ComputedAtUtc,
    FinalActionEvaluationMeta? Meta = null);

public interface IFinalActionService
{
    /// <summary>
    /// Evaluates every watchlist symbol and open portfolio holding of the user with the canonical rules.
    /// Results are cached briefly; pass <paramref name="fresh"/> to force a new scan (EOD capture does).
    /// </summary>
    Task<FinalActionSnapshotDto> ComputeAsync(string userId, bool fresh = false, CancellationToken ct = default);

    /// <summary>
    /// Writes the canonical portfolio Final Actions into the portfolio snapshot that the Dashboard Action Center
    /// reads, so it no longer depends on an open browser to sync them. Returns the number of holdings written.
    /// </summary>
    Task<int> PublishPortfolioActionsAsync(string userId, FinalActionSnapshotDto snapshot, CancellationToken ct = default);
}

public sealed class FinalActionService(
    AppDbContext db,
    IRsiScannerService scanner,
    IPortfolioSnapshotService portfolioSnapshot,
    IMemoryCache cache,
    ILogger<FinalActionService> logger) : IFinalActionService
{
    // Same parameters the Angular watchlist analysis uses (analyzeSymbols(batch, 30, 75, 'Enhanced')).
    private const decimal OversoldThreshold = 30m;
    private const decimal OverboughtThreshold = 75m;
    private const string LogicMode = "Enhanced";
    private const int BatchSize = 100;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<FinalActionSnapshotDto> ComputeAsync(string userId, bool fresh = false, CancellationToken ct = default)
    {
        var key = $"final-actions:{userId}";
        if (!fresh && cache.TryGetValue(key, out FinalActionSnapshotDto? hit) && hit is not null) return hit;

        var watchlistItems = await db.WatchlistItems.AsNoTracking()
            .Where(w => w.UserId == userId || w.UserId == null).ToListAsync(ct);
        var portfolioSnap = await db.PortfolioSnapshots.AsNoTracking().SingleOrDefaultAsync(s => s.UserId == userId, ct);
        var summaries = Deserialize<List<PortfolioSummaryDto>>(portfolioSnap?.SnapshotJson) ?? [];
        var openHoldings = summaries
            .Where(s => !IsClose(s.Item.TransactionType) && !s.Item.IsManual).ToList();

        var symbols = watchlistItems.Select(w => w.Symbol)
            .Concat(openHoldings.Select(h => h.Item.Symbol))
            .Select(s => s.ToUpperInvariant()).Distinct().ToList();

        var scans = new Dictionary<string, RsiScanResult>(StringComparer.OrdinalIgnoreCase);
        var unknown = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var batch in symbols.Chunk(BatchSize))
        {
            try
            {
                foreach (var r in await scanner.AnalyzeSymbolsAsync(batch, OversoldThreshold, OverboughtThreshold, LogicMode, userId, ct))
                    scans[r.Symbol] = r;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "[FinalAction] Scan batch of {Count} symbols failed; those symbols get no decision.", batch.Length);
                foreach (var s in batch) unknown.Add(s);
            }
        }

        // A symbol with no scan or no usable price was not evaluated: it must never confirm or invalidate anything.
        foreach (var s in symbols)
            if (!scans.TryGetValue(s, out var sc) || sc.CurrentPrice <= 0m) unknown.Add(s);

        var valueByTicker = await LoadValueScreenerAsync(ct);

        var watchlist = new List<FinalActionResultDto>();
        foreach (var item in watchlistItems)
        {
            if (!scans.TryGetValue(item.Symbol, out var scan)) continue;
            valueByTicker.TryGetValue(item.Symbol, out var vs);
            var ctx = vs is null ? null : new WatchlistValueContext((double)vs.Score, vs.ActionTrigger == ActionTrigger.ValueTrapWarning, null);
            var role = item.Role ?? "Strategic";
            var decision = FinalActionEngine.TranslateForWatchlist(ToInput(scan), role, ctx);
            watchlist.Add(ToDto(decision, item.Symbol, SignalSources.Watchlist, null, role, scan));
        }

        var grandTotal = await GrandTotalAsync(userId, summaries, ct);
        var positionLimits = await db.SinglePositionLimits.AsNoTracking().ToListAsync(ct);
        var sectorTargets = await db.AllocationSectorTargets.AsNoTracking().ToListAsync(ct);
        var stockTotal = summaries.Where(s => !IsClose(s.Item.TransactionType)).Sum(MarketValue);
        var portfolio = new List<FinalActionResultDto>();
        foreach (var holding in openHoldings)
        {
            if (!scans.TryGetValue(holding.Item.Symbol, out var scan)) continue;
            var context = BuildContext(holding, scan, summaries, grandTotal);
            var decision = FinalActionEngine.TranslateForPortfolio(ToInput(scan), holding.Item.HoldingRole, context);
            var evidence = BuildEvidence(holding, scan, openHoldings, summaries, grandTotal, stockTotal, positionLimits, sectorTargets, context);
            var gate = PortfolioEvidenceGate.Apply(decision, holding.Item.HoldingRole, evidence);
            portfolio.Add(ToDto(gate.Decision, holding.Item.Symbol, SignalSources.Portfolio, holding.Item.Id, holding.Item.HoldingRole, scan)
                with
                {
                    ReasonCodes = gate.ReasonCodes,
                    EvidenceAsOfUtc = evidence.EvidenceAsOfUtc,
                    EntryDate = evidence.EntryDate,
                    OriginalAction = gate.OriginalAction,
                });
        }

        var meta = new FinalActionEvaluationMeta(
            unknown.Select(s => s.ToUpperInvariant()).ToHashSet(), portfolioSnap?.UpdatedAt, portfolioSnap is not null);
        var snapshot = new FinalActionSnapshotDto(watchlist, portfolio, DateTime.UtcNow, meta);
        cache.Set(key, snapshot, CacheTtl);
        return snapshot;
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    public async Task<int> PublishPortfolioActionsAsync(string userId, FinalActionSnapshotDto snapshot, CancellationToken ct = default)
    {
        var items = snapshot.Portfolio
            .Where(p => p.ItemId is not null)
            .Select(p => new FinalActionSyncItem(p.ItemId!.Value, p.FinalAction, p.Severity, p.Priority))
            .ToList();
        if (items.Count == 0) return 0;
        await portfolioSnapshot.PatchFinalActionsAsync(userId, items, ct);
        return items.Count;
    }

    private static FinalActionResultDto ToDto(
        FinalActionDecision d, string symbol, string source, int? itemId, string? role, RsiScanResult scan)
    {
        var def = FinalActionCatalog.Describe(d.FinalAction);
        var (severity, priority) = FinalActionEngine.ActionCenterClassification(d.FinalActionClass);
        var dto = new FinalActionResultDto(
            symbol, source, itemId, role, d.FinalAction, def.Id, def.Category, def.Direction, severity, priority,
            d.FinalActionClass, d.TrendSetup, d.MomentumShift, d.BaseAction, d.GapStatus, d.HoverDescription,
            d.FinalActionReason, d.WatchlistDiagnostics?.HasHardStructuralNegative ?? false,
            scan.CurrentPrice, string.IsNullOrEmpty(scan.AnalysisTicker) ? scan.Symbol : scan.AnalysisTicker,
            string.IsNullOrEmpty(scan.AnalysisCurrency) ? null : scan.AnalysisCurrency,
            d.TrendSetupReason, d.MomentumShiftReason, d.TrendSetupClass, d.MomentumShiftClass, d.GapScore, d.GapPct,
            d.WatchlistDiagnostics is { } w
                ? new FinalActionDiagnosticsDto(w.PriceStructureState, w.MomentumState, w.MaStructure, w.BuyScore, w.Role)
                : null,
            new FinalActionTechnicalsDto(scan.Rsi, scan.TrendShift, scan.MaStructure, scan.MaCrossState, scan.PriceStructure,
                scan.FibZone, scan.FibStatus, scan.Fib61_8, scan.DynamicStopLoss));
        return dto with { IsActionable = DecisionEligibility.IsActionable(dto), EvidenceAsOfUtc = scan.ScannedAt };
    }

    private static PortfolioEvidence BuildEvidence(
        PortfolioSummaryDto holding, RsiScanResult scan, IReadOnlyList<PortfolioSummaryDto> openHoldings,
        IReadOnlyList<PortfolioSummaryDto> all, double grandTotal, decimal stockTotal,
        IReadOnlyList<SinglePositionLimit> limits, IReadOnlyList<AllocationSectorTarget> sectorTargets, PortfolioItemContext context)
    {
        var item = holding.Item;
        var lots = openHoldings.Where(h => string.Equals(h.Item.Symbol, item.Symbol, StringComparison.OrdinalIgnoreCase)).ToList();
        var openDates = lots.Where(l => l.Item.OpenDate is not null).Select(l => DateOnly.FromDateTime(l.Item.OpenDate!.Value)).ToList();
        DateOnly? entry = openDates.Count > 0 ? openDates.Max() : null;

        var evidenceUtc = scan.ScannedAt.Kind == DateTimeKind.Local ? scan.ScannedAt.ToUniversalTime()
            : DateTime.SpecifyKind(scan.ScannedAt, DateTimeKind.Utc);
        var evidenceDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(evidenceUtc, EasternZone()));

        double? positionSize = grandTotal > 0 ? Dbl(lots.Sum(MarketValue)) / grandTotal * 100 : null;
        var limit = limits.FirstOrDefault(l => string.Equals(l.Role, item.HoldingRole ?? "Strategic", StringComparison.OrdinalIgnoreCase));

        var sector = item.Sector ?? scan.Sector ?? "";
        var target = string.IsNullOrEmpty(sector) ? null
            : sectorTargets.FirstOrDefault(t => string.Equals(t.Sector, sector, StringComparison.OrdinalIgnoreCase));
        var sectorOver = false;
        if (target is not null && stockTotal > 0)
        {
            var actual = all.Where(s => !IsClose(s.Item.TransactionType)
                    && string.Equals(s.Item.Sector ?? "", sector, StringComparison.OrdinalIgnoreCase)).Sum(MarketValue) / stockTotal * 100m;
            sectorOver = actual - target.TargetPct > 2m;
        }

        return new PortfolioEvidence(
            context.UnrealizedGainPct, entry, evidenceDate, evidenceUtc, positionSize,
            limit is null ? null : Dbl(limit.TargetPct), sectorOver,
            scan.PriceStructure?.HasHardStructuralNegative == true, item.DecisionSource);
    }

    private static decimal MarketValue(PortfolioSummaryDto s) => s.Item.IsManual
        ? s.Item.ManualMarketValue ?? s.Item.AverageCostBasis * s.Item.Shares
        : (s.Quote?.CurrentPrice ?? s.Item.AverageCostBasis) * s.Item.Shares;

    private static TimeZoneInfo EasternZone() =>
        TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "Eastern Standard Time" : "America/New_York");

    /// <summary>Doubles are parsed from the decimal's text so values equal what the JSON API hands to JavaScript.</summary>
    private static double Dbl(decimal v) => double.Parse(v.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    private static double? Dbl(decimal? v) => v.HasValue ? Dbl(v.Value) : null;

    public static FinalActionInput ToInput(RsiScanResult r)
    {
        var ps = r.PriceStructure;
        return new FinalActionInput
        {
            Symbol = r.Symbol,
            Rsi = Dbl(r.Rsi),
            CurrentPrice = Dbl(r.CurrentPrice),
            ChangePercent = Dbl(r.ChangePercent),
            DayHigh = Dbl(r.DayHigh),
            DayLow = Dbl(r.DayLow),
            OpenPrice = Dbl(r.OpenPrice),
            PreviousClose = Dbl(r.PreviousClose),
            VolumeRatio = Dbl(r.VolumeRatio),
            RsiSignal = Dbl(r.RsiSignal),
            RsiSignalAvailable = r.RsiSignalAvailable,
            MacdHistogram = Dbl(r.MacdHistogram),
            MacdHistDelta = Dbl(r.MacdHistDelta),
            Ema9 = Dbl(r.Ema9Price),
            Ema10 = Dbl(r.Ema10Price),
            Ema20 = Dbl(r.Ema20Price),
            Sma20 = Dbl(r.Sma20Price),
            Sma50 = Dbl(r.Sma50Price),
            Sma200 = Dbl(r.Sma200),
            Status = r.Status.ToString(),
            ScanType = r.ScanType.ToString(),
            AnalystTargetPrice = Dbl(r.AnalystTargetPrice),
            Fib50 = Dbl(r.Fib50),
            Fib61_8 = Dbl(r.Fib61_8),
            Fib78_6 = Dbl(r.Fib78_6),
            FibZone = r.FibZone ?? "",
            FibStatus = r.FibStatus ?? "",
            RsiDelta1D = Dbl(r.RsiDelta1D),
            TrendSetup200 = r.TrendSetup200 ?? "",
            TrendShift = r.TrendShift ?? "",
            // The JavaScript rule is `!!r.chaseRisk` on a string: any non-empty text counts.
            ChaseRisk = !string.IsNullOrEmpty(r.ChaseRisk),
            Week52High = Dbl(r.Week52High),
            PriceStructure = ps is null ? null : new FinalActionPriceStructure(
                ps.HasHardStructuralNegative, ps.KeyLevelRole, ps.KeyLevelState, ps.PrimaryPatternState, ps.PrimaryPatternType),
        };
    }

    // ── Portfolio context (port of PortfolioFinalActionSyncService) ──────────

    private static PortfolioItemContext BuildContext(
        PortfolioSummaryDto holding, RsiScanResult scan, IReadOnlyList<PortfolioSummaryDto> all, double grandTotal)
    {
        var item = holding.Item;
        double? tradedPrice = all.FirstOrDefault(s => s.Item.Id == item.Id)?.Quote is { } q
            ? Dbl(q.CurrentPrice)
            : scan.UsesUnderlyingSecurity ? null : Dbl(scan.CurrentPrice);
        var analysisPrice = Dbl(scan.CurrentPrice);
        var avgCost = Dbl(item.AverageCostBasis);
        double? gain = tradedPrice is not null && avgCost > 0 ? (tradedPrice.Value - avgCost) / avgCost * 100 : null;
        double? holdingDays = item.OpenDate is { } open ? Math.Floor((DateTime.UtcNow - open).TotalDays) : null;
        var w52 = Dbl(scan.Week52High);
        double? dist52 = w52 > 0 ? (analysisPrice - w52) / w52 * 100 : null;
        double? marketValue = item.IsManual
            ? Dbl(item.ManualMarketValue ?? item.AverageCostBasis)
            : tradedPrice is not null ? tradedPrice.Value * Dbl(item.Shares) : null;
        double? positionSize = grandTotal > 0 && marketValue is not null ? marketValue.Value / grandTotal * 100 : null;

        return new PortfolioItemContext(
            item.AccountType, gain, holdingDays, dist52, positionSize,
            RiskControlClosePct(item.Symbol, all), item.DecisionSource);
    }

    private static double? RiskControlClosePct(string symbol, IReadOnlyList<PortfolioSummaryDto> all)
    {
        var closes = all.Where(s => s.Item.Symbol == symbol && IsClose(s.Item.TransactionType) && s.Item.DecisionSource == "Risk Control").ToList();
        if (closes.Count == 0) return null;
        var closedShares = closes.Sum(s => Dbl(s.Item.Shares));
        var openShares = all.Where(s => s.Item.Symbol == symbol && !IsClose(s.Item.TransactionType)).Sum(s => Dbl(s.Item.Shares));
        var total = openShares + closedShares;
        return total > 0 ? closedShares / total * 100 : null;
    }

    /// <summary>Stocks + cash + options, the same denominator the Portfolio page and Dashboard use.</summary>
    private async Task<double> GrandTotalAsync(string userId, IReadOnlyList<PortfolioSummaryDto> summaries, CancellationToken ct)
    {
        var stocks = summaries.Where(s => !IsClose(s.Item.TransactionType)).Sum(s => s.Item.IsManual
            ? (s.Item.ManualMarketValue ?? s.Item.AverageCostBasis * s.Item.Shares)
            : (s.Quote?.CurrentPrice ?? s.Item.AverageCostBasis) * s.Item.Shares);
        var cash = await db.CashItems.Where(c => c.UserId == userId || c.UserId == null).SumAsync(c => c.Amount, ct);
        var options = await db.OptionItems
            .Where(o => (o.UserId == userId || o.UserId == null) && o.TransactionType != "CLOSE")
            .SumAsync(o => o.MarketPrice * o.NumberOfContracts * 100, ct);
        return Dbl(stocks + cash + options);
    }

    private async Task<Dictionary<string, ValueScreenerResult>> LoadValueScreenerAsync(CancellationToken ct)
    {
        var map = new Dictionary<string, ValueScreenerResult>(StringComparer.OrdinalIgnoreCase);
        // Same precedence as the Watchlist page: watchlist results first, portfolio results override.
        foreach (var origin in new[] { "Watchlist", "Portfolio" })
        {
            var snap = await db.ValueScreenerSnapshots.AsNoTracking()
                .Where(s => s.Origin == origin).OrderByDescending(s => s.RunAt).FirstOrDefaultAsync(ct);
            foreach (var r in Deserialize<List<ValueScreenerResult>>(snap?.ResultsJson) ?? [])
                map[r.Symbol] = r;
        }
        return map;
    }

    private static bool IsClose(string? type) => string.Equals(type, "CLOSE", StringComparison.OrdinalIgnoreCase);

    private static T? Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        try { return JsonSerializer.Deserialize<T>(json, JsonOpts); }
        catch (JsonException) { return default; }
    }
}
