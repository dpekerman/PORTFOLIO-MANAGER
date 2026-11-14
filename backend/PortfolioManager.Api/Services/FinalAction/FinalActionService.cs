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
}

public sealed class FinalActionService(
    AppDbContext db,
    IRsiScannerService scanner,
    IPortfolioSnapshotService portfolioSnapshot,
    IMemoryCache cache,
    ILogger<FinalActionService> logger,
    PortfolioManager.Api.Services.Risk.IOpenRiskReader? riskReader = null) : IFinalActionService
{
    // Same parameters the Angular watchlist analysis uses (analyzeSymbols(batch, 30, 75, 'Enhanced')).
    private const decimal OversoldThreshold = 30m;
    private const decimal OverboughtThreshold = 75m;
    private const string LogicMode = "Enhanced";
    private const int BatchSize = 100;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);
    /// <summary>Failed, inconsistent or incomplete market data can recover within minutes: such a snapshot must not be reused for the full TTL.</summary>
    private static readonly TimeSpan TransientCacheTtl = TimeSpan.FromMinutes(1);
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

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
        var key = $"final-actions:{userId}";
        if (!fresh && cache.TryGetValue(key, out FinalActionSnapshotDto? hit) && hit is not null && IsStillCurrent(hit, DateTime.UtcNow)) return hit;

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
        var rejections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var rejectedBars = new Dictionary<string, RejectedMarketBar>(StringComparer.OrdinalIgnoreCase);
        var unknown = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var batch in symbols.Chunk(BatchSize))
        {
            try
            {
                foreach (var r in await scanner.AnalyzeSymbolsAsync(batch, OversoldThreshold, OverboughtThreshold, LogicMode, userId, ct))
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
                foreach (var r in await scanner.AnalyzeSymbolsAsync(retry, OversoldThreshold, OverboughtThreshold, LogicMode, userId, ct))
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
        EvidenceAssessment Assess(RsiScanResult? scan, string symbol, string category, DateTime? quoteUtc)
        {
            var tradingDate = scan is null || scan.TradingDate == default ? (DateOnly?)null : scan.TradingDate;
            return FinalActionStatusResolver.AssessEvidence(
                new EvidenceInput(tradingDate, quoteUtc, scan?.CurrentPrice ?? 0m, EvaluationFailed: scan is null,
                    MissingRequired: scan is null ? null : FinalActionStatusResolver.MissingRequired(scan, category),
                    Rejection: scan is null && rejections.TryGetValue(symbol, out var rejected) ? rejected : null),
                TradingCalendar.ExchangeFor(symbol, scan?.AnalysisMarket), nowUtc);
        }

        static List<string> Codes(EvidenceAssessment a) =>
            a.Codes.Where(c => c != FinalActionStatusResolver.QuoteTimestampUnavailable).ToList();

        static string Basis(string symbol, RsiScanResult? scan) =>
            TradingCalendar.Resolve(symbol, scan?.AnalysisMarket).Basis.ToString();

        var grandTotal = await GrandTotalAsync(userId, summaries, ct);
        var positionLimits = await db.SinglePositionLimits.AsNoTracking().ToListAsync(ct);
        var sectorTargets = await db.AllocationSectorTargets.AsNoTracking().ToListAsync(ct);
        var stockTotal = summaries.Where(s => !IsClose(s.Item.TransactionType)).Sum(MarketValue);
        var valuation = await CheckValuationAsync(userId, summaries, ct);
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
            FinalActionDecision? decision = null;
            if (scan is not null)
            {
                valueByTicker.TryGetValue(item.Symbol, out var vs);
                var ctx = vs is null ? null : new WatchlistValueContext((double)vs.Score, vs.ActionTrigger == ActionTrigger.ValueTrapWarning, null);
                decision = FinalActionEngine.TranslateForWatchlist(ToInput(scan), role, ctx);
            }
            var category = decision is null ? "Advisory" : FinalActionCatalog.Describe(decision.FinalAction).Category;
            DateTime? wlQuote = watchQuoteUtc.TryGetValue(item.Symbol, out var wq0) ? wq0 : null;
            DateTime? wlFetched = watchFetchedUtc.TryGetValue(item.Symbol, out var wf0) ? wf0 : null;
            var assessment = Assess(scan, item.Symbol, category, wlQuote);
            if (decision is null || assessment.Health != EvidenceHealth.Fresh)
            {
                unknown.Add(item.Symbol);
                rejectedBars.TryGetValue(item.Symbol, out var rejectedBar);
                watchlist.Add(AwaitingDto(item.Symbol, SignalSources.Watchlist, null, role, scan, assessment, null, null, rejectedBar) with
                {
                    QuoteTimestampUtc = wlQuote, QuoteFetchedAtUtc = wlFetched, QuoteVerification = wlQuote is null ? "Unavailable" : "Verified",
                    ExchangeBasis = Basis(item.Symbol, scan),
                });
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
            else if (owned.Count > 0) wlCodes = ["ALREADY_OWNED", .. wlCodes];
            watchlist.Add(ToDto(decision, item.Symbol, SignalSources.Watchlist, null, role, scan!) with
            {
                EvaluationState = nameof(EffectiveState.CurrentValid),
                EvidenceHealth = assessment.Health.ToString(),
                LastSuccessfulEvaluationUtc = nowUtc,
                DataAsOfDate = assessment.DataAsOf,
                ExpectedSession = assessment.ExpectedSession,
                ReasonCodes = wlCodes,
                OriginalAction = wlOriginal,
                Ownership = OwnershipOf(owned, grandTotal, valuation),
                AllocationStatus = valuation.Status,
                QuoteTimestampUtc = wlQuote,
                QuoteFetchedAtUtc = wlFetched,
                QuoteVerification = wlQuote is null ? "Unavailable" : "Verified",
                ExchangeBasis = Basis(item.Symbol, scan),
            });
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
        cache.Set(key, snapshot, HasTransientEvidenceProblem(snapshot) ? TransientCacheTtl : CacheTtl);
        return snapshot;
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
        double? positionSize = reliable && grandTotal > 0 ? Dbl(lots.Sum(MarketValue)) / grandTotal * 100 : null;
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
        var sector = lots.Select(l => l.Item.Sector).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? scan.Sector ?? "";
        var target = string.IsNullOrEmpty(sector) ? null
            : sectorTargets.FirstOrDefault(t => string.Equals(t.Sector, sector, StringComparison.OrdinalIgnoreCase));
        var sectorOver = false;
        double? sectorPct = null;
        if (reliable && target is not null && stockTotal > 0)
        {
            var actual = all.Where(s => !IsClose(s.Item.TransactionType)
                    && (string.Equals(s.Item.Sector ?? "", sector, StringComparison.OrdinalIgnoreCase)
                        || (string.IsNullOrWhiteSpace(s.Item.Sector)
                            && string.Equals(s.Item.Symbol, item.Symbol, StringComparison.OrdinalIgnoreCase)))).Sum(MarketValue) / stockTotal * 100m;
            sectorPct = Dbl(actual);
            sectorOver = actual - target.TargetPct > 2m;
        }

        return new PortfolioEvidence(
            context.UnrealizedGainPct, entry, evidenceDate, evidenceUtc, positionSize,
            limit is null ? null : Dbl(limit.TargetPct), sectorOver,
            scan.PriceStructure?.HasHardStructuralNegative == true, item.DecisionSource,
            ambiguous, ambiguityDetail, sectorPct, target is null ? null : Dbl(target.TargetPct), !string.IsNullOrEmpty(sector), risk, valuation);
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

    /// <summary>
    /// Decides whether allocation percentages can be trusted. There is no FX service and no currency on holdings or cash, so
    /// the currency of each component is the provider's quote currency (ticker suffix as a labelled fallback); cash follows the
    /// ledger convention (reporting currency CAD unless the account or description is marked USD). More than one currency
    /// means the raw sum is not meaningful and no conversion is guessed.
    /// </summary>
    internal static ValuationCheck ClassifyValuation(
        IEnumerable<(string Symbol, string? QuoteCurrency)> stocks,
        IEnumerable<(string? Account, string? Description)> cash,
        IEnumerable<string> optionUnderlyings)
    {
        var currencies = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var (symbol, quoteCurrency) in stocks)
            currencies.Add(PortfolioManager.Api.Services.Risk.TradingCurrency.Resolve(symbol, quoteCurrency).Code);
        foreach (var u in optionUnderlyings)
            currencies.Add(PortfolioManager.Api.Services.Risk.TradingCurrency.Resolve(u, null).Code);
        foreach (var (account, description) in cash)
            currencies.Add(UsdMarker.IsMatch(account ?? "") || UsdMarker.IsMatch(description ?? "") ? "USD" : "CAD");

        if (currencies.Count <= 1)
            return new ValuationCheck(ValuationCheck.SingleCurrency, currencies.Count == 0 ? "no valued components" : $"all components in {currencies.Single()}");
        return new ValuationCheck(ValuationCheck.MixedCurrency,
            $"the portfolio total mixes {string.Join(" and ", currencies)} with no verified conversion");
    }

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
        return ClassifyValuation(stocks, cash, options);
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
        double? exposure = valuation.Reliable && grandTotal > 0 ? Dbl(lots.Sum(MarketValue)) / grandTotal * 100 : null;
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
