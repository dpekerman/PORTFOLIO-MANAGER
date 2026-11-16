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
    string? OriginalAction = null,
    /// <summary>CurrentValid | PriorSessionValid | AwaitingData | Evaluating | Expired | HistoricalWarning.</summary>
    string? EvaluationState = null,
    /// <summary>Fresh | Stale | Missing | Invalid | Failed — health of the market evidence, independent of any lifecycle.</summary>
    string? EvidenceHealth = null,
    /// <summary>Last valid published action, kept separately when this result is Awaiting Data (never refreshed by a UI load).</summary>
    string? LastValidAction = null,
    DateTime? LastSuccessfulEvaluationUtc = null,
    DateOnly? DataAsOfDate = null,
    DateOnly? ExpectedSession = null,
    DateTime? QuoteTimestampUtc = null,
    bool IsHistoricalWarning = false,
    /// <summary>Verified = a source quote timestamp was checked against the expected session; Unavailable = none exists, so the scan's bar date is the only session-level evidence (freshness of the quote itself is NOT independently verified).</summary>
    string? QuoteVerification = null,
    /// <summary>How the exchange (which decides the session calendar) was resolved: Metadata | Suffix | Assumed (NYSE default when nothing identifies the market).</summary>
    string? ExchangeBasis = null,
    /// <summary>Read-only stop-risk facts for an open Swing lot (observed breach, coverage, verification). Never an execution claim.</summary>
    StopRiskEvidence? Risk = null,
    /// <summary>What the decision knows about current ownership of the symbol (open lots only; closed lots never count).</summary>
    FinalActionOwnership? Ownership = null,
    /// <summary>SingleCurrency | MixedCurrency | UnknownCurrency: whether allocation percentages behind the decision are verifiable.</summary>
    string? AllocationStatus = null,
    /// <summary>When the app fetched the quote (distinct from <see cref="QuoteTimestampUtc"/>, the provider's own price time).</summary>
    DateTime? QuoteFetchedAtUtc = null,
    RejectedMarketBar? RejectedBar = null,
    bool IntradayRangeReconstructed = false);

/// <summary>Ownership facts across all open lots and accounts of a symbol. Exposure is null when it cannot be verified.</summary>
public sealed record FinalActionOwnership(
    bool Owned,
    int OpenLots,
    IReadOnlyList<string> Accounts,
    IReadOnlyList<string> Roles,
    bool MixedRoles,
    double? ExposurePct);

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

    /// <summary>The cached evaluation, if any. Never scans: read-only consumers (Dashboard, Action Center rows) use it so a page load cannot trigger market-data calls or renew a recommendation.</summary>
    FinalActionSnapshotDto? TryGetCached(string userId) => null;

    /// <summary>
    /// PM-029: the same evaluation as <see cref="ComputeAsync"/>, returned as one immutable bundle together with the exact scans and
    /// portfolio basis the decisions were made from, so consumers never re-derive or re-scan. Fresh evaluations are single-flight per user.
    /// </summary>
    Task<FinalActionEvaluation> EvaluateAsync(string userId, bool fresh = false, CancellationToken ct = default) =>
        throw new NotSupportedException();

    /// <summary>
    /// PM-029: evaluates symbols the user neither watches nor holds through the identical watchlist decision path (role Strategic),
    /// side-effect free: read-only scan, never published, never cached as the user's Final Actions, never fed to lifecycle/Action Center.
    /// </summary>
    Task<UniverseFinalActions> EvaluateUniverseAsync(string userId, IReadOnlyCollection<string> symbols, CancellationToken ct = default) =>
        throw new NotSupportedException();

    /// <summary>Runs the authoritative user evaluation with read-only scanner and staged-signal preview evidence.</summary>
    Task<FinalActionEvaluation> EvaluatePreviewAsync(string userId, CancellationToken ct = default) =>
        throw new NotSupportedException("This Final Action service does not support read-only evaluation previews.");
}

/// <summary>Portfolio numbers a Final Action evaluation used. Consumers (PM-029 Portfolio Fit) must use these, never recompute them.</summary>
public sealed record FinalActionPortfolioBasis(
    IReadOnlyList<PortfolioSummaryDto> Summaries,
    IReadOnlyList<PortfolioSummaryDto> OpenHoldings,
    double GrandTotal,
    decimal StockTotal,
    ValuationCheck Valuation,
    IReadOnlyList<SinglePositionLimit> PositionLimits,
    IReadOnlyList<AllocationSectorTarget> SectorTargets,
    DateTime? PortfolioUpdatedAt);

/// <summary>PM-029 Portfolio Fit input derived with the Final Action exposure arithmetic (see FinalActionService.ExposureFor).</summary>
public sealed record PortfolioExposure(
    bool Owned,
    bool AllocationReliable,
    double? PositionSizePct,
    double? PositionLimitPct,
    bool RolePolicyAmbiguous,
    bool SectorKnown,
    string Sector,
    double? SectorPct,
    double? SectorTargetPct);

/// <summary>One immutable Final Action evaluation: the decisions plus the evidence they were made from.</summary>
public sealed record FinalActionEvaluation(
    Guid EvaluationId,
    FinalActionSnapshotDto Snapshot,
    IReadOnlyDictionary<string, RsiScanResult> Scans,
    FinalActionPortfolioBasis Portfolio);

public sealed record UniverseFinalActions(
    IReadOnlyList<FinalActionResultDto> Rows,
    IReadOnlyDictionary<string, RsiScanResult> Scans,
    IReadOnlySet<string> Unknown,
    DateTime ComputedAtUtc);

public sealed class FinalActionService(
    AppDbContext db,
    IRsiScannerService scanner,
    IPortfolioSnapshotService portfolioSnapshot,
    IMemoryCache cache,
    ILogger<FinalActionService> logger,
    PortfolioManager.Api.Services.Risk.IOpenRiskReader? riskReader = null,
    PortfolioManager.Api.Services.Valuation.IUsdCadRateSource? usdCadRates = null) : IFinalActionService
{
    // Same parameters the Angular watchlist analysis uses (analyzeSymbols(batch, 30, 75, 'Enhanced')).
    private const decimal OversoldThreshold = 30m;
    private const decimal OverboughtThreshold = 75m;
    private const string LogicMode = "Enhanced";
    private const int BatchSize = 100;
    private enum ScannerEvaluationMode { Persist, ReadOnly, Preview }
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);
    /// <summary>Failed, inconsistent or incomplete market data can recover within minutes: such a snapshot must not be reused for the full TTL.</summary>
    private static readonly TimeSpan TransientCacheTtl = TimeSpan.FromMinutes(1);
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    /// <summary>PM-029 row source for symbols evaluated only because they are in the opportunity universe (TSX 60).</summary>
    public const string UniverseSource = "Universe";

    public FinalActionSnapshotDto? TryGetCached(string userId) =>
        cache.TryGetValue($"final-actions:{userId}", out FinalActionSnapshotDto? hit) && IsStillCurrent(hit, DateTime.UtcNow) ? hit : null;

    /// <summary>
    /// A cached snapshot is only served while every decision it holds is still based on the last completed session. When a session
    /// completes (or the grace period ends) after the snapshot was computed, its actions are no longer current and are recomputed.
    /// </summary>
    internal static bool IsStillCurrent(FinalActionSnapshotDto? snapshot, DateTime nowUtc) =>
        snapshot is not null
        && snapshot.Watchlist.Concat(snapshot.Portfolio).All(r =>
            r.EvaluationState != nameof(EffectiveState.CurrentValid)
            || r.DataAsOfDate is not { } asOf
            || asOf >= TradingCalendar.LastCompletedSession(nowUtc, TradingCalendar.ExchangeFor(r.Symbol, null)));

    private static bool HasTransientEvidenceProblem(FinalActionSnapshotDto s) =>
        s.Watchlist.Concat(s.Portfolio).Any(r => r.EvidenceHealth is nameof(EvidenceHealth.Failed) or nameof(EvidenceHealth.Invalid) or nameof(EvidenceHealth.Missing));

    public async Task<FinalActionSnapshotDto> ComputeAsync(string userId, bool fresh = false, CancellationToken ct = default)
    {
        if (!fresh && cache.TryGetValue(SnapshotKey(userId), out FinalActionSnapshotDto? hit) && hit is not null && IsStillCurrent(hit, DateTime.UtcNow)) return hit;
        return (await EvaluateAsync(userId, fresh, ct)).Snapshot;
    }

    private static string SnapshotKey(string userId) => $"final-actions:{userId}";
    private static string BundleKey(string userId) => $"final-actions-bundle:{userId}";
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.Ordinal);

    public async Task<FinalActionEvaluation> EvaluateAsync(string userId, bool fresh = false, CancellationToken ct = default)
    {
        var requestedAtUtc = DateTime.UtcNow;
        if (!fresh && TryGetBundle(userId, requestedAtUtc) is { } cached) return cached;

        // Single-flight: concurrent callers wait for the evaluation in progress instead of scanning the same symbols again.
        // A fresh caller accepts a bundle only if it was computed after its own request started.
        var gate = Gates.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            if (TryGetBundle(userId, DateTime.UtcNow) is { } done && (!fresh || done.Snapshot.ComputedAtUtc >= requestedAtUtc)) return done;
            return await ComputeCoreAsync(userId, ScannerEvaluationMode.Persist, ct);
        }
        finally { gate.Release(); }
    }

    public async Task<FinalActionEvaluation> EvaluatePreviewAsync(string userId, CancellationToken ct = default)
    {
        var gate = Gates.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            return await ComputeCoreAsync(userId, ScannerEvaluationMode.Preview, ct);
        }
        finally { gate.Release(); }
    }

    private FinalActionEvaluation? TryGetBundle(string userId, DateTime nowUtc) =>
        cache.TryGetValue(BundleKey(userId), out FinalActionEvaluation? b) && b is not null && IsStillCurrent(b.Snapshot, nowUtc) ? b : null;

    private sealed record ScanPass(
        Dictionary<string, RsiScanResult> Scans,
        Dictionary<string, string> Rejections,
        Dictionary<string, RejectedMarketBar> RejectedBars,
        HashSet<string> Unknown);

    /// <summary>Batched scan + one retry, shared by persisted, read-only and preview evaluations.</summary>
    private async Task<ScanPass> ScanSymbolsAsync(IReadOnlyList<string> symbols, string userId, ScannerEvaluationMode mode, CancellationToken ct)
    {
        Task<List<RsiScanResult>> Analyze(IEnumerable<string> batch) => mode switch
        {
            ScannerEvaluationMode.Persist => scanner.AnalyzeSymbolsAsync(batch, OversoldThreshold, OverboughtThreshold, LogicMode, userId, ct),
            ScannerEvaluationMode.Preview => scanner.AnalyzeSymbolsPreviewAsync(batch, OversoldThreshold, OverboughtThreshold, LogicMode, userId, ct),
            _ => scanner.AnalyzeSymbolsReadOnlyAsync(batch, OversoldThreshold, OverboughtThreshold, LogicMode, userId, ct),
        };

        var scans = new Dictionary<string, RsiScanResult>(StringComparer.OrdinalIgnoreCase);
        var rejections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var rejectedBars = new Dictionary<string, RejectedMarketBar>(StringComparer.OrdinalIgnoreCase);
        var unknown = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var batch in symbols.Chunk(BatchSize))
        {
            try
            {
                foreach (var r in await Analyze(batch))
                    scans[r.Symbol] = r;
                foreach (var (rejectedSymbol, why) in scanner.LastRejections) rejections[rejectedSymbol] = why;
                foreach (var (rejectedSymbol, bar) in scanner.LastRejectedBars) rejectedBars[rejectedSymbol] = bar;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "[FinalAction] Scan batch of {Count} symbols failed; those symbols get no decision.", batch.Length);
                foreach (var s in batch) unknown.Add(s);
            }
        }

        // A transient provider hiccup on one thin symbol (e.g. a CSE listing) must not leave it "Awaiting Data" for the whole day:
        // retry once, only for symbols with no scan and no deterministic rejection (inconsistent/insufficient data would just repeat).
        var retry = symbols.Where(s => !scans.ContainsKey(s) && !rejections.ContainsKey(s) && !unknown.Contains(s)).ToArray();
        if (retry.Length > 0)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
                foreach (var r in await Analyze(retry))
                    scans[r.Symbol] = r;
                foreach (var (rejectedSymbol, why) in scanner.LastRejections) rejections[rejectedSymbol] = why;
                foreach (var (rejectedSymbol, bar) in scanner.LastRejectedBars) rejectedBars[rejectedSymbol] = bar;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "[FinalAction] Retry scan of {Count} symbol(s) failed.", retry.Length);
            }
        }

        // A symbol with no scan or no usable price was not evaluated: it must never confirm or invalidate anything.
        foreach (var s in symbols)
            if (!scans.TryGetValue(s, out var sc) || sc.CurrentPrice <= 0m) unknown.Add(s);
        return new ScanPass(scans, rejections, rejectedBars, unknown);
    }

    /// <summary>Evidence must be usable before any decision is kept; otherwise the symbol is "Awaiting Data".</summary>
    private static EvidenceAssessment AssessEvidence(
        RsiScanResult? scan, string symbol, string category, DateTime? quoteUtc, IReadOnlyDictionary<string, string> rejections, DateTime nowUtc)
    {
        var tradingDate = scan is null || scan.TradingDate == default ? (DateOnly?)null : scan.TradingDate;
        return FinalActionStatusResolver.AssessEvidence(
            new EvidenceInput(tradingDate, quoteUtc, scan?.CurrentPrice ?? 0m, EvaluationFailed: scan is null,
                MissingRequired: scan is null ? null : FinalActionStatusResolver.MissingRequired(scan, category),
                Rejection: scan is null && rejections.TryGetValue(symbol, out var rejected) ? rejected : null),
            TradingCalendar.ExchangeFor(symbol, scan?.AnalysisMarket), nowUtc);
    }

    private static List<string> EvidenceCodes(EvidenceAssessment a) =>
        a.Codes.Where(c => c != FinalActionStatusResolver.QuoteTimestampUnavailable).ToList();

    private static string ExchangeBasisOf(string symbol, RsiScanResult? scan) =>
        TradingCalendar.Resolve(symbol, scan?.AnalysisMarket).Basis.ToString();

    /// <summary>The watchlist engine decision for one symbol (null without a scan).</summary>
    private static FinalActionDecision? WatchlistDecision(RsiScanResult? scan, string symbol, string role, IReadOnlyDictionary<string, ValueScreenerResult> valueByTicker)
    {
        if (scan is null) return null;
        valueByTicker.TryGetValue(symbol, out var vs);
        var ctx = vs is null ? null : new WatchlistValueContext((double)vs.Score, vs.ActionTrigger == ActionTrigger.ValueTrapWarning, null);
        return FinalActionEngine.TranslateForWatchlist(ToInput(scan), role, ctx);
    }

    private static FinalActionResultDto AwaitingWatchlistRow(
        string symbol, string source, string role, RsiScanResult? scan, EvidenceAssessment assessment, RejectedMarketBar? rejectedBar,
        DateTime? quoteUtc, DateTime? fetchedUtc) =>
        AwaitingDto(symbol, source, null, role, scan, assessment, null, null, rejectedBar) with
        {
            QuoteTimestampUtc = quoteUtc, QuoteFetchedAtUtc = fetchedUtc, QuoteVerification = quoteUtc is null ? "Unavailable" : "Verified",
            ExchangeBasis = ExchangeBasisOf(symbol, scan),
        };

    private static FinalActionResultDto CurrentWatchlistRow(
        FinalActionDecision decision, string symbol, string source, string role, RsiScanResult scan, EvidenceAssessment assessment,
        DateTime nowUtc, List<string> codes, string? originalAction, FinalActionOwnership ownership, string allocationStatus,
        DateTime? quoteUtc, DateTime? fetchedUtc) =>
        ToDto(decision, symbol, source, null, role, scan) with
        {
            EvaluationState = nameof(EffectiveState.CurrentValid),
            EvidenceHealth = assessment.Health.ToString(),
            LastSuccessfulEvaluationUtc = nowUtc,
            DataAsOfDate = assessment.DataAsOf,
            ExpectedSession = assessment.ExpectedSession,
            ReasonCodes = codes,
            OriginalAction = originalAction,
            Ownership = ownership,
            AllocationStatus = allocationStatus,
            QuoteTimestampUtc = quoteUtc,
            QuoteFetchedAtUtc = fetchedUtc,
            QuoteVerification = quoteUtc is null ? "Unavailable" : "Verified",
            ExchangeBasis = ExchangeBasisOf(symbol, scan),
        };

    public async Task<UniverseFinalActions> EvaluateUniverseAsync(string userId, IReadOnlyCollection<string> symbols, CancellationToken ct = default)
    {
        const string role = "Strategic";
        var list = symbols.Select(s => s.Trim().ToUpperInvariant()).Where(s => s.Length > 0).Distinct().ToList();
        var pass = await ScanSymbolsAsync(list, userId, ScannerEvaluationMode.ReadOnly, ct);
        var valueByTicker = await LoadValueScreenerAsync(ct);
        var summaries = await LoadSummariesAsync(userId, ct);
        var valuation = await CheckValuationAsync(userId, summaries, ct);
        var grandTotal = await GrandTotalAsync(userId, summaries, valuation, ct);
        var stockTotal = summaries.Where(s => !IsClose(s.Item.TransactionType)).Sum(s => MarketValue(s, valuation));
        var positionLimits = await db.SinglePositionLimits.AsNoTracking().ToListAsync(ct);
        var sectorTargets = await db.AllocationSectorTargets.AsNoTracking().ToListAsync(ct);
        var nowUtc = DateTime.UtcNow;
        var notOwned = OwnershipOf([], 0, valuation);

        var rows = new List<FinalActionResultDto>();
        foreach (var symbol in list)
        {
            pass.Scans.TryGetValue(symbol, out var scan);
            var decision = WatchlistDecision(scan, symbol, role, valueByTicker);
            var category = decision is null ? "Advisory" : FinalActionCatalog.Describe(decision.FinalAction).Category;
            var assessment = AssessEvidence(scan, symbol, category, null, pass.Rejections, nowUtc);
            if (decision is null || assessment.Health != EvidenceHealth.Fresh)
            {
                pass.Unknown.Add(symbol);
                pass.RejectedBars.TryGetValue(symbol, out var rejectedBar);
                rows.Add(AwaitingWatchlistRow(symbol, UniverseSource, role, scan, assessment, rejectedBar, null, null));
                continue;
            }
            var codes = EvidenceCodes(assessment);
            string? originalAction = null;
            if (category == "Entry")
            {
                var evidence = BuildNewPositionEvidence(symbol, role, scan!, summaries, grandTotal, stockTotal,
                    positionLimits, sectorTargets, valuation);
                var conditioned = PortfolioEvidenceGate.ConditionEntry(decision, evidence);
                decision = conditioned.Decision;
                originalAction = conditioned.OriginalAction;
                codes = conditioned.ReasonCodes.Concat(codes).ToList();
            }
            rows.Add(CurrentWatchlistRow(decision, symbol, UniverseSource, role, scan!, assessment, nowUtc,
                codes, originalAction, notOwned, valuation.Status, null, null));
        }
        return new UniverseFinalActions(rows,
            pass.Scans.ToDictionary(kv => kv.Key, kv => kv.Value.Copy(), StringComparer.OrdinalIgnoreCase),
            pass.Unknown.Select(s => s.ToUpperInvariant()).ToHashSet(), nowUtc);
    }

    private async Task<List<PortfolioSummaryDto>> LoadSummariesAsync(string userId, CancellationToken ct)
    {
        var portfolioSnap = await db.PortfolioSnapshots.AsNoTracking().SingleOrDefaultAsync(s => s.UserId == userId, ct);
        return Deserialize<List<PortfolioSummaryDto>>(portfolioSnap?.SnapshotJson) ?? [];
    }

    private async Task<FinalActionEvaluation> ComputeCoreAsync(string userId, ScannerEvaluationMode mode, CancellationToken ct)
    {
        var watchlistItems = await db.WatchlistItems.AsNoTracking()
            .Where(w => w.UserId == userId || w.UserId == null).ToListAsync(ct);
        var portfolioSnap = await db.PortfolioSnapshots.AsNoTracking().SingleOrDefaultAsync(s => s.UserId == userId, ct);
        var summaries = Deserialize<List<PortfolioSummaryDto>>(portfolioSnap?.SnapshotJson) ?? [];
        var openHoldings = summaries
            .Where(s => !IsClose(s.Item.TransactionType) && !s.Item.IsManual).ToList();

        var symbols = watchlistItems.Select(w => w.Symbol)
            .Concat(openHoldings.Select(h => h.Item.Symbol))
            .Select(s => s.ToUpperInvariant()).Distinct().ToList();

        var (scans, rejections, rejectedBars, unknown) = await ScanSymbolsAsync(symbols, userId, mode, ct);
        var valueByTicker = await LoadValueScreenerAsync(ct);
        var nowUtc = DateTime.UtcNow;

        // Watchlist quotes come from the already-stored Watchlist snapshot (same source holdings use): no new requests.
        var watchQuoteUtc = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        var watchFetchedUtc = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        var watchSnap = await db.WatchlistSnapshots.AsNoTracking().SingleOrDefaultAsync(s => s.UserId == userId, ct);
        foreach (var w in Deserialize<List<WatchlistSummaryDto>>(watchSnap?.SnapshotJson) ?? [])
        {
            var (sourceUtc, fetchedUtc) = QuoteTimes(w.Quote);
            if (sourceUtc is { } su) watchQuoteUtc[w.Item.Symbol] = su;
            if (fetchedUtc is { } fu) watchFetchedUtc[w.Item.Symbol] = fu;
        }

        // Evidence must be usable before any decision is kept: otherwise the symbol is "Awaiting Data" — a visible,
        // non-actionable status — and is also listed as unknown so lifecycle never confirms, invalidates or expires on it.
        EvidenceAssessment Assess(RsiScanResult? scan, string symbol, string category, DateTime? quoteUtc) =>
            AssessEvidence(scan, symbol, category, quoteUtc, rejections, nowUtc);

        static List<string> Codes(EvidenceAssessment a) => EvidenceCodes(a);

        static string Basis(string symbol, RsiScanResult? scan) => ExchangeBasisOf(symbol, scan);

        var valuation = await CheckValuationAsync(userId, summaries, ct);
        var grandTotal = await GrandTotalAsync(userId, summaries, valuation, ct);
        var positionLimits = await db.SinglePositionLimits.AsNoTracking().ToListAsync(ct);
        var sectorTargets = await db.AllocationSectorTargets.AsNoTracking().ToListAsync(ct);
        var stockTotal = summaries.Where(s => !IsClose(s.Item.TransactionType)).Sum(s => MarketValue(s, valuation));
        var riskByLot = await LoadRiskAsync(userId, summaries, ct);

        // One authoritative portfolio evaluation per open lot (engine + evidence + gate), shared by the Portfolio rows below and
        // by the Watchlist, so an owned ticker's ADD is judged against exactly the decision the Portfolio shows for its lots.
        var lotEval = new Dictionary<int, (RsiScanResult? Scan, PortfolioEvidence? Evidence, GateOutcome? Gate)>();
        foreach (var holding in openHoldings)
        {
            scans.TryGetValue(holding.Item.Symbol, out var lotScan);
            if (lotScan is null) { lotEval[holding.Item.Id] = (null, null, null); continue; }
            var context = BuildContext(holding, lotScan, summaries, grandTotal, valuation);
            var raw = FinalActionEngine.TranslateForPortfolio(ToInput(lotScan), holding.Item.HoldingRole, context);
            var evidence = BuildEvidence(holding, lotScan, openHoldings, summaries, grandTotal, stockTotal, positionLimits, sectorTargets, context, valuation,
                riskByLot.GetValueOrDefault(holding.Item.Id));
            lotEval[holding.Item.Id] = (lotScan, evidence, PortfolioEvidenceGate.Apply(raw, holding.Item.HoldingRole, evidence));
        }

        var watchlist = new List<FinalActionResultDto>();
        foreach (var item in watchlistItems)
        {
            var role = item.Role ?? "Strategic";
            scans.TryGetValue(item.Symbol, out var scan);
            var decision = WatchlistDecision(scan, item.Symbol, role, valueByTicker);
            var category = decision is null ? "Advisory" : FinalActionCatalog.Describe(decision.FinalAction).Category;
            DateTime? wlQuote = watchQuoteUtc.TryGetValue(item.Symbol, out var wq0) ? wq0 : null;
            DateTime? wlFetched = watchFetchedUtc.TryGetValue(item.Symbol, out var wf0) ? wf0 : null;
            var assessment = Assess(scan, item.Symbol, category, wlQuote);
            if (decision is null || assessment.Health != EvidenceHealth.Fresh)
            {
                unknown.Add(item.Symbol);
                rejectedBars.TryGetValue(item.Symbol, out var rejectedBar);
                watchlist.Add(AwaitingWatchlistRow(item.Symbol, SignalSources.Watchlist, role, scan, assessment, rejectedBar, wlQuote, wlFetched));
                continue;
            }
            var owned = openHoldings.Where(h => string.Equals(h.Item.Symbol, item.Symbol, StringComparison.OrdinalIgnoreCase)).ToList();
            var wlCodes = Codes(assessment);
            string? wlOriginal = null;
            if (owned.Count > 0 && category == "Entry")
            {
                // Entry readiness for a ticker that is already held is an ADD. Exposure is every open lot of the symbol;
                // limits come from the lots' role, and lots with different roles make the ADD policy ambiguous.
                var lot = owned[0];
                var lotCtx = BuildContext(lot, scan!, summaries, grandTotal, valuation);
                var lotEvidence = BuildEvidence(lot, scan!, openHoldings, summaries, grandTotal, stockTotal, positionLimits, sectorTargets, lotCtx, valuation);
                var ownedRoles = owned.Select(RoleOfLot).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(r => r, StringComparer.OrdinalIgnoreCase).ToList();
                if (ownedRoles.Count > 1)
                    lotEvidence = lotEvidence with
                    {
                        RolePolicyAmbiguous = true,
                        RolePolicyDetail = $"open lots hold different roles: {string.Join(", ", ownedRoles)}",
                    };
                var conditioned = PortfolioEvidenceGate.ConditionOwnedEntry(decision, lotEvidence);
                decision = conditioned.Decision;
                wlOriginal = conditioned.OriginalAction;
                wlCodes = conditioned.ReasonCodes.Concat(["ALREADY_OWNED"]).Concat(wlCodes).ToList();

                // The Portfolio's own decision for the held lots has the last word on an ADD: if it does not permit adding
                // (No Add / caution / risk review / exit), the Watchlist entry must not read as a buy.
                if (FinalActionCatalog.Describe(decision.FinalAction).Category == "Entry")
                {
                    var blocking = owned.Select(l => lotEval[l.Item.Id].Gate?.Decision.FinalAction)
                        .FirstOrDefault(a => a is not null && PortfolioEvidenceGate.BlocksAdd(a));
                    if (blocking is not null)
                    {
                        var blocked = PortfolioEvidenceGate.BlockOwnedEntryByPortfolioAction(decision, blocking);
                        decision = blocked.Decision;
                        wlOriginal = blocked.OriginalAction;
                        wlCodes = blocked.ReasonCodes.Concat(wlCodes).ToList();
                    }
                }
            }
            else if (category == "Entry")
            {
                var evidence = BuildNewPositionEvidence(item.Symbol, role, scan!, summaries, grandTotal, stockTotal,
                    positionLimits, sectorTargets, valuation);
                var conditioned = PortfolioEvidenceGate.ConditionEntry(decision, evidence);
                decision = conditioned.Decision;
                wlOriginal = conditioned.OriginalAction;
                wlCodes = conditioned.ReasonCodes.Concat(wlCodes).ToList();
            }
            else if (owned.Count > 0) wlCodes = ["ALREADY_OWNED", .. wlCodes];
            watchlist.Add(CurrentWatchlistRow(decision, item.Symbol, SignalSources.Watchlist, role, scan!, assessment, nowUtc,
                wlCodes, wlOriginal, OwnershipOf(owned, grandTotal, valuation), valuation.Status, wlQuote, wlFetched));
        }

        var portfolio = new List<FinalActionResultDto>();
        foreach (var holding in openHoldings)
        {
            var (quoteUtc, quoteFetchedUtc) = QuoteTimes(holding.Quote);
            var (scan, evidence, gate) = lotEval[holding.Item.Id];
            var category = gate is null ? "Advisory" : FinalActionCatalog.Describe(gate.Decision.FinalAction).Category;
            var assessment = Assess(scan, holding.Item.Symbol, category, quoteUtc);
            if (gate is null || evidence is null || assessment.Health != EvidenceHealth.Fresh)
            {
                unknown.Add(holding.Item.Symbol);
                rejectedBars.TryGetValue(holding.Item.Symbol, out var rejectedBar);
                portfolio.Add(AwaitingDto(holding.Item.Symbol, SignalSources.Portfolio, holding.Item.Id, holding.Item.HoldingRole, scan, assessment,
                    holding.Item.FinalAction, holding.Item.FinalActionUpdatedAt, rejectedBar) with
                {
                    QuoteTimestampUtc = quoteUtc, QuoteFetchedAtUtc = quoteFetchedUtc, QuoteVerification = quoteUtc is null ? "Unavailable" : "Verified",
                    ExchangeBasis = Basis(holding.Item.Symbol, scan),
                });
                continue;
            }
            var (explainCodes, rationale) = FinalActionExplainer.ExplainPortfolio(gate.Decision, gate, evidence, holding.Item.HoldingRole);
            var finalDecision = gate.Decision.FinalActionReason is null && rationale is not null
                ? gate.Decision with { FinalActionReason = rationale } : gate.Decision;
            portfolio.Add(ToDto(finalDecision, holding.Item.Symbol, SignalSources.Portfolio, holding.Item.Id, holding.Item.HoldingRole, scan!)
                with
                {
                    ReasonCodes = explainCodes.Concat(Codes(assessment)).ToList(),
                    EvidenceAsOfUtc = evidence.EvidenceAsOfUtc,
                    EntryDate = evidence.EntryDate,
                    OriginalAction = gate.OriginalAction,
                    Risk = evidence.Risk,
                    Ownership = OwnershipOf(openHoldings.Where(h => string.Equals(h.Item.Symbol, holding.Item.Symbol, StringComparison.OrdinalIgnoreCase)).ToList(), grandTotal, valuation),
                    AllocationStatus = valuation.Status,
                    EvaluationState = nameof(EffectiveState.CurrentValid),
                    EvidenceHealth = assessment.Health.ToString(),
                    LastSuccessfulEvaluationUtc = nowUtc,
                    DataAsOfDate = assessment.DataAsOf,
                    ExpectedSession = assessment.ExpectedSession,
                    QuoteTimestampUtc = quoteUtc,
                    QuoteFetchedAtUtc = quoteFetchedUtc,
                    QuoteVerification = quoteUtc is null ? "Unavailable" : "Verified",
                    ExchangeBasis = Basis(holding.Item.Symbol, scan),
                });
        }

        var meta = new FinalActionEvaluationMeta(
            unknown.Select(s => s.ToUpperInvariant()).ToHashSet(), portfolioSnap?.UpdatedAt, portfolioSnap is not null);
        var snapshot = new FinalActionSnapshotDto(watchlist, portfolio, nowUtc, meta);
        var basis = new FinalActionPortfolioBasis(summaries, openHoldings, grandTotal, stockTotal, valuation, positionLimits, sectorTargets,
            portfolioSnap?.UpdatedAt);
        var bundle = new FinalActionEvaluation(Guid.NewGuid(), snapshot,
            scans.ToDictionary(kv => kv.Key, kv => kv.Value.Copy(), StringComparer.OrdinalIgnoreCase), basis);
        if (mode == ScannerEvaluationMode.Persist)
        {
            var ttl = HasTransientEvidenceProblem(snapshot) ? TransientCacheTtl : CacheTtl;
            cache.Set(SnapshotKey(userId), snapshot, ttl);
            cache.Set(BundleKey(userId), bundle, ttl);
        }
        return bundle;
    }

    /// <summary>
    /// A symbol whose required evidence is missing, stale, invalid or failed. It is never actionable, never a Hold, and
    /// carries the last published action separately as historical context.
    /// </summary>
    private static FinalActionResultDto AwaitingDto(
        string symbol, string source, int? itemId, string? role, RsiScanResult? scan, EvidenceAssessment a,
        string? lastValidAction, DateTime? lastEvaluatedUtc, RejectedMarketBar? rejectedBar)
    {
        const string label = "Awaiting Data";
        var def = FinalActionCatalog.Describe(label);
        var reason = DescribeAwaiting(a, lastValidAction, lastEvaluatedUtc);
        return new FinalActionResultDto(
            symbol, source, itemId, role, label, def.Id, def.Category, def.Direction, "hold", "INFORMATIONAL",
            "ma-standby", "", "", "", "", reason, reason, false,
            0m, scan is null || string.IsNullOrEmpty(scan.AnalysisTicker) ? rejectedBar?.AnalysisTicker ?? symbol : scan.AnalysisTicker, null,
            IsActionable: false, ReasonCodes: a.Codes, EvidenceAsOfUtc: scan?.ScannedAt,
            EvaluationState: nameof(EffectiveState.AwaitingData), EvidenceHealth: a.Health.ToString(),
            LastValidAction: lastValidAction, LastSuccessfulEvaluationUtc: lastEvaluatedUtc,
            DataAsOfDate: a.DataAsOf ?? rejectedBar?.TradingDate, ExpectedSession: a.ExpectedSession, RejectedBar: rejectedBar);
    }

    private static string DescribeAwaiting(EvidenceAssessment a, string? lastValidAction, DateTime? lastEvaluatedUtc)
    {
        var why = a.Health switch
        {
            EvidenceHealth.Failed => "the market-data refresh for this symbol failed (provider or network error)",
            EvidenceHealth.Invalid when a.Codes.Contains(FinalActionStatusResolver.AwaitingInconsistentData) =>
                "the market data for this symbol is internally inconsistent (a session's price range does not contain its own price) and was not used",
            EvidenceHealth.Invalid => "the price is missing or invalid",
            EvidenceHealth.Missing when a.Codes.Contains(FinalActionStatusResolver.AwaitingInsufficientHistory) =>
                "the provider returned too little complete price history to evaluate this symbol",
            EvidenceHealth.Missing => "required evidence is unavailable (" + string.Join(", ", a.Codes) + ")",
            EvidenceHealth.Stale => a.DataAsOf is { } d
                ? $"the latest data is from {d:yyyy-MM-dd}, older than the last completed session ({a.ExpectedSession:yyyy-MM-dd})"
                : "the evidence is older than the last completed session",
            _ => "evidence is unavailable",
        };
        var prior = string.IsNullOrEmpty(lastValidAction) ? ""
            : $" Last valid action: {lastValidAction}" + (lastEvaluatedUtc is { } t ? $" (evaluated {t:yyyy-MM-dd HH:mm} UTC)." : ".");
        return $"Awaiting Data: {why}. No decision is made until a successful evaluation.{prior}";
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    public async Task<int> PublishPortfolioActionsAsync(string userId, FinalActionSnapshotDto snapshot, CancellationToken ct = default)
    {
        // Only symbols evaluated on fresh evidence are published, each stamped with its own evaluation time and session.
        // Awaiting Data symbols keep their previous published action and timestamp untouched.
        var items = snapshot.Portfolio
            .Where(p => p.ItemId is not null && p.EvaluationState != nameof(EffectiveState.AwaitingData))
            .Select(p => new FinalActionSyncItem(p.ItemId!.Value, p.FinalAction, p.Severity, p.Priority,
                p.LastSuccessfulEvaluationUtc ?? snapshot.ComputedAtUtc, p.ExpectedSession?.ToString("yyyy-MM-dd")))
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
        if (d.FinalAction == PortfolioEvidenceGate.ReviewStopBreach) priority = "REQUIRED";
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
        return dto with { IsActionable = DecisionEligibility.IsActionable(dto), EvidenceAsOfUtc = scan.ScannedAt,
            IntradayRangeReconstructed = scan.IntradayRangeReconstructed };
    }

    internal static PortfolioEvidence BuildEvidence(
        PortfolioSummaryDto holding, RsiScanResult scan, IReadOnlyList<PortfolioSummaryDto> openHoldings,
        IReadOnlyList<PortfolioSummaryDto> all, double grandTotal, decimal stockTotal,
        IReadOnlyList<SinglePositionLimit> limits, IReadOnlyList<AllocationSectorTarget> sectorTargets, PortfolioItemContext context,
        ValuationCheck? valuation = null, StopRiskEvidence? risk = null)
    {
        var item = holding.Item;
        var lots = openHoldings.Where(h => string.Equals(h.Item.Symbol, item.Symbol, StringComparison.OrdinalIgnoreCase)).ToList();
        var openDates = lots.Where(l => l.Item.OpenDate is not null).Select(l => DateOnly.FromDateTime(l.Item.OpenDate!.Value)).ToList();
        DateOnly? entry = openDates.Count > 0 ? openDates.Max() : null;

        var evidenceUtc = scan.ScannedAt.Kind == DateTimeKind.Local ? scan.ScannedAt.ToUniversalTime()
            : DateTime.SpecifyKind(scan.ScannedAt, DateTimeKind.Utc);
        var evidenceDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(evidenceUtc, EasternZone()));

        // Concentration is the symbol's TOTAL exposure (every lot, every account). The limit is the target lot's own role limit.
        var reliable = valuation?.Reliable ?? true;
        double? positionSize = reliable && grandTotal > 0 ? Dbl(lots.Sum(l => MarketValue(l, valuation))) / grandTotal * 100 : null;
        string RoleOf(PortfolioSummaryDto s) => string.IsNullOrWhiteSpace(s.Item.HoldingRole) ? "Strategic" : s.Item.HoldingRole!.Trim();
        var targetRole = RoleOf(holding);
        SinglePositionLimit? LimitFor(string role) => limits.FirstOrDefault(l => string.Equals(l.Role, role, StringComparison.OrdinalIgnoreCase));
        var limit = LimitFor(targetRole);
        var lotRoles = lots.Select(RoleOf).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var ambiguous = limit is null && lotRoles.Count > 1 && lotRoles.Any(r => LimitFor(r) is not null);
        var ambiguityDetail = ambiguous
            ? string.Join(", ", lotRoles.Select(r => LimitFor(r) is { } l ? $"{r} limit {l.TargetPct:0.##}%" : $"{r} no limit configured"))
            : null;

        // The symbol's sector is resolved once (first lot that has one, else the scan), so a lot with a blank sector
        // still counts toward the sector of its own symbol.
        var sector = SymbolSector(lots, scan.Sector);
        var (sectorPct, target, sectorOver) = SectorExposure(item.Symbol, sector, all, stockTotal, sectorTargets, reliable, valuation);

        return new PortfolioEvidence(
            context.UnrealizedGainPct, entry, evidenceDate, evidenceUtc, positionSize,
            limit is null ? null : Dbl(limit.TargetPct), sectorOver,
            scan.PriceStructure?.HasHardStructuralNegative == true, item.DecisionSource,
            ambiguous, ambiguityDetail, sectorPct, target is null ? null : Dbl(target.TargetPct), !string.IsNullOrEmpty(sector), risk, valuation)
        {
            AllocationDenominatorAvailable = grandTotal > 0,
        };
    }

    private static PortfolioEvidence BuildNewPositionEvidence(
        string symbol, string role, RsiScanResult scan, IReadOnlyList<PortfolioSummaryDto> summaries,
        double grandTotal, decimal stockTotal, IReadOnlyList<SinglePositionLimit> limits,
        IReadOnlyList<AllocationSectorTarget> sectorTargets, ValuationCheck valuation)
    {
        var sector = scan.Sector ?? "";
        var (sectorPct, target, sectorOver) = SectorExposure(symbol, sector, summaries, stockTotal, sectorTargets, valuation.Reliable, valuation);
        var limit = limits.FirstOrDefault(l => string.Equals(l.Role, role, StringComparison.OrdinalIgnoreCase));
        var positionSize = valuation.Reliable && grandTotal > 0 ? 0d : (double?)null;

        return new PortfolioEvidence(
            null, null, scan.TradingDate, scan.ScannedAt, positionSize,
            limit is null ? null : Dbl(limit.TargetPct), sectorOver,
            scan.PriceStructure?.HasHardStructuralNegative == true, "Watchlist",
            SectorPct: sectorPct, SectorTargetPct: target is null ? null : Dbl(target.TargetPct),
            SectorKnown: !string.IsNullOrWhiteSpace(sector), Valuation: valuation)
        {
            AllocationDenominatorAvailable = grandTotal > 0,
        };
    }

    private static string SymbolSector(IEnumerable<PortfolioSummaryDto> lots, string? fallback) =>
        lots.Select(l => l.Item.Sector).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? fallback ?? "";

    /// <summary>Sector weight of open stock (stockTotal denominator) vs its target; over = actual − target &gt; 2 pp (the ADD gate).</summary>
    private static (double? Pct, AllocationSectorTarget? Target, bool Over) SectorExposure(
        string symbol, string sector, IReadOnlyList<PortfolioSummaryDto> all, decimal stockTotal,
        IReadOnlyList<AllocationSectorTarget> sectorTargets, bool reliable, ValuationCheck? valuation = null)
    {
        var target = string.IsNullOrEmpty(sector) ? null
            : sectorTargets.FirstOrDefault(t => string.Equals(t.Sector, sector, StringComparison.OrdinalIgnoreCase));
        if (!reliable || target is null || stockTotal <= 0) return (null, target, false);
        var actual = all.Where(s => !IsClose(s.Item.TransactionType)
                && (string.Equals(s.Item.Sector ?? "", sector, StringComparison.OrdinalIgnoreCase)
                    || (string.IsNullOrWhiteSpace(s.Item.Sector)
                        && string.Equals(s.Item.Symbol, symbol, StringComparison.OrdinalIgnoreCase)))).Sum(s => MarketValue(s, valuation)) / stockTotal * 100m;
        return (Dbl(actual), target, actual - target.TargetPct > 2m);
    }

    /// <summary>
    /// PM-029 Portfolio Fit input for any symbol, owned or not, from the exact basis and arithmetic of a Final Action evaluation:
    /// whole-symbol exposure over GrandTotal, the lot role's limit (or <paramref name="defaultRole"/> when not owned), mixed-role
    /// ambiguity, and sector weight vs target. Never a new valuation formula.
    /// </summary>
    internal static PortfolioExposure ExposureFor(string symbol, string defaultRole, string? fallbackSector, FinalActionPortfolioBasis basis)
    {
        var reliable = basis.Valuation.Reliable;
        var lots = basis.OpenHoldings.Where(h => string.Equals(h.Item.Symbol, symbol, StringComparison.OrdinalIgnoreCase)).ToList();
        var owned = lots.Count > 0;
        double? positionSize = !owned ? 0d : reliable && basis.GrandTotal > 0 ? Dbl(lots.Sum(l => MarketValue(l, basis.Valuation))) / basis.GrandTotal * 100 : null;
        SinglePositionLimit? LimitFor(string role) =>
            basis.PositionLimits.FirstOrDefault(l => string.Equals(l.Role, role, StringComparison.OrdinalIgnoreCase));
        var roles = owned ? lots.Select(RoleOfLot).Distinct(StringComparer.OrdinalIgnoreCase).ToList() : [defaultRole];
        var limit = LimitFor(roles[0]);
        var ambiguous = roles.Count > 1;
        var sector = SymbolSector(lots, fallbackSector);
        var (sectorPct, target, _) = SectorExposure(symbol, sector, basis.Summaries, basis.StockTotal, basis.SectorTargets, reliable, basis.Valuation);
        return new PortfolioExposure(owned, reliable, positionSize, ambiguous ? null : limit is null ? null : Dbl(limit.TargetPct), ambiguous,
            !string.IsNullOrEmpty(sector), sector, sectorPct, target is null ? null : Dbl(target.TargetPct));
    }

    private static string RoleOfLot(PortfolioSummaryDto s) => string.IsNullOrWhiteSpace(s.Item.HoldingRole) ? "Strategic" : s.Item.HoldingRole!.Trim();

    /// <summary>
    /// Provider price time (<see cref="StockQuote.SourcePriceTimestamp"/>) and the app's fetch time, kept apart. Only the
    /// provider time can prove which session a price belongs to; a fetch time proves nothing about the price.
    /// </summary>
    internal static (DateTime? SourceUtc, DateTime? FetchedUtc) QuoteTimes(StockQuote? q) => (
        q?.SourcePriceTimestamp is > 0 ? DateTimeOffset.FromUnixTimeSeconds(q.SourcePriceTimestamp.Value).UtcDateTime : null,
        q is { Timestamp: > 0 } ? DateTimeOffset.FromUnixTimeSeconds(q.Timestamp).UtcDateTime : null);

    private static decimal MarketValue(PortfolioSummaryDto s) => s.Item.IsManual
        ? s.Item.ManualMarketValue ?? s.Item.AverageCostBasis * s.Item.Shares
        : (s.Quote?.CurrentPrice ?? s.Item.AverageCostBasis) * s.Item.Shares;

    /// <summary>Allocation value in the portfolio's CAD-equivalent basis: a USD lot is converted with the evaluation's single USD/CAD rate.</summary>
    private static decimal MarketValue(PortfolioSummaryDto s, ValuationCheck? valuation) =>
        MarketValue(s) * (valuation is not null && IsUsd(s) ? valuation.UsdCad : 1m);

    private static bool IsUsd(PortfolioSummaryDto s) =>
        PortfolioManager.Api.Services.Risk.TradingCurrency.Resolve(s.Item.Symbol, s.Quote?.Currency).Code == "USD";

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

    internal static PortfolioItemContext BuildContext(
        PortfolioSummaryDto holding, RsiScanResult scan, IReadOnlyList<PortfolioSummaryDto> all, double grandTotal, ValuationCheck? valuation = null)
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
        if (marketValue is not null && valuation is not null && IsUsd(holding)) marketValue *= Dbl(valuation.UsdCad);
        double? positionSize = (valuation?.Reliable ?? true) && grandTotal > 0 && marketValue is not null ? marketValue.Value / grandTotal * 100 : null;

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

    /// <summary>
    /// CAD-equivalent total: stocks/ETFs at market + cash + open-option market value, the same screen formulas the Portfolio page
    /// uses (option value = Market Price × contracts × 100). Each USD component is converted with the single evaluation rate.
    /// </summary>
    private async Task<double> GrandTotalAsync(string userId, IReadOnlyList<PortfolioSummaryDto> summaries, ValuationCheck valuation, CancellationToken ct)
    {
        var stocks = summaries.Where(s => !IsClose(s.Item.TransactionType)).Sum(s => MarketValue(s, valuation));
        var cashRows = await db.CashItems.AsNoTracking().Where(c => c.UserId == userId || c.UserId == null)
            .Select(c => new { c.Amount, c.AccountType, c.Description }).ToListAsync(ct);
        var cash = cashRows.Sum(c => CashCurrency(c.AccountType, c.Description) == "USD" ? c.Amount * valuation.UsdCad : c.Amount);
        var optionRows = await db.OptionItems.AsNoTracking()
            .Where(o => (o.UserId == userId || o.UserId == null) && o.TransactionType != "CLOSE")
            .Select(o => new { o.UnderlyingTicker, o.MarketPrice, o.NumberOfContracts }).ToListAsync(ct);
        var options = optionRows.Sum(o => o.MarketPrice * o.NumberOfContracts * 100
            * (PortfolioManager.Api.Services.Risk.TradingCurrency.Resolve(o.UnderlyingTicker, null).Code == "USD" ? valuation.UsdCad : 1m));
        return Dbl(stocks + cash + options);
    }

    private Task<Dictionary<string, ValueScreenerResult>> LoadValueScreenerAsync(CancellationToken ct) => LoadValueScreenerAsync(db, ct);

    /// <summary>Latest Value Screener result per symbol with the Final Action precedence (Universe &lt; Watchlist &lt; Portfolio).</summary>
    internal static async Task<Dictionary<string, ValueScreenerResult>> LoadValueScreenerAsync(AppDbContext db, CancellationToken ct)
    {
        var map = new Dictionary<string, ValueScreenerResult>(StringComparer.OrdinalIgnoreCase);
        // Same precedence as the Watchlist page: watchlist results first, portfolio results override. PM-029 Universe results
        // (symbols neither watched nor held) have the lowest precedence and never replace a Watchlist/Portfolio evaluation.
        foreach (var origin in new[] { "Universe", "Watchlist", "Portfolio" })
        {
            var snap = await db.ValueScreenerSnapshots.AsNoTracking()
                .Where(s => s.Origin == origin).OrderByDescending(s => s.RunAt).FirstOrDefaultAsync(ct);
            foreach (var r in Deserialize<List<ValueScreenerResult>>(snap?.ResultsJson) ?? [])
                map[r.Symbol] = r;
        }
        return map;
    }

    /// <summary>
    /// Decides whether allocation percentages can be trusted. The currency of each component is the provider's quote currency
    /// (ticker suffix as a labelled fallback); cash follows the owner-approved ledger convention (CAD unless the account or
    /// description is marked USD). A single currency needs no conversion. A mix is reliable only when one verified Bank of
    /// Canada USD/CAD rate is supplied; the rate is never guessed or defaulted to 1.
    /// </summary>
    internal static ValuationCheck ClassifyValuation(
        IEnumerable<(string Symbol, string? QuoteCurrency)> stocks,
        IEnumerable<(string? Account, string? Description)> cash,
        IEnumerable<string> optionUnderlyings,
        PortfolioManager.Api.Services.Valuation.UsdCadRate? fx = null)
    {
        var currencies = new SortedSet<string>(StringComparer.Ordinal);
        var stockCodes = stocks.Select(s => PortfolioManager.Api.Services.Risk.TradingCurrency.Resolve(s.Symbol, s.QuoteCurrency).Code).ToList();
        var optionCodes = optionUnderlyings.Select(u => PortfolioManager.Api.Services.Risk.TradingCurrency.Resolve(u, null).Code).ToList();
        var cashCodes = cash.Select(c => CashCurrency(c.Account, c.Description)).ToList();
        foreach (var code in stockCodes.Concat(optionCodes).Concat(cashCodes)) currencies.Add(code);

        if (currencies.Count <= 1)
            return new ValuationCheck(ValuationCheck.SingleCurrency, currencies.Count == 0 ? "no valued components" : $"all components in {currencies.Single()}");
        if (!currencies.SetEquals(["CAD", "USD"]))
            return new ValuationCheck(ValuationCheck.MixedCurrency,
                $"the portfolio contains unsupported currencies ({string.Join(", ", currencies)}); only CAD and USD can be converted");
        if (fx is { IsAvailable: true })
            return new ValuationCheck(ValuationCheck.CadConverted,
                $"USD components converted to CAD: {fx.Detail}", fx.Rate!.Value);
        static string Split(string label, List<string> codes) => codes.Count == 0 ? "" :
            $"{label} {string.Join("/", codes.GroupBy(c => c).OrderBy(g => g.Key).Select(g => $"{g.Count()} {g.Key}"))}";
        var parts = new[] { Split("stocks", stockCodes), Split("cash rows", cashCodes), Split("open options", optionCodes) }.Where(p => p.Length > 0);
        return new ValuationCheck(ValuationCheck.MixedCurrency,
            $"the portfolio total mixes {string.Join(" and ", currencies)} ({string.Join("; ", parts)}) and no verified USD/CAD rate is available: "
            + (fx?.Detail ?? "no FX source is configured"));
    }

    private static string CashCurrency(string? account, string? description) =>
        UsdMarker.IsMatch(account ?? "") || UsdMarker.IsMatch(description ?? "") ? "USD" : "CAD";

    private static readonly System.Text.RegularExpressions.Regex UsdMarker = new(
        @"(?<![A-Za-z])(USD|US\$|USDOLLAR)(?![A-Za-z])|_USD\b",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

    private async Task<ValuationCheck> CheckValuationAsync(string userId, IReadOnlyList<PortfolioSummaryDto> summaries, CancellationToken ct)
    {
        var stocks = summaries.Where(s => !IsClose(s.Item.TransactionType))
            .Select(s => (s.Item.Symbol, s.Quote?.Currency)).ToList();
        var cash = (await db.CashItems.AsNoTracking().Where(c => c.UserId == userId || c.UserId == null)
            .Select(c => new { c.AccountType, c.Description }).ToListAsync(ct)).Select(c => (c.AccountType, (string?)c.Description)).ToList();
        var options = await db.OptionItems.AsNoTracking()
            .Where(o => (o.UserId == userId || o.UserId == null) && o.TransactionType != "CLOSE")
            .Select(o => o.UnderlyingTicker).ToListAsync(ct);
        var plain = ClassifyValuation(stocks, cash, options);
        if (plain.Status != ValuationCheck.MixedCurrency || usdCadRates is null) return plain;
        // Only a genuine CAD/USD mix needs the rate, so a CAD-only portfolio never calls the FX provider.
        return ClassifyValuation(stocks, cash, options, await usdCadRates.GetAsync(DateTimeOffset.UtcNow, ct));
    }

    /// <summary>One batched read of open Swing risk for the whole pass. A failed read leaves risk unknown; it never implies Clear.</summary>
    private async Task<Dictionary<int, StopRiskEvidence>> LoadRiskAsync(string userId, IReadOnlyList<PortfolioSummaryDto> summaries, CancellationToken ct)
    {
        var map = new Dictionary<int, StopRiskEvidence>();
        if (riskReader is null) return map;
        try
        {
            var quotes = new Dictionary<string, StockQuote>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in summaries) if (s.Quote is not null) quotes[s.Item.Symbol] = s.Quote;
            var snap = await riskReader.ReadAsync(new PortfolioManager.Api.Services.Risk.RiskScope(userId, false), quotes, ct);
            foreach (var p in snap.Positions) 
            {
                var evidence = ToRiskEvidence(p);
                foreach (var lot in p.Position.OpenLots.Where(l => l.IsSwing)) map[lot.Id] = evidence;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "[FinalAction] Stop-risk read failed; stop evidence is unavailable for this pass.");
        }
        return map;
    }

    internal static StopRiskEvidence ToRiskEvidence(PortfolioManager.Api.Services.Risk.OpenRiskPosition p)
    {
        var usable = p.Evidence.IsUsable;
        var price = p.Evidence.Price;
        decimal? brokerStop = p.ActiveStops.Count > 0 ? p.ActiveStops.Max(s => s.TriggerPrice) : null;
        var plannedBreached = p.Evaluation.PlannedStopState == "Breached";
        var brokerBreached = usable && brokerStop is not null && price is not null && price <= brokerStop;
        return new StopRiskEvidence(
            p.Evaluation.PlannedStopState, p.PlanCurrent ? p.Plan?.PlannedStopPrice : null, brokerStop, plannedBreached, brokerBreached,
            price, p.Evidence.AsOfUtc, p.Evidence.Basis, p.Evaluation.CoverageState, p.Evaluation.Protection,
            p.ActiveStops.Select(s => s.BrokerVerificationStatus).ToList(), p.PlanCurrent, p.Position.Account, p.Position.EpisodeOpenDate);
    }

    private static FinalActionOwnership OwnershipOf(IReadOnlyList<PortfolioSummaryDto> lots, double grandTotal, ValuationCheck valuation)
    {
        if (lots.Count == 0) return new FinalActionOwnership(false, 0, [], [], false, null);
        string Role(PortfolioSummaryDto s) => string.IsNullOrWhiteSpace(s.Item.HoldingRole) ? "Strategic" : s.Item.HoldingRole!.Trim();
        var roles = lots.Select(Role).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(r => r).ToList();
        var accounts = lots.Select(l => l.Item.AccountType ?? "").Where(a => a.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(a => a).ToList();
        double? exposure = valuation.Reliable && grandTotal > 0 ? Dbl(lots.Sum(l => MarketValue(l, valuation))) / grandTotal * 100 : null;
        return new FinalActionOwnership(true, lots.Count, accounts, roles, roles.Count > 1, exposure);
    }

    private static bool IsClose(string? type) => string.Equals(type, "CLOSE", StringComparison.OrdinalIgnoreCase);

    private static T? Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        try { return JsonSerializer.Deserialize<T>(json, JsonOpts); }
        catch (JsonException) { return default; }
    }
}
