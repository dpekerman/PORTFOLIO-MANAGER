using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services.FinalAction;

namespace PortfolioManager.Api.Services.Opportunities;

public interface IOpportunityService
{
    /// <summary>Latest stored snapshot for the user. Never calls market data.</summary>
    Task<OpportunitySnapshotDto> GetLatestAsync(string userId, CancellationToken ct = default);

    /// <summary>One row's detail from a specific snapshot (or the latest). Null when the snapshot no longer exists (HTTP 410).</summary>
    Task<OpportunityDetailDto?> GetDetailAsync(string userId, string symbol, int? snapshotId, CancellationToken ct = default);

    /// <summary>Builds and stores a new snapshot. Single-flight per user: throws <see cref="OpportunityRefreshInProgressException"/>.</summary>
    Task<OpportunitySnapshotDto> RefreshAsync(string userId, CancellationToken ct = default);
}

/// <summary>
/// PM-029 orchestration (docs/pm-029-trading-opportunities.md). Read-only decision support:
/// <list type="bullet">
/// <item>FINAL ACTION comes only from <see cref="IFinalActionService"/> (the user's evaluation bundle for Watchlist + holdings, and the
/// side-effect-free universe evaluation for TSX 60-only symbols). Decisions are copied verbatim.</item>
/// <item>Scores, Sector Heat, setups and Fit come from the pure calculators, using the exact scans the decisions were made from.</item>
/// <item>The only rows a refresh writes are <c>OpportunitySnapshots</c>, <c>OpportunityRefreshState</c> and Value Screener
/// <c>Origin = Universe</c> snapshots.</item>
/// </list>
/// </summary>
public sealed class OpportunityService(
    AppDbContext db,
    IFinalActionService finalActions,
    IMarketDataProvider marketData,
    ISecurityAnalysisResolver analysisResolver,
    ValueScreenerService valueScreener,
    ValueScreenerPersistenceService valuePersistence,
    ILogger<OpportunityService> logger) : IOpportunityService
{
    public const string IndexCode = "TSX60";
    public const string SourceWatchlist = "Watchlist";
    public const string SourcePortfolio = "Portfolio";
    public const string SourceTsx60 = "TSX60";
    internal const int HistoryBatchSize = 5;
    internal static TimeSpan HistoryBatchDelay = TimeSpan.FromMilliseconds(300);

    /// <summary>Yahoo USD→CAD daily series, used only to put a US sector-ETF proxy into CAD for TSX stocks.</summary>
    private const string UsdCadSymbol = "CAD=X";
    private const string UsProxySuffix = " (US proxy, converted to CAD)";
    private const int SnapshotsKept = 5;

    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> RefreshGates = new(StringComparer.Ordinal);

    // ── Read ──────────────────────────────────────────────────────────────────

    public async Task<OpportunitySnapshotDto> GetLatestAsync(string userId, CancellationToken ct = default)
    {
        var state = await db.OpportunityRefreshStates.AsNoTracking().SingleOrDefaultAsync(s => s.UserId == userId, ct);
        var snap = await db.OpportunitySnapshots.AsNoTracking()
            .Where(s => s.UserId == userId).OrderByDescending(s => s.ComputedAtUtc).ThenByDescending(s => s.Id).FirstOrDefaultAsync(ct);
        var inFlight = RefreshGates.TryGetValue(userId, out var gate) && gate.CurrentCount == 0;
        var lastAttempt = state is null ? null : new OpportunityLastAttemptDto(state.LastAttemptAtUtc, state.LastAttemptStatus, state.ErrorSummary);
        if (snap is null)
            return new OpportunitySnapshotDto(null, OpportunityScoringConfig.Version, OpportunityScoringConfig.Disclaimer, null, null, null,
                null, null, lastAttempt, new OpportunityUniverseCountsDto(0, 0, 0, 0), true, false, inFlight, []);

        var payload = Deserialize(snap.SnapshotJson);
        var portfolioSnapshot = await db.PortfolioSnapshots.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == userId, ct);
        var listingNames = await ExistingListingNamesAsync(userId, portfolioSnapshot, ct);
        var session = DateOnly.FromDateTime(snap.DataAsOfSession);
        return new OpportunitySnapshotDto(
            snap.Id, snap.ScoringVersion, OpportunityScoringConfig.DisclaimerFor(snap.ScoringVersion), snap.ComputedAtUtc, snap.FinalActionComputedAtUtc, session,
            snap.Status, snap.ErrorSummary, lastAttempt, payload.Counts,
            session < TradingCalendar.LastCompletedSession(DateTime.UtcNow, MarketExchange.Tsx),
            portfolioSnapshot?.UpdatedAt is { } now && payload.PortfolioUpdatedAtUtc is { } then ? now > then : portfolioSnapshot is not null && payload.PortfolioUpdatedAtUtc is null,
            inFlight,
            payload.Records.Select(r => ProjectListingName(r.Row, listingNames)).ToList());
    }

    public async Task<OpportunityDetailDto?> GetDetailAsync(string userId, string symbol, int? snapshotId, CancellationToken ct = default)
    {
        var query = db.OpportunitySnapshots.AsNoTracking().Where(s => s.UserId == userId);
        var snap = snapshotId is { } id
            ? await query.SingleOrDefaultAsync(s => s.Id == id, ct)
            : await query.OrderByDescending(s => s.ComputedAtUtc).ThenByDescending(s => s.Id).FirstOrDefaultAsync(ct);
        if (snap is null) return null;
        var record = Deserialize(snap.SnapshotJson).Records
            .FirstOrDefault(r => string.Equals(r.Row.Symbol, symbol.Trim(), StringComparison.OrdinalIgnoreCase));
        if (record is null) return null;
        var portfolioSnapshot = await db.PortfolioSnapshots.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == userId, ct);
        var listingNames = await ExistingListingNamesAsync(userId, portfolioSnapshot, ct);
        return record with { SnapshotId = snap.Id, Row = ProjectListingName(record.Row, listingNames) };
    }

    // ── Refresh ───────────────────────────────────────────────────────────────

    public async Task<OpportunitySnapshotDto> RefreshAsync(string userId, CancellationToken ct = default)
    {
        var gate = RefreshGates.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
        if (!await gate.WaitAsync(0, ct)) throw new OpportunityRefreshInProgressException();
        try
        {
            var startedAt = DateTime.UtcNow;
            await SetStateAsync(userId, startedAt, "Running", null, null, ct);
            try
            {
                var (payload, evaluation, warnings) = await BuildAsync(userId, ct);
                var status = warnings.Count == 0 ? "Succeeded" : "Partial";
                var errorSummary = warnings.Count == 0 ? null : Truncate(string.Join("; ", warnings), 1900);
                var snapshot = new OpportunitySnapshot
                {
                    UserId = userId,
                    ScoringVersion = OpportunityScoringConfig.Version,
                    EvaluationId = evaluation.EvaluationId.ToString(),
                    ComputedAtUtc = DateTime.UtcNow,
                    FinalActionComputedAtUtc = evaluation.Snapshot.ComputedAtUtc,
                    DataAsOfSession = TradingCalendar.LastCompletedSession(startedAt, MarketExchange.Tsx).ToDateTime(TimeOnly.MinValue),
                    Status = status,
                    ErrorSummary = errorSummary,
                    SnapshotJson = JsonSerializer.Serialize(payload, Json),
                };
                db.OpportunitySnapshots.Add(snapshot);
                await db.SaveChangesAsync(ct);

                var old = await db.OpportunitySnapshots.Where(s => s.UserId == userId)
                    .OrderByDescending(s => s.ComputedAtUtc).ThenByDescending(s => s.Id).Skip(SnapshotsKept).ToListAsync(ct);
                if (old.Count > 0)
                {
                    db.OpportunitySnapshots.RemoveRange(old);
                    await db.SaveChangesAsync(ct);
                }
                await SetStateAsync(userId, startedAt, status, errorSummary, snapshot.Id, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A failed refresh is recorded separately and never replaces or relabels the last successful snapshot.
                logger.LogError(ex, "[Opportunities] Refresh failed for user {UserId}", userId);
                await SetStateAsync(userId, startedAt, "Failed", Truncate(ex.Message, 1900), null, CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                await SetStateAsync(userId, startedAt, "Failed", "Refresh was cancelled.", null, CancellationToken.None);
                throw;
            }
        }
        finally { gate.Release(); }
        return await GetLatestAsync(userId, ct);
    }

    private async Task SetStateAsync(string userId, DateTime attemptAt, string status, string? error, int? snapshotId, CancellationToken ct)
    {
        var state = await db.OpportunityRefreshStates.SingleOrDefaultAsync(s => s.UserId == userId, ct);
        if (state is null)
        {
            state = new OpportunityRefreshState { UserId = userId };
            db.OpportunityRefreshStates.Add(state);
        }
        state.LastAttemptAtUtc = attemptAt;
        state.LastAttemptStatus = status;
        state.ErrorSummary = error;
        if (snapshotId is not null) state.LastSuccessfulSnapshotId = snapshotId;
        await db.SaveChangesAsync(ct);
    }

    private sealed record UniverseMember(string Symbol, bool Watchlist, bool Portfolio, bool Tsx60, string? WatchlistRole, string? IndexName);

    private async Task<(OpportunitySnapshotPayload Payload, FinalActionEvaluation Evaluation, List<string> Warnings)> BuildAsync(
        string userId, CancellationToken ct)
    {
        var warnings = new List<string>();
        var nowUtc = DateTime.UtcNow;

        // 1. Authoritative Final Action evaluation for the user's Watchlist + holdings (the same evaluation the Final Action page uses).
        var evaluation = await finalActions.EvaluatePreviewAsync(userId, ct);
        var basis = evaluation.Portfolio;

        // 2. Universe: Watchlist (same visibility rule as Final Action) + open holdings + current TSX 60 constituents.
        var watchlist = await db.WatchlistItems.AsNoTracking().Where(w => w.UserId == userId || w.UserId == null).ToListAsync(ct);
        var constituents = await db.IndexConstituents.AsNoTracking()
            .Where(c => c.IndexCode == IndexCode && c.EffectiveTo == null).ToListAsync(ct);
        var members = new Dictionary<string, UniverseMember>(StringComparer.OrdinalIgnoreCase);
        UniverseMember Get(string s) => members.TryGetValue(s, out var m) ? m : new UniverseMember(s, false, false, false, null, null);
        foreach (var w in watchlist)
        {
            var key = w.Symbol.Trim().ToUpperInvariant();
            members[key] = Get(key) with { Watchlist = true, WatchlistRole = string.IsNullOrWhiteSpace(w.Role) ? "Strategic" : w.Role.Trim() };
        }
        foreach (var h in basis.OpenHoldings)
        {
            var key = h.Item.Symbol.Trim().ToUpperInvariant();
            members[key] = Get(key) with { Portfolio = true };
        }
        foreach (var c in constituents)
        {
            var key = c.Symbol.Trim().ToUpperInvariant();
            if (key.Length == 0) continue;
            members[key] = Get(key) with { Tsx60 = true, IndexName = string.IsNullOrWhiteSpace(c.CompanyName) ? null : c.CompanyName.Trim() };
        }
        var universeOnly = members.Values.Where(m => !m.Watchlist && !m.Portfolio).Select(m => m.Symbol).OrderBy(s => s, StringComparer.Ordinal).ToList();

        // 3. Fundamentals for universe-only symbols: re-screen only those with no Universe result within the freshness window (U3/U5).
        if (universeOnly.Count > 0)
            await RefreshUniverseFundamentalsAsync(universeOnly, nowUtc, warnings, ct);

        // 4. Side-effect-free decisions for universe-only symbols (identical watchlist path, role Strategic).
        var universe = universeOnly.Count > 0
            ? await finalActions.EvaluateUniverseAsync(userId, universeOnly, ct)
            : new UniverseFinalActions([], new Dictionary<string, RsiScanResult>(), new HashSet<string>(), nowUtc);

        var scans = new Dictionary<string, RsiScanResult>(evaluation.Scans, StringComparer.OrdinalIgnoreCase);
        foreach (var (s, scan) in universe.Scans) scans.TryAdd(s, scan);

        var valueByTicker = await FinalActionService.LoadValueScreenerAsync(db, ct);
        var valueOrigins = await ValueOriginTimesAsync(ct);
        var mappings = await db.SectorBenchmarkMappings.AsNoTracking().Where(m => m.IsActive).ToListAsync(ct);
        var watchQuotes = await WatchlistQuotesAsync(userId, ct);
        var savedPlans = await SavedPlansAsync(userId, basis, ct);

        // 5. Identity: a symbol with no scan may be one whose analysis security needs the user's mapping (read-only check).
        var unresolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in members.Keys.Where(s => !scans.ContainsKey(s)))
        {
            try
            {
                var r = await analysisResolver.ResolveReadOnlyAsync(s, userId, ct);
                if (r.ResolutionStatus == UnderlyingResolutionStatus.NeedsUserInput) unresolved.Add(s);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogDebug(ex, "[Opportunities] Identity check failed for {Symbol}", s);
            }
        }

        // 6. Classification, benchmark and calendar per row; then daily histories (throttled) for Sector Heat.
        var plans = new Dictionary<string, RowPlan>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in members.Values)
        {
            scans.TryGetValue(m.Symbol, out var scan);
            valueByTicker.TryGetValue(m.Symbol, out var value);
            watchQuotes.TryGetValue(m.Symbol, out var wq);
            var listingQuote = wq is not null && string.Equals(wq.Symbol, m.Symbol, StringComparison.OrdinalIgnoreCase) ? wq : null;
            if (wq is not null && listingQuote is null)
                logger.LogWarning("Ignoring listing name/type metadata for {Symbol}: cached quote identifies {QuoteSymbol}.", m.Symbol, wq.Symbol);
            var lots = basis.OpenHoldings.Where(h => string.Equals(h.Item.Symbol, m.Symbol, StringComparison.OrdinalIgnoreCase)).Select(h => h.Item).ToList();
            var providerSector = FirstUsable(scan?.Sector, wq?.Sector, value?.Sector);
            var classification = SectorClassificationResolver.Resolve(lots, providerSector, FirstUsable(wq?.Industry));
            var analysisTicker = scan is null || string.IsNullOrEmpty(scan.AnalysisTicker) ? m.Symbol : scan.AnalysisTicker;
            var exchange = TradingCalendar.ExchangeFor(analysisTicker, scan?.AnalysisMarket);
            var mapping = SectorClassificationResolver.FindBenchmark(mappings, classification.Sector, exchange);
            var usProxy = false;
            if (mapping is null && exchange == MarketExchange.Tsx && classification.IsClassified)
            {
                // TSX has no sector ETF for Consumer Discretionary, Communication Services or Health Care: use the US sector ETF in CAD.
                mapping = SectorClassificationResolver.FindBenchmark(mappings, classification.Sector, MarketExchange.Nyse);
                usProxy = mapping is not null;
            }
            plans[m.Symbol] = new RowPlan(m, scan, value, lots, classification, analysisTicker, exchange, mapping,
                TradingCalendar.LastCompletedSession(nowUtc, exchange),
                ResolveInstrumentType(scan, listingQuote?.InstrumentType),
                listingQuote?.CompanyName, usProxy);
        }
        var historySymbols = plans.Values.Where(p => p.Scan is not null)
            .SelectMany(p => p.Mapping is null ? new[] { p.AnalysisTicker } : new[] { p.AnalysisTicker, p.Mapping.BenchmarkSymbol })
            .Concat(plans.Values.Any(p => p.Scan is not null && p.BenchmarkIsUsProxy) ? [UsdCadSymbol] : [])
            .Select(s => s.Trim().ToUpperInvariant()).Distinct().OrderBy(s => s, StringComparer.Ordinal).ToList();
        var histories = await LoadHistoriesAsync(historySymbols, ct);
        var failedBenchmarks = plans.Values.Where(p => p.Mapping is not null)
            .Select(p => p.Mapping!.BenchmarkSymbol.Trim().ToUpperInvariant())
            .Concat(plans.Values.Any(p => p.Scan is not null && p.BenchmarkIsUsProxy) ? [UsdCadSymbol] : []).Distinct()
            .Where(b => histories.GetValueOrDefault(b) is not { Count: > 0 }).ToList();
        if (failedBenchmarks.Count > 0) warnings.Add($"Benchmark history unavailable: {string.Join(", ", failedBenchmarks)}");

        // 7. Calculators and decisions per row.
        var faRows = evaluation.Snapshot.Watchlist.Concat(evaluation.Snapshot.Portfolio).Concat(universe.Rows).ToList();
        var records = new List<OpportunityDetailDto>(plans.Count);
        foreach (var p in plans.Values.OrderBy(p => p.Member.Symbol, StringComparer.Ordinal))
        {
            var benchInfo = p.Mapping is null ? null
                : new SectorBenchmarkInfo(p.Mapping.BenchmarkSymbol.Trim().ToUpperInvariant(),
                    p.BenchmarkIsUsProxy ? p.Mapping.BenchmarkName + UsProxySuffix : p.Mapping.BenchmarkName,
                    p.BenchmarkIsUsProxy ? "CAD" : p.Mapping.Currency);
            var analysisBars = histories.GetValueOrDefault(p.AnalysisTicker.Trim().ToUpperInvariant());
            var benchBars = benchInfo is null ? null
                : p.BenchmarkIsUsProxy
                    ? SectorRelativeCalculator.ConvertUsBenchmarkToCad(
                        histories.GetValueOrDefault(benchInfo.Symbol), histories.GetValueOrDefault(UsdCadSymbol), analysisBars)
                    : histories.GetValueOrDefault(benchInfo.Symbol);
            SectorRelativeResult? sector = p.Scan is null ? null : SectorRelativeCalculator.Compute(
                analysisBars, benchBars,
                p.ExpectedSession, p.Exchange, p.Scan.AnalysisCurrency, benchInfo, p.Classification.IsClassified);
            var setup = TradeSetupResolver.Resolve(p.Scan, p.ExpectedSession);
            var risk = TradeSetupResolver.AssessRisk(p.Scan, setup, p.ExpectedSession);
            var input = new OpportunityScoringInput(p.Scan, p.ExpectedSession, sector, setup, p.Value, DateTime.UtcNow,
                IdentityResolved: !unresolved.Contains(p.Member.Symbol), InstrumentType: p.InstrumentType);
            var swing = OpportunityScoringEngine.ScoreSwing(input);
            var strategic = OpportunityScoringEngine.ScoreStrategic(input);

            var exposure = FinalActionService.ExposureFor(p.Member.Symbol, p.Member.WatchlistRole ?? "Strategic", p.Classification.Sector ?? p.Scan?.Sector, basis);
            var fit = PortfolioFitCalculator.Evaluate(new FitInput(exposure.Owned, exposure.AllocationReliable, exposure.PositionSizePct,
                exposure.PositionLimitPct, exposure.RolePolicyAmbiguous, exposure.SectorKnown, exposure.SectorPct, exposure.SectorTargetPct));

            var decisions = faRows.Where(r => string.Equals(r.Symbol, p.Member.Symbol, StringComparison.OrdinalIgnoreCase)).ToList();
            var row = BuildRow(p, swing, strategic, sector, setup, risk, fit, decisions, unresolved.Contains(p.Member.Symbol), valueOrigins);
            var structuralHistory = histories.GetValueOrDefault(p.AnalysisTicker.Trim().ToUpperInvariant());
            // The Good/Caution/Poor policy was approved by the owner on 2026-11-15; it remains advisory and never changes scores, FINAL ACTION or stops.
            // Swing grades the staged-signal setup; Strategic grades the confirmed-structure setup (no signal stop required).
            row = row with
            {
                SetupQuality = SetupQualityCalculator.Evaluate(p.Scan, structuralHistory, p.ExpectedSession, DateTime.UtcNow,
                    classificationApproved: true),
                StrategicSetupQuality = SetupQualityCalculator.EvaluateStrategic(p.Scan, structuralHistory, p.ExpectedSession, DateTime.UtcNow),
            };
            var evidence = BuildEvidence(p, valueOrigins, basis, evaluation, decisions);
            records.Add(new OpportunityDetailDto(0, row, swing, strategic, sector, setup, decisions,
                savedPlans.GetValueOrDefault(p.Member.Symbol) ?? [], evidence));
        }

        var counts = new OpportunityUniverseCountsDto(
            members.Values.Count(m => m.Watchlist), members.Values.Count(m => m.Portfolio), members.Values.Count(m => m.Tsx60), members.Count);
        return (new OpportunitySnapshotPayload(counts, basis.PortfolioUpdatedAt, records), evaluation, warnings);
    }

    private sealed record RowPlan(
        UniverseMember Member,
        RsiScanResult? Scan,
        ValueScreenerResult? Value,
        IReadOnlyList<PortfolioItemDto> Lots,
        SectorClassification Classification,
        string AnalysisTicker,
        MarketExchange Exchange,
        SectorBenchmarkMapping? Mapping,
        DateOnly ExpectedSession,
        string? InstrumentType,
        string? ListingCompanyName,
        bool BenchmarkIsUsProxy = false);

    // ── Row mapping ───────────────────────────────────────────────────────────

    private static OpportunityRowDto BuildRow(
        RowPlan p, ScoreResult swing, ScoreResult strategic, SectorRelativeResult? sector, TradeSetupResult setup, RiskAssessment risk,
        FitResult fit, IReadOnlyList<FinalActionResultDto> decisions, bool unresolved, IReadOnlyDictionary<string, (string Origin, DateTime RunAt)> valueOrigins)
    {
        var m = p.Member;
        var scan = p.Scan;
        var sources = new List<string>();
        if (m.Watchlist) sources.Add(SourceWatchlist);
        if (m.Portfolio) sources.Add(SourcePortfolio);
        if (m.Tsx60) sources.Add(SourceTsx60);
        var accounts = p.Lots.Select(l => l.AccountType ?? "").Where(a => a.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(a => a).ToList();
        var roles = p.Lots.Select(l => string.IsNullOrWhiteSpace(l.HoldingRole) ? "Strategic" : l.HoldingRole!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(r => r).ToList();
        var companyName = ListingName(m.Symbol, p.Lots.OrderBy(l => l.Id).Select(l => l.CompanyName)
            .Concat([p.ListingCompanyName, m.IndexName, scan?.UsesUnderlyingSecurity == true ? null : scan?.CompanyName, p.Value?.Description])) ?? m.Symbol;
        var stale = scan is not null && scan.TradingDate != p.ExpectedSession;
        var rowStatus = unresolved ? "IdentityUnresolved" : scan is null ? "NoScan" : stale ? "StaleScan" : "Ok";
        var d20 = sector?.D20;
        var d60 = sector?.D60;
        string? valueOrigin = p.Value is not null && valueOrigins.TryGetValue(p.Member.Symbol, out var vo) ? vo.Origin : null;

        return new OpportunityRowDto(
            m.Symbol, companyName, scan?.AnalysisTicker is { Length: > 0 } at ? at : null, scan?.UsesUnderlyingSecurity == true,
            p.Exchange.ToString(), FirstUsable(scan?.AnalysisCurrency),
            sources, p.Lots.Count > 0, accounts, roles, m.WatchlistRole,
            p.Classification.Sector, p.Classification.Industry, p.Classification.Source,
            Summary(swing), Summary(strategic),
            new OpportunitySectorHeatDto(d20?.IsAvailable == true ? d20.DisplayDiffPp : null, d60?.IsAvailable == true ? d60.DisplayDiffPp : null,
                d20?.StockReturnPct, d20?.BenchmarkReturnPct,
                (sector?.Status ?? SectorHeatStatus.Stale).ToString(), (d20?.Direction ?? SectorHeatDirection.None).ToString(), d20?.Intensity ?? 0,
                sector?.BenchmarkSymbol),
            new OpportunitySetupDto(setup.Kind.ToString(), setup.Entry, setup.Stop, setup.Target, setup.TargetSource, setup.RewardRisk,
                setup.RiskPct, setup.RewardPct, setup.StopDistanceAtr, setup.Currency, setup.ReasonCodes),
            new OpportunityRiskDto(risk.Category.ToString(), risk.RiskToStopPct, risk.AtrPct, risk.HardStructuralNegative, risk.Basis),
            new OpportunityFitDto(fit.Status.ToString(), fit.ReasonCodes, fit.WeightPct, fit.PositionLimitPct, fit.PositionHeadroomPct,
                fit.SectorWeightPct, fit.SectorTargetPct, fit.SectorHeadroomPct),
            SummarizeDecisions(decisions, p.Lots.Count > 0),
            scan is null
                ? new OpportunityIndicatorsDto(null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null,
                    d20?.IsAvailable == true ? d20.DisplayDiffPp : null, d60?.IsAvailable == true ? d60.DisplayDiffPp : null)
                : new OpportunityIndicatorsDto(
                    Pos(scan.CurrentPrice), scan.ChangePercent, scan.Rsi, scan.RsiSignalAvailable ? scan.RsiSignal : null, scan.MacdHistogram,
                    Pos(scan.Ema20Price), Pos(scan.Sma50Price), scan.Has200Dma ? Pos(scan.Sma200) : null, Pos(scan.DailyAtr), Pos(scan.VolumeRatio),
                    Pos(scan.Week52High), Pos(scan.Week52Low), Pos(scan.DynamicStopLoss),
                    NullIfBlank(scan.TrendShift), NullIfBlank(scan.MaStructure), NullIfBlank(scan.FibZone), NullIfBlank(scan.ChannelState),
                    d20?.IsAvailable == true ? d20.DisplayDiffPp : null, d60?.IsAvailable == true ? d60.DisplayDiffPp : null),
            new OpportunityFreshnessDto(scan?.TradingDate, p.ExpectedSession, scan?.ScannedAt, scan is null || stale,
                p.Value?.AnalyzedAt, valueOrigin),
            rowStatus, p.InstrumentType, FibonacciSwing: !stale && scan?.FibonacciSwing is { } fib
                && fib.Matches(m.Symbol) ? fib : null);
    }

    private static OpportunityScoreSummaryDto Summary(ScoreResult s) =>
        new(s.Score, s.CompletenessPct, s.Status.ToString(), s.Band.ToString(), s.ReasonCodes);

    /// <summary>
    /// Owned symbols are summarised from their account/lot decisions (the Portfolio rows); a Watchlist "ADD" view of an owned symbol is
    /// listed in the drawer but never overrides a lot decision. Non-owned symbols use their single Watchlist or Universe decision.
    /// Differing labels ⇒ Multiple (neutral); the heatmap never picks a winner.
    /// </summary>
    internal static OpportunityFinalActionDto SummarizeDecisions(IReadOnlyList<FinalActionResultDto> decisions, bool owned)
    {
        var primary = owned
            ? decisions.Where(d => d.Source == SignalSources.Portfolio).ToList()
            : decisions.Where(d => d.Source != SignalSources.Portfolio).ToList();
        if (primary.Count == 0) primary = decisions.ToList();
        if (primary.Count == 0) return new OpportunityFinalActionDto("None", null, null, null, null, false, null, null, null, 0, []);
        var labels = primary.Select(d => d.FinalAction).Distinct(StringComparer.Ordinal).OrderBy(l => l, StringComparer.Ordinal).ToList();
        if (labels.Count > 1)
            return new OpportunityFinalActionDto("Multiple", null, null, null, null, false, null, null, null, primary.Count, labels);
        var d = primary[0];
        return new OpportunityFinalActionDto("Single", d.FinalAction, d.FinalActionId, d.Category, d.FinalActionClass,
            primary.All(x => x.IsActionable), d.EvaluationState, d.EvidenceHealth, d.Reason, primary.Count, labels);
    }

    private static OpportunityEvidenceDto BuildEvidence(
        RowPlan p, IReadOnlyDictionary<string, (string Origin, DateTime RunAt)> valueOrigins,
        FinalActionPortfolioBasis basis, FinalActionEvaluation evaluation, IReadOnlyList<FinalActionResultDto> decisions)
    {
        var warnings = new List<string>();
        if (p.Scan is null) warnings.Add("No technical scan was available for this symbol.");
        else if (p.Scan.TradingDate != p.ExpectedSession)
            warnings.Add($"Technical data is from {p.Scan.TradingDate:yyyy-MM-dd}; the last completed session is {p.ExpectedSession:yyyy-MM-dd}.");
        if (p.Scan?.UsesUnderlyingSecurity == true)
            warnings.Add($"Analysis uses the underlying security {p.Scan.AnalysisTicker}; prices are in {p.Scan.AnalysisCurrency}.");
        if (p.Value?.MarketCapFallbackUsed == true) warnings.Add("Market capitalisation was estimated; market-cap based factors are excluded.");
        if (!basis.Valuation.Reliable) warnings.Add($"Allocation percentages are not verifiable: {basis.Valuation.Detail}.");
        string? valueOrigin = p.Value is not null && valueOrigins.TryGetValue(p.Member.Symbol, out var vo) ? vo.Origin : null;
        return new OpportunityEvidenceDto(
            OpportunityScoringConfig.Version, OpportunityScoringConfig.Disclaimer, OpportunityScoringConfig.SectorMethodology,
            "Yahoo Finance daily chart (split-adjusted close)",
            p.Mapping?.BenchmarkSymbol.Trim().ToUpperInvariant(),
            string.IsNullOrWhiteSpace(p.Mapping?.BenchmarkName) ? null : p.Mapping.BenchmarkName + (p.BenchmarkIsUsProxy ? UsProxySuffix : ""),
            p.Classification.Source,
            p.Scan?.ScannedAt, p.Scan?.TradingDate, p.ExpectedSession,
            valueOrigin, p.Value?.AnalyzedAt,
            p.Value?.EarningsYieldBasis, p.Value?.FcfYieldBasis, p.Value?.CapitalEfficiencyBasis,
            decisions.Select(d => d.AllocationStatus).FirstOrDefault(a => a is not null) ?? basis.Valuation.Status,
            basis.PortfolioUpdatedAt, evaluation.EvaluationId.ToString(), evaluation.Snapshot.ComputedAtUtc, warnings,
            p.Mapping is null ? null : p.BenchmarkIsUsProxy ? "Sector mapping (US proxy)" : "Sector mapping",
            p.BenchmarkIsUsProxy ? "CAD" : p.Mapping?.Currency, p.Mapping?.Source,
            p.BenchmarkIsUsProxy ? "No TSX benchmark exists for this sector; the US sector ETF is converted to CAD with the USD/CAD daily rate." : p.Mapping?.Notes,
            p.Mapping?.SourceAsOfDate);
    }

    // ── Data loading ──────────────────────────────────────────────────────────

    private async Task RefreshUniverseFundamentalsAsync(IReadOnlyList<string> universeOnly, DateTime nowUtc, List<string> warnings, CancellationToken ct)
    {
        var latest = await db.ValueScreenerSnapshots.AsNoTracking()
            .Where(s => s.Origin == nameof(ValueOrigin.Universe)).OrderByDescending(s => s.RunAt).FirstOrDefaultAsync(ct);
        var previous = DeserializeValues(latest?.ResultsJson);
        var byKey = previous.GroupBy(r => r.Symbol, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var due = universeOnly.Where(s => !byKey.TryGetValue(s, out var r) || nowUtc - AsUtc(r.AnalyzedAt) > OpportunityScoringConfig.FundamentalsMaxAge).ToList();
        if (due.Count == 0) return;
        try
        {
            var fresh = await valueScreener.RunUniverseAsync(due, ct);
            foreach (var r in fresh) byKey[r.Symbol] = r;
            // Keep only current universe-only members: a symbol later watched or bought is screened under its own origin.
            var merged = universeOnly.Where(byKey.ContainsKey).Select(s => byKey[s]).ToList();
            await valuePersistence.SaveAsync(nameof(ValueOrigin.Universe), merged, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "[Opportunities] Universe fundamentals refresh failed");
            warnings.Add("Universe fundamentals refresh failed; earlier results (if any) were used.");
        }
    }

    private async Task<Dictionary<string, (string Origin, DateTime RunAt)>> ValueOriginTimesAsync(CancellationToken ct)
    {
        var map = new Dictionary<string, (string, DateTime)>(StringComparer.OrdinalIgnoreCase);
        foreach (var origin in new[] { "Universe", "Watchlist", "Portfolio" })
        {
            var snap = await db.ValueScreenerSnapshots.AsNoTracking()
                .Where(s => s.Origin == origin).OrderByDescending(s => s.RunAt).FirstOrDefaultAsync(ct);
            foreach (var r in DeserializeValues(snap?.ResultsJson)) map[r.Symbol] = (origin, snap!.RunAt);
        }
        return map;
    }

    private async Task<Dictionary<string, StockQuote>> WatchlistQuotesAsync(string userId, CancellationToken ct)
    {
        var map = new Dictionary<string, StockQuote>(StringComparer.OrdinalIgnoreCase);
        var snap = await db.WatchlistSnapshots.AsNoTracking().SingleOrDefaultAsync(s => s.UserId == userId, ct);
        if (string.IsNullOrWhiteSpace(snap?.SnapshotJson)) return map;
        try
        {
            foreach (var w in JsonSerializer.Deserialize<List<WatchlistSummaryDto>>(snap.SnapshotJson, Json) ?? [])
                if (w.Quote is not null) map[w.Item.Symbol] = w.Quote;
        }
        catch (JsonException) { }
        return map;
    }

    /// <summary>Current saved risk plans of open holdings, keyed by symbol. Display only; never an input to a score.</summary>
    private async Task<Dictionary<string, IReadOnlyList<SavedRiskPlanDto>>> SavedPlansAsync(
        string userId, FinalActionPortfolioBasis basis, CancellationToken ct)
    {
        var held = basis.OpenHoldings.Select(h => h.Item.Symbol.ToUpperInvariant()).Distinct().ToList();
        if (held.Count == 0) return new(StringComparer.OrdinalIgnoreCase);
        var plans = await db.RiskPlans.AsNoTracking()
            .Where(r => (r.UserId == userId || r.UserId == null) && held.Contains(r.Symbol.ToUpper())).ToListAsync(ct);
        var result = new Dictionary<string, IReadOnlyList<SavedRiskPlanDto>>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in plans.GroupBy(r => r.Symbol.ToUpperInvariant()))
        {
            var list = new List<SavedRiskPlanDto>();
            foreach (var plan in g.OrderBy(r => r.AccountType, StringComparer.OrdinalIgnoreCase))
            {
                var lots = basis.OpenHoldings.Where(h => string.Equals(h.Item.Symbol, plan.Symbol, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(h.Item.AccountType ?? "", plan.AccountType, StringComparison.OrdinalIgnoreCase)).ToList();
                if (lots.Count == 0) continue;
                var shares = lots.Sum(l => l.Item.Shares);
                decimal? avg = shares > 0 ? Math.Round(lots.Sum(l => l.Item.Shares * l.Item.AverageCostBasis) / shares, 4) : null;
                decimal? rr = avg is { } e && plan.PlannedStopPrice is { } s && plan.TargetPrice is { } t && s < e && e < t
                    ? Math.Round((t - e) / (e - s), 2) : null;
                list.Add(new SavedRiskPlanDto(plan.AccountType, avg, plan.PlannedStopPrice, plan.TargetPrice, rr, plan.StopType, plan.Currency, plan.UpdatedAtUtc));
            }
            if (list.Count > 0) result[g.Key] = list;
        }
        return result;
    }

    /// <summary>Daily closes, fetched once per symbol per refresh in throttled batches (5 symbols, 300 ms apart).</summary>
    private async Task<Dictionary<string, IReadOnlyList<MarketDailyClose>?>> LoadHistoriesAsync(IReadOnlyList<string> symbols, CancellationToken ct)
    {
        var result = new Dictionary<string, IReadOnlyList<MarketDailyClose>?>(StringComparer.OrdinalIgnoreCase);
        var first = true;
        foreach (var batch in symbols.Chunk(HistoryBatchSize))
        {
            if (!first && HistoryBatchDelay > TimeSpan.Zero) await Task.Delay(HistoryBatchDelay, ct);
            first = false;
            var fetched = await Task.WhenAll(batch.Select(async s =>
            {
                try { return (s, await marketData.GetDailyClosesAsync(s, ct)); }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "[Opportunities] History fetch failed for {Symbol}", s);
                    return (s, (IReadOnlyList<MarketDailyClose>?)null);
                }
            }));
            foreach (var (s, bars) in fetched) result[s] = bars;
        }
        return result;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static OpportunitySnapshotPayload Deserialize(string json) =>
        JsonSerializer.Deserialize<OpportunitySnapshotPayload>(json, Json)
        ?? new OpportunitySnapshotPayload(new OpportunityUniverseCountsDto(0, 0, 0, 0), null, []);

    private static List<ValueScreenerResult> DeserializeValues(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<ValueScreenerResult>>(json, Json) ?? []; }
        catch (JsonException) { return []; }
    }

    private static DateTime AsUtc(DateTime t) => t.Kind == DateTimeKind.Local ? t.ToUniversalTime() : DateTime.SpecifyKind(t, DateTimeKind.Utc);

    private static decimal? Pos(decimal v) => v > 0m ? v : null;

    private static string? NullIfBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>
    /// A CDR/wrapper analysed through its mapped underlying is classified by the underlying security; the wrapper listing's own
    /// (often "ETF") type must not defer scoring. Any other row keeps the verified listing type first.
    /// </summary>
    internal static string? ResolveInstrumentType(RsiScanResult? scan, string? listingType) =>
        scan?.UsesUnderlyingSecurity == true ? scan.InstrumentType : listingType ?? scan?.InstrumentType;

    private static string? FirstUsable(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v) && !string.Equals(v.Trim(), "ETFs & Funds", StringComparison.OrdinalIgnoreCase))?.Trim();

    private async Task<Dictionary<string, string>> ExistingListingNamesAsync(
        string userId, PortfolioSnapshot? portfolioSnapshot, CancellationToken ct)
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        List<PortfolioSummaryDto> holdings = [];
        try
        {
            holdings = JsonSerializer.Deserialize<List<PortfolioSummaryDto>>(portfolioSnapshot?.SnapshotJson ?? "[]", Json) ?? [];
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "[Opportunities] Could not read Portfolio names for user {UserId}", userId);
        }
        foreach (var holding in holdings
            .Where(h => !h.Item.IsManual && !IsClosedTransaction(h.Item.TransactionType))
            .OrderBy(h => h.Item.Id))
            AddListingName(names, holding.Item.Symbol, holding.Item.CompanyName);

        var watchlistSnapshot = await db.WatchlistSnapshots.AsNoTracking().SingleOrDefaultAsync(s => s.UserId == userId, ct);
        try
        {
            foreach (var item in JsonSerializer.Deserialize<List<WatchlistSummaryDto>>(watchlistSnapshot?.SnapshotJson ?? "[]", Json) ?? [])
            {
                if (item.Quote is null || !string.Equals(item.Quote.Symbol, item.Item.Symbol, StringComparison.OrdinalIgnoreCase))
                    continue;
                AddListingName(names, item.Item.Symbol, item.Quote.CompanyName);
            }
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "[Opportunities] Could not read Watchlist names for user {UserId}", userId);
        }

        var constituents = await db.IndexConstituents.AsNoTracking()
            .Where(c => c.IndexCode == IndexCode && c.EffectiveTo == null).ToListAsync(ct);
        foreach (var constituent in constituents.OrderBy(c => c.Symbol, StringComparer.Ordinal))
            AddListingName(names, constituent.Symbol, constituent.CompanyName);
        return names;
    }

    private static void AddListingName(IDictionary<string, string> names, string symbol, string? candidate)
    {
        var name = ListingName(symbol, [candidate]);
        if (name is not null) names.TryAdd(symbol.Trim(), name);
    }

    private static string? ListingName(string symbol, IEnumerable<string?> candidates) =>
        candidates.Select(name => name?.Trim()).FirstOrDefault(name =>
            !string.IsNullOrWhiteSpace(name)
            && !string.Equals(name, symbol.Trim(), StringComparison.OrdinalIgnoreCase)
            && name.ToUpperInvariant() is not ("N/A" or "NA" or "UNKNOWN" or "UNAVAILABLE" or "ETFS & FUNDS" or "-"));

    private static OpportunityRowDto ProjectListingName(
        OpportunityRowDto row, IReadOnlyDictionary<string, string> listingNames)
    {
        if (listingNames.TryGetValue(row.Symbol, out var name))
            return row with { CompanyName = name };
        return row.UsesUnderlying ? row with { CompanyName = row.Symbol } : row;
    }

    private static bool IsClosedTransaction(string? transactionType) =>
        string.Equals(transactionType, "CLOSE", StringComparison.OrdinalIgnoreCase);

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
