using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services.FinalAction;

namespace PortfolioManager.Api.Services;

public interface ISignalsAuditService
{
    Task<SignalsAuditSettingsDto> GetSettingsAsync(string userId, CancellationToken ct = default);
    Task<SignalsAuditSettingsDto> SaveSettingsAsync(string userId, SaveSignalsAuditSettingsRequest request, CancellationToken ct = default);
    /// <summary>Evaluates Priority Candidates and enabled Final Actions and records new / closed episodes. Idempotent.</summary>
    Task<SignalCaptureResultDto> CaptureAsync(string userId, CancellationToken ct = default);
    /// <summary>
    /// One authoritative EOD pass over an already computed Final Action snapshot. Only a complete EOD pass
    /// (<see cref="CaptureOptions.IsEodPass"/> and <see cref="CaptureOptions.EvaluationComplete"/>) may confirm, invalidate,
    /// supersede or expire; otherwise existing recommendations are preserved untouched.
    /// </summary>
    Task<SignalCaptureResultDto> CaptureAsync(string userId, FinalActionSnapshotDto snapshot, CaptureOptions options, CancellationToken ct = default);
    /// <summary>
    /// Records that the latest EOD evaluation failed or could not run, so the Action Center reports it as
    /// unavailable instead of implying an all-clear. Never changes any recommendation.
    /// </summary>
    Task RecordEvaluationFailureAsync(string userId, CancellationToken ct = default);
    /// <summary>User command: Active -> Dismissed. Independent of MyAction. Null when not found; ArgumentException when not Active.</summary>
    Task<SignalAuditRowDto?> DismissAsync(string userId, long id, string? note, CancellationToken ct = default);
    Task<IReadOnlyList<SignalTypeOptionDto>> GetSignalTypesAsync(string userId, CancellationToken ct = default);
    Task<SignalAuditPagedResponse> QueryAsync(string userId, SignalsAuditQuery query, CancellationToken ct = default);
    Task<SignalAuditSummaryDto> GetSummaryAsync(string userId, SignalsAuditQuery query, CancellationToken ct = default);
    Task<SignalAuditDetailDto?> GetDetailAsync(string userId, long id, CancellationToken ct = default);
    Task<IReadOnlyList<SignalTransactionCandidateDto>?> GetTransactionCandidatesAsync(string userId, long id, CancellationToken ct = default);
    /// <summary>Null when the signal does not exist; throws <see cref="ArgumentException"/> on invalid input.</summary>
    Task<SignalAuditRowDto?> SaveActionAsync(string userId, long id, SaveSignalActionRequest request, CancellationToken ct = default);
}

public sealed record SignalsAuditQuery(
    string? Ticker = null,
    string[]? SignalType = null,
    string? Source = null,
    string[]? Role = null,
    string? MyAction = null,
    string? From = null,
    string? To = null,
    string? SortBy = null,
    string? SortDir = null,
    int Page = 1,
    int PageSize = 25,
    string? Origin = null,
    bool IncludeBaselineInStats = false,
    bool IncludeUnaudited = false,
    string? Status = null);

/// <summary>Per-pass context for the lifecycle-aware capture.</summary>
public sealed record CaptureOptions(bool IsEodPass, bool EvaluationComplete = true, DateTime? RunStartedUtc = null);

public sealed class SignalsAuditService(
    AppDbContext db,
    IPortfolioActionScoreService scores,
    IFinalActionService finalActions,
    ISecurityAnalysisResolver analysisResolver,
    IMarketDataProvider marketData,
    IMemoryCache cache,
    IAuditService audit,
    ILogger<SignalsAuditService> logger) : ISignalsAuditService
{
    private const string PriorityRuleVersion = "priority-score-v2";
    private const string FinalActionRuleVersion = "final-action-v1";
    private const int MaxEvaluatedRows = 5000;
    private const int MaxPageSize = 200;
    private const int MaxNoteLength = 1000;
    private const int MaxCandidateWindowDays = 45;
    private const int MaxEntries = 200;
    private const int MaxLabelLength = 120;
    private const decimal BasisTolerance = 0.005m;
    private static readonly TimeSpan SeriesCacheTtl = TimeSpan.FromMinutes(15);
    // Serialises capture inside the process; the unique EpisodeKey index covers cross-process races.
    private static readonly SemaphoreSlim CaptureLock = new(1, 1);

    // ── Settings ──────────────────────────────────────────────────────────────

    public async Task<SignalsAuditSettingsDto> GetSettingsAsync(string userId, CancellationToken ct = default) =>
        ToDto(await LoadSettingsAsync(userId, ct) ?? new SignalsAuditSettings());

    public async Task<SignalsAuditSettingsDto> SaveSettingsAsync(
        string userId, SaveSignalsAuditSettingsRequest request, CancellationToken ct = default)
    {
        if (request.PriorityThreshold is < 0m or > 100m)
            throw new ArgumentException("Priority score threshold must be between 0 and 100.");

        var previous = await LoadSettingsAsync(userId, ct);
        var threshold = Math.Round(request.PriorityThreshold, 1);
        var activating = request.PriorityEnabled
            && (previous is null || !previous.PriorityEnabled || previous.PriorityThreshold != threshold);

        var priorityBaseline = request.PriorityEnabled ? previous?.BaselineSymbols ?? [] : [];
        if (activating)
        {
            // Symbols already qualifying right now must not become "new" signals just because of this change.
            var active = await ActiveSymbolsAsync(userId, SignalTypeCatalog.PriorityCandidateKey, ct);
            var current = await scores.GetScoresAsync(userId, ct);
            var eligibleSymbols = await EligibleSymbolsAsync(userId, current, fresh: false, ct);
            priorityBaseline = current
                .Where(s => s.TotalScore > threshold && eligibleSymbols.Contains(s.Symbol.ToUpperInvariant())
                            && !active.Contains(s.Symbol.ToUpperInvariant()))
                .Select(s => s.Symbol.ToUpperInvariant()).Distinct().ToList();
        }

        var entries = NormalizeEntries(request.Entries ?? ResolveEntries(previous));
        var faEnabled = request.FinalActionEnabled ?? previous?.FinalActionEnabled ?? true;

        var previousEnabled = ResolveEntries(previous).Where(e => e.Enabled).Select(e => e.Id).ToHashSet();
        var newlyEnabled = faEnabled
            ? entries.Where(e => e.Enabled && (!(previous?.FinalActionEnabled ?? true) || !previousEnabled.Contains(e.Id)))
                .Select(e => e.Id).ToHashSet()
            : [];

        var faBaseline = faEnabled ? previous?.FinalActionBaseline ?? [] : [];
        var baselineNeeded = faEnabled && previous is null;
        if (newlyEnabled.Count > 0 && previous is not null)
        {
            // Activation baseline: pairs the rules already produce for newly enabled actions do not become signals.
            try
            {
                var snapshot = await finalActions.ComputeAsync(userId, fresh: false, ct);
                var active = await ActiveFinalActionPairsAsync(userId, ct);
                faBaseline = faBaseline.Concat(
                        AllResults(snapshot).Where(r => newlyEnabled.Contains(r.FinalActionId))
                            .Select(r => Pair(r.Symbol, r.FinalActionId)).Where(p => !active.Contains(p)))
                    .Distinct().ToList();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "[SignalsAudit] Final Action baseline could not be computed now; it will be taken at the next capture.");
                baselineNeeded = true;
            }
        }

        var saved = new SignalsAuditSettings
        {
            PriorityEnabled = request.PriorityEnabled,
            PriorityThreshold = threshold,
            BaselineSymbols = priorityBaseline,
            ActivatedAtUtc = activating ? DateTime.UtcNow : previous?.ActivatedAtUtc,
            FinalActionEnabled = faEnabled,
            Entries = entries,
            FinalActionBaseline = faBaseline,
            FinalActionBaselineNeeded = baselineNeeded,
            InitialBaselineDone = previous?.InitialBaselineDone ?? false,
            LifecycleBaselineDone = previous?.LifecycleBaselineDone ?? false,
            LastEodEvaluationUtc = previous?.LastEodEvaluationUtc,
            LastEodEvaluationComplete = previous?.LastEodEvaluationComplete ?? false,
            LastCompleteEvaluationUtc = previous?.LastCompleteEvaluationUtc,
        };
        await SaveSettingsRowAsync(userId, saved, ct);

        await audit.LogAsync(AuditCategories.Config, AuditActions.Update,
            $"Signals Audit settings: priority {(saved.PriorityEnabled ? "on" : "off")} (> {saved.PriorityThreshold}), Final Actions {(saved.FinalActionEnabled ? "on" : "off")} ({entries.Count(e => e.Enabled)} of {entries.Count} enabled)",
            "SignalsAuditSettings", ct: ct);
        return ToDto(saved);
    }

    /// <summary>Resolves the persisted list, or the six business defaults when nothing was ever saved.</summary>
    private static List<AuditedActionEntry> ResolveEntries(SignalsAuditSettings? s) =>
        s?.Entries ?? FinalActionCatalog.DefaultAuditedLabels
            .Select(label =>
            {
                var id = FinalActionCatalog.CanonicalId(label);
                return new AuditedActionEntry(id, label, true, !FinalActionCatalog.IsKnown(id));
            }).ToList();

    private static List<AuditedActionEntry> NormalizeEntries(IReadOnlyList<AuditedActionEntry>? input)
    {
        if (input is null) return [];
        if (input.Count > MaxEntries) throw new ArgumentException($"At most {MaxEntries} audited actions are allowed.");

        var result = new List<AuditedActionEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in input)
        {
            var label = (e.Label ?? "").Trim();
            if (label.Length == 0) throw new ArgumentException("Every audited action needs a name.");
            if (label.Length > MaxLabelLength) throw new ArgumentException($"Action names cannot exceed {MaxLabelLength} characters.");
            // A rules-backed entry keeps its identity when only its label is edited; custom entries are identified by their text.
            var id = !e.Custom && FinalActionCatalog.IsKnown(e.Id ?? "")
                ? FinalActionCatalog.CanonicalId(e.Id!)
                : FinalActionCatalog.CanonicalId(label);
            if (!seen.Add(id)) throw new ArgumentException($"'{label}' is already in the list.");
            result.Add(new AuditedActionEntry(id, label, e.Enabled, !FinalActionCatalog.IsKnown(id)));
        }
        return result;
    }

    // ── Capture ───────────────────────────────────────────────────────────────

    public async Task RecordEvaluationFailureAsync(string userId, CancellationToken ct = default)
    {
        await CaptureLock.WaitAsync(ct);
        try
        {
            var settings = await LoadSettingsAsync(userId, ct) ?? new SignalsAuditSettings();
            await SaveSettingsRowAsync(userId, settings with
            {
                LastEodEvaluationUtc = DateTime.UtcNow,
                LastEodEvaluationComplete = false,
            }, ct);
        }
        finally
        {
            CaptureLock.Release();
        }
    }

    public Task<SignalCaptureResultDto> CaptureAsync(string userId, CancellationToken ct = default) =>
        CaptureCoreAsync(userId, null, new CaptureOptions(IsEodPass: false), ct);

    public Task<SignalCaptureResultDto> CaptureAsync(
        string userId, FinalActionSnapshotDto snapshot, CaptureOptions options, CancellationToken ct = default) =>
        CaptureCoreAsync(userId, snapshot, options, ct);

    private async Task<SignalCaptureResultDto> CaptureCoreAsync(
        string userId, FinalActionSnapshotDto? provided, CaptureOptions options, CancellationToken ct)
    {
        await CaptureLock.WaitAsync(ct);
        try
        {
            var settings = await LoadSettingsAsync(userId, ct);
            var firstRun = settings is null;
            var today = SnapDate(TorontoToday());
            var nowUtc = DateTime.UtcNow;

            var candidates = await scores.GetScoresAsync(userId, ct);
            FinalActionSnapshotDto snapshot;
            try
            {
                snapshot = provided ?? await finalActions.ComputeAsync(userId, fresh: false, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "[SignalsAudit] Final Action evaluation failed; capture skipped.");
                return new SignalCaptureResultDto(0, 0, true, "Final Action evaluation unavailable.");
            }

            var allResults = AllResults(snapshot);
            if (candidates.Count == 0 && allResults.Count == 0)
                return new SignalCaptureResultDto(0, 0, true, "No scored candidates or decisions available (no snapshot data).");

            // Only a complete EOD pass may confirm, invalidate or expire. Manual captures and incomplete passes
            // can create missing recommendations but never close or revalidate one.
            var meta = snapshot.Meta;
            var passOk = options.IsEodPass && options.EvaluationComplete && (meta?.PassComplete ?? true);
            var portfolioStale = meta is not null && options.RunStartedUtc is { } runStart
                && (meta.PortfolioSnapshotAtUtc ?? DateTime.MinValue) < runStart;
            bool SymbolUnknown(string symbol, string source) =>
                !passOk || (source == SignalSources.Portfolio && portfolioStale)
                || (meta?.UnknownSymbols.Contains(symbol.ToUpperInvariant()) ?? false);

            var eligible = EligibleSymbols(snapshot.Watchlist, candidates);
            // The first successful capture records what already qualifies as INITIAL_BASELINE events.
            var initializing = settings is null || !settings.InitialBaselineDone;
            var eventOrigin = initializing ? SignalOrigins.InitialBaseline : SignalOrigins.Live;
            settings ??= new SignalsAuditSettings { ActivatedAtUtc = DateTime.UtcNow };

            var events = await db.SignalAuditEvents.Where(e => e.UserId == userId).ToListAsync(ct);
            var created = 0;
            var closed = 0;

            // ── Priority Candidates ──────────────────────────────────────────
            var activePc = events.Where(e => e.SignalType == SignalTypeCatalog.PriorityCandidateKey && e.EpisodeEndedDate is null)
                .ToDictionary(e => e.Symbol.ToUpperInvariant());
            // Disabled policy: capture stops and open episodes close; history stays untouched.
            var qualifying = settings.PriorityEnabled
                ? candidates.Where(s => s.TotalScore > settings.PriorityThreshold && s.CurrentPrice > 0m
                                        && eligible.Contains(s.Symbol.ToUpperInvariant()))
                    .GroupBy(s => s.Symbol.ToUpperInvariant()).ToDictionary(g => g.Key, g => g.First())
                : new Dictionary<string, ActionScoreDto>();

            foreach (var (key, ev) in activePc)
            {
                if (qualifying.ContainsKey(key))
                {
                    if (passOk && !SymbolUnknown(ev.Symbol, ev.Source) && ev.Status == SignalStatuses.Active) ev.LastValidatedAtUtc = nowUtc;
                    continue;
                }
                if (SymbolUnknown(ev.Symbol, ev.Source)) continue;
                CloseEpisode(ev, SignalStatuses.Invalidated, "Priority condition no longer holds", today, nowUtc);
                closed++;
            }

            var pcBaseline = initializing
                ? []
                : settings.BaselineSymbols.Select(s => s.ToUpperInvariant()).Where(qualifying.ContainsKey).ToHashSet();
            var facts = await LoadTechnicalFactsAsync(userId, ct);
            foreach (var (symbol, score) in qualifying)
            {
                if (activePc.ContainsKey(symbol) || pcBaseline.Contains(symbol)) continue;
                var fa = snapshot.Watchlist.FirstOrDefault(r => r.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
                var ev = await NewEventAsync(userId, events, SignalTypeCatalog.PriorityCandidate, score.Symbol, today,
                    score.CurrentPrice, score.AnalysisCurrency, score.HoldingRole, SignalSources.Watchlist,
                    score.TotalScore, PriorityRuleVersion, BuildPriorityContext(score, facts.GetValueOrDefault(symbol), fa), ct,
                    origin: eventOrigin);
                db.SignalAuditEvents.Add(ev);
                events.Add(ev);
                created++;
            }

            var prunedPcBaseline = pcBaseline.OrderBy(s => s).ToList();

            // ── Final Actions ────────────────────────────────────────────────
            // Every actionable (Entry / TrimExit) recommendation is recorded; the audited list only decides whether
            // it also counts in performance analytics (PerformanceAudited).
            var entries = ResolveEntries(settings);
            var enabledIds = settings.FinalActionEnabled
                ? entries.Where(e => e.Enabled).Select(e => e.Id).ToHashSet()
                : [];
            var activeFa = events.Where(e => e.SignalType.StartsWith(SignalTypeCatalog.FinalActionPrefix, StringComparison.Ordinal)
                                             && e.EpisodeEndedDate is null).ToList();

            // Identity of a recommendation = symbol + action + source (a holding and a watchlist entry are distinct contexts).
            var priced = allResults.Where(r => r.AnalysisPrice > 0m).ToList();
            var bySrcPair = priced.GroupBy(r => SrcKey(r.Symbol, r.FinalActionId, r.Source)).ToDictionary(g => g.Key, g => g.First());
            var symbolsWithDecision = priced.Select(r => r.Symbol.ToUpperInvariant()).ToHashSet();
            var currentSymbols = await CurrentSymbolsAsync(userId, ct);

            var produced = priced.Where(r => enabledIds.Contains(r.FinalActionId))
                .Select(r => Pair(r.Symbol, r.FinalActionId)).ToHashSet();
            var activePairs = activeFa.Select(e => Pair(e.Symbol, FaId(e))).ToHashSet();
            HashSet<string> faBaseline = initializing
                ? []
                : settings.FinalActionBaselineNeeded && settings.FinalActionEnabled
                    ? produced.Where(p => !activePairs.Contains(p)).ToHashSet()
                    : settings.FinalActionBaseline.Where(produced.Contains).ToHashSet();

            var openKeys = activeFa.Select(e => SrcKey(e.Symbol, FaId(e), e.Source)).ToHashSet();
            var replacements = new Dictionary<string, SignalAuditEvent>();
            foreach (var r in bySrcPair.Values)
            {
                if (openKeys.Contains(SrcKey(r.Symbol, r.FinalActionId, r.Source))) continue;
                // Never create from data the evaluation could not vouch for.
                if ((meta?.UnknownSymbols.Contains(r.Symbol.ToUpperInvariant()) ?? false)
                    || (r.Source == SignalSources.Portfolio && portfolioStale)) continue;

                var audited = enabledIds.Contains(r.FinalActionId);
                var actionable = DecisionEligibility.IsActionable(r);
                var pair = Pair(r.Symbol, r.FinalActionId);
                var suppressed = audited && faBaseline.Contains(pair);
                var asAudited = audited && !suppressed;
                if (!asAudited && !actionable) continue;

                var origin = asAudited
                    ? eventOrigin
                    : initializing || !settings.LifecycleBaselineDone || suppressed ? SignalOrigins.InitialBaseline : SignalOrigins.Live;
                var def = FinalActionCatalog.Describe(r.FinalActionId);
                var type = new SignalTypeDefinition(SignalTypeCatalog.FinalActionKey(r.FinalActionId), def.Label, def.Category, def.Direction);
                var ev = await NewEventAsync(userId, events, type, r.Symbol, today, r.AnalysisPrice, r.AnalysisCurrency, r.Role,
                    r.Source, null, FinalActionRuleVersion, BuildFinalActionContext(r), ct, r.AnalysisTicker, origin);
                ev.PerformanceAudited = asAudited;
                if (passOk) ev.LastValidatedAtUtc = nowUtc;
                db.SignalAuditEvents.Add(ev);
                events.Add(ev);
                replacements[SymSrc(r.Symbol, r.Source)] = ev;
                created++;
            }

            foreach (var ev in activeFa)
            {
                var symbol = ev.Symbol.ToUpperInvariant();
                var unknown = SymbolUnknown(symbol, ev.Source);

                if (bySrcPair.ContainsKey(SrcKey(symbol, FaId(ev), ev.Source)))
                {
                    // The same recommendation continues: revalidate it, never re-create it.
                    if (!unknown && ev.Status == SignalStatuses.Active) ev.LastValidatedAtUtc = nowUtc;
                    continue;
                }

                if (unknown)
                {
                    logger.LogWarning("[SignalsAudit] {Symbol} ({Source}) {Action} left unconfirmed: symbol not evaluated in this pass (passOk={PassOk}, listedUnknown={Listed}, portfolioStale={Stale}).",
                        symbol, ev.Source, FaId(ev), passOk, meta?.UnknownSymbols.Contains(symbol) ?? false, portfolioStale);
                    // Failed or incomplete evaluation: keep the state and let the row show as stale. Only a complete pass
                    // in which this one security stayed unevaluated past its window may expire it (risk reviews never do).
                    if (passOk && !(ev.Source == SignalSources.Portfolio && portfolioStale) && ev.Status == SignalStatuses.Active
                        && DecisionEligibility.Classify(FaId(ev), ev.Source, 1m) != DecisionBucket.RiskReview
                        && !IsLongHorizonHoldingExit(ev)
                        && TradingCalendar.TradingDaysBetween(
                               TradingCalendar.TorontoDate(ev.LastValidatedAtUtc ?? ev.CapturedAtUtc), today, TradingCalendar.ExchangeFor(ev.Symbol))
                           > RecommendationLifecycle.ExpiryWindowDays(ev.SignalCategory))
                    {
                        CloseEpisode(ev, SignalStatuses.Expired, "No successful revalidation within the window", today, nowUtc);
                        closed++;
                    }
                    continue;
                }

                // A symbol with no decision in this recommendation's own context (holding vs watchlist) is left open
                // unless it left the watchlist and portfolio; a decision from the other context proves nothing.
                var noDecision = !symbolsWithDecision.Contains(SymSrc(symbol, ev.Source));
                if (noDecision && currentSymbols.Contains(symbol))
                {
                    logger.LogWarning("[SignalsAudit] {Symbol} ({Source}) {Action} left unconfirmed: the pass produced no priced decision for this symbol.",
                        symbol, ev.Source, FaId(ev));
                    continue;
                }

                if (replacements.TryGetValue(SymSrc(symbol, ev.Source), out var newer))
                    CloseEpisode(ev, SignalStatuses.Superseded, "Replaced by a newer recommendation", today, nowUtc, newer);
                else if (OriginatingRiskPersists(ev, priced.FirstOrDefault(r =>
                             string.Equals(r.Symbol, symbol, StringComparison.OrdinalIgnoreCase) && r.Source == ev.Source)))
                {
                    // A successful evaluation alone does not resolve a long-horizon risk event: the condition that raised
                    // it (e.g. a confirmed structural breakdown) must actually be gone. Stay open, uncounted if not actionable.
                    logger.LogInformation("[SignalsAudit] {Symbol} ({Source}) {Action} stays open: the originating risk condition persists.",
                        symbol, ev.Source, FaId(ev));
                    continue;
                }
                else
                    CloseEpisode(ev, SignalStatuses.Invalidated, "Condition no longer produced by a complete evaluation", today, nowUtc);
                closed++;
            }

            var prunedFaBaseline = faBaseline.OrderBy(p => p).ToList();
            var settingsChanged = firstRun || initializing || options.IsEodPass
                || !settings.LifecycleBaselineDone
                || !settings.BaselineSymbols.OrderBy(s => s).SequenceEqual(prunedPcBaseline)
                || settings.FinalActionBaselineNeeded
                || !settings.FinalActionBaseline.OrderBy(p => p).SequenceEqual(prunedFaBaseline);
            if (settingsChanged)
                await StageSettingsRowAsync(userId, settings with
                {
                    BaselineSymbols = prunedPcBaseline,
                    FinalActionBaseline = prunedFaBaseline,
                    FinalActionBaselineNeeded = false,
                    InitialBaselineDone = true,
                    LifecycleBaselineDone = true,
                    LastEodEvaluationUtc = options.IsEodPass ? nowUtc : settings.LastEodEvaluationUtc,
                    LastEodEvaluationComplete = options.IsEodPass ? passOk && !portfolioStale : settings.LastEodEvaluationComplete,
                    LastCompleteEvaluationUtc = options.IsEodPass && passOk && !portfolioStale ? nowUtc : settings.LastCompleteEvaluationUtc,
                    Entries = settings.Entries ?? entries,
                }, ct);

            try
            {
                // Events, closed episodes and the pruned baselines commit atomically.
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex)
            {
                // A concurrent run already wrote the same episode; the next run reconciles.
                // Detach only this service's entities: the shared scoped context also tracks EOD run logs.
                foreach (var entry in db.ChangeTracker.Entries()
                             .Where(x => x.Entity is SignalAuditEvent or UserPreference).ToList())
                    entry.State = EntityState.Detached;
                logger.LogWarning(ex, "[SignalsAudit] Capture hit a duplicate episode key; treating as already captured.");
                return new SignalCaptureResultDto(0, 0, true, "Duplicate episode suppressed.");
            }

            if (created > 0 || closed > 0)
                await audit.LogAsync(AuditCategories.Signals, AuditActions.Other,
                    $"Signals Audit capture: {created} new, {closed} closed",
                    "SignalAuditEvent", ct: ct);
            return new SignalCaptureResultDto(created, closed, false, null);
        }
        finally
        {
            CaptureLock.Release();
        }
    }

    private async Task<SignalAuditEvent> NewEventAsync(
        string userId, List<SignalAuditEvent> existing, SignalTypeDefinition type, string symbol, DateOnly date,
        decimal price, string? currency, string? role, string source, decimal? score, string ruleVersion,
        string context, CancellationToken ct, string? analysisSymbol = null, string origin = SignalOrigins.Live)
    {
        var resolvedSymbol = analysisSymbol ?? (await analysisResolver.ResolveAsync(symbol, userId, ct)).AnalysisTicker;
        var series = (await LoadSeriesAsync([resolvedSymbol], ct)).GetValueOrDefault(resolvedSymbol.ToUpperInvariant());
        // The provider's own close for the signal date is the reference for later basis (split) checks.
        decimal? basisClose = series?.FirstOrDefault(c => c.Date == date)?.Close;

        var sequence = existing.Count(e => e.SignalType == type.Key
            && e.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase) && e.SignalDate == date) + 1;
        return new SignalAuditEvent
        {
            UserId = userId,
            Symbol = symbol,
            AnalysisSymbol = resolvedSymbol,
            Currency = currency,
            SignalType = type.Key,
            SignalCategory = type.Category,
            Direction = type.Direction,
            SignalDate = date,
            SignalPrice = price,
            Score = score,
            Role = role,
            Source = source,
            Origin = origin,
            RuleVersion = ruleVersion,
            EpisodeKey = $"{type.Key.ToUpperInvariant()}|{symbol.ToUpperInvariant()}|{date:yyyyMMdd}|{sequence}",
            ContextJson = context,
            EligibilityStatus = SignalEligibility.Verified,
            BasisClose = basisClose,
        };
    }

    // ── Eligibility (same rules as the Action Center / Priority Candidates widget) ──

    private static bool IsPriorityEligible(FinalActionResultDto? r, decimal totalScore) =>
        PriorityCandidateEligibility.Resolve(r, totalScore).Eligible;

    private static HashSet<string> EligibleSymbols(IReadOnlyList<FinalActionResultDto> watchlist, IReadOnlyList<ActionScoreDto> candidates)
    {
        var byTicker = watchlist.GroupBy(r => r.Symbol.ToUpperInvariant()).ToDictionary(g => g.Key, g => g.First());
        return candidates
            .Where(c => IsPriorityEligible(byTicker.GetValueOrDefault(c.Symbol.ToUpperInvariant()), c.TotalScore))
            .Select(c => c.Symbol.ToUpperInvariant()).ToHashSet();
    }

    private async Task<HashSet<string>> EligibleSymbolsAsync(string userId, IReadOnlyList<ActionScoreDto> candidates, bool fresh, CancellationToken ct)
    {
        try
        {
            var snapshot = await finalActions.ComputeAsync(userId, fresh, ct);
            return EligibleSymbols(snapshot.Watchlist, candidates);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "[SignalsAudit] Eligibility could not be evaluated; treating no candidate as eligible.");
            return [];
        }
    }

    private static List<FinalActionResultDto> AllResults(FinalActionSnapshotDto s) => [.. s.Watchlist, .. s.Portfolio];

    private static string Pair(string symbol, string actionId) => $"{symbol.ToUpperInvariant()}|{actionId}";
    private static string SrcKey(string symbol, string actionId, string source) => $"{symbol.ToUpperInvariant()}|{actionId}|{source}";
    private static string SymSrc(string symbol, string source) => $"{symbol.ToUpperInvariant()}|{source}";
    private static string FaId(SignalAuditEvent e) => e.SignalType[SignalTypeCatalog.FinalActionPrefix.Length..];

    /// <summary>
    /// A Strategic/Core (long-horizon) holding's exit recommendation is never expired by a calendar window alone: the
    /// short Trim/Exit window is meant for short-horizon trades. It still closes when a complete evaluation no longer produces it.
    /// </summary>
    private static bool IsLongHorizonHoldingExit(SignalAuditEvent e) =>
        e.Source == SignalSources.Portfolio && e.SignalCategory == SignalCategories.TrimExit
        && !PortfolioEvidenceGate.IsShortHorizon(e.Role);

    /// <summary>
    /// For a Strategic/Core (long-horizon) exit or any holding Risk Review raised by a confirmed structural breakdown
    /// (recorded in the event's reason codes), the breakdown still being present in a successful evaluation means the
    /// risk is unresolved. Events without recorded reason codes keep the existing behaviour.
    /// </summary>
    private static bool OriginatingRiskPersists(SignalAuditEvent ev, FinalActionResultDto? current)
    {
        if (current is null || ev.Source != SignalSources.Portfolio) return false;
        var isRisk = DecisionEligibility.Classify(FaId(ev), ev.Source, 1m) == DecisionBucket.RiskReview;
        if (!isRisk && !IsLongHorizonHoldingExit(ev)) return false;
        if (string.IsNullOrWhiteSpace(ev.ContextJson)) return false;
        try
        {
            using var doc = JsonDocument.Parse(ev.ContextJson);
            if (!doc.RootElement.TryGetProperty("reasonCodes", out var codes) || codes.ValueKind != JsonValueKind.Array) return false;
            var raised = codes.EnumerateArray().Select(c => c.GetString()).Contains(GateReasons.StructuralBreakdown);
            return raised && current.Technicals?.PriceStructure.HasHardStructuralNegative == true;
        }
        catch (JsonException) { return false; }
    }

    private static void CloseEpisode(SignalAuditEvent ev, string status, string reason, DateOnly today, DateTime nowUtc, SignalAuditEvent? by = null)
    {
        // Acted / Dismissed keep their status; the episode simply ends.
        if (!RecommendationLifecycle.TryTransition(ev, status, reason, today, nowUtc, by)) ev.EpisodeEndedDate ??= today;
    }

    private async Task<HashSet<string>> CurrentSymbolsAsync(string userId, CancellationToken ct)
    {
        var watch = await db.WatchlistItems.AsNoTracking().Where(w => w.UserId == userId || w.UserId == null)
            .Select(w => w.Symbol).ToListAsync(ct);
        var held = await db.PortfolioItems.AsNoTracking()
            .Where(p => (p.UserId == userId || p.UserId == null) && p.TransactionType != "CLOSE").Select(p => p.Symbol).ToListAsync(ct);
        return watch.Concat(held).Select(s => s.ToUpperInvariant()).ToHashSet();
    }

    private async Task<HashSet<string>> ActiveFinalActionPairsAsync(string userId, CancellationToken ct) =>
        (await db.SignalAuditEvents.AsNoTracking()
            .Where(e => e.UserId == userId && e.SignalType.StartsWith(SignalTypeCatalog.FinalActionPrefix) && e.EpisodeEndedDate == null)
            .Select(e => new { e.Symbol, e.SignalType }).ToListAsync(ct))
        .Select(e => Pair(e.Symbol, e.SignalType[SignalTypeCatalog.FinalActionPrefix.Length..])).ToHashSet();

    private static string BuildPriorityContext(ActionScoreDto score, SharedTechnicalFacts? facts, FinalActionResultDto? fa) =>
        JsonSerializer.Serialize(new
        {
            score = new
            {
                total = score.TotalScore,
                portfolioNeed = score.PortfolioNeedScore,
                technical = score.TechnicalScore,
                fundamental = score.FundamentalScore,
                risk = score.RiskScore,
                badge = score.Badge,
            },
            trendShift = score.TrendShift,
            rsi = score.Rsi,
            allocationStatus = score.AllocationStatus,
            watchlistTier = score.WatchlistTier,
            eodSignalState = score.LatestEodSignalState,
            eodScanType = score.LatestEodScanType,
            finalAction = fa?.FinalAction,
            finalActionReason = fa?.Reason,
            technicalFacts = facts,
        }, SignalsAuditSettings.Json);

    private static string BuildFinalActionContext(FinalActionResultDto r) =>
        JsonSerializer.Serialize(new
        {
            finalAction = r.FinalAction,
            finalActionId = r.FinalActionId,
            finalActionReason = r.Reason,
            trendShift = r.MomentumShift,
            trendSetup = r.TrendSetup,
            baseAction = r.BaseAction,
            gapStatus = r.GapStatus,
            severity = r.Severity,
            priority = r.Priority,
            hasHardStructuralNegative = r.HasHardStructuralNegative,
            hover = r.HoverDescription,
            reasonCodes = r.ReasonCodes,
            evidenceAsOfUtc = r.EvidenceAsOfUtc,
            entryDate = r.EntryDate,
            isActionable = r.IsActionable,
            rsi = r.Technicals?.Rsi,
            technicalFacts = r.Technicals is null ? null : new
            {
                maStructure = r.Technicals.MaStructure,
                maCrossState = r.Technicals.MaCrossState,
                priceStructure = r.Technicals.PriceStructure,
            },
            fibonacci = r.Technicals is null ? null : new { zone = r.Technicals.FibZone, status = r.Technicals.FibStatus, level618 = r.Technicals.Fib61_8 },
            structuralStop = r.Technicals?.DynamicStopLoss,
            diagnostics = r.Diagnostics,
        }, SignalsAuditSettings.Json);

    private async Task<Dictionary<string, SharedTechnicalFacts>> LoadTechnicalFactsAsync(string userId, CancellationToken ct)
    {
        var snap = await db.WatchlistSnapshots.AsNoTracking().SingleOrDefaultAsync(s => s.UserId == userId, ct);
        if (snap is null) return [];
        try
        {
            var items = JsonSerializer.Deserialize<List<WatchlistSummaryDto>>(snap.SnapshotJson, SignalsAuditSettings.Json) ?? [];
            return items.Where(w => w.TechnicalFacts is not null)
                .GroupBy(w => w.Item.Symbol.ToUpperInvariant())
                .ToDictionary(g => g.Key, g => g.First().TechnicalFacts!);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private async Task<HashSet<string>> ActiveSymbolsAsync(string userId, string signalType, CancellationToken ct) =>
        (await db.SignalAuditEvents.AsNoTracking()
            .Where(e => e.UserId == userId && e.SignalType == signalType && e.EpisodeEndedDate == null)
            .Select(e => e.Symbol).ToListAsync(ct))
        .Select(s => s.ToUpperInvariant()).ToHashSet();

    // ── Query ─────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<SignalTypeOptionDto>> GetSignalTypesAsync(string userId, CancellationToken ct = default)
    {
        var historical = await db.SignalAuditEvents.AsNoTracking()
            .Where(e => e.UserId == userId).Select(e => e.SignalType).Distinct().ToListAsync(ct);
        var configured = ResolveEntries(await LoadSettingsAsync(userId, ct)).Select(e => SignalTypeCatalog.FinalActionKey(e.Id));
        var keys = SignalTypeCatalog.All.Select(d => d.Key).Union(configured).Union(historical, StringComparer.Ordinal);
        return keys.Select(k =>
        {
            var def = SignalTypeCatalog.Find(k);
            return new SignalTypeOptionDto(k, def?.Label ?? k, def?.Category ?? SignalCategories.Advisory,
                def?.Direction ?? SignalDirections.Neutral);
        }).OrderBy(o => o.Label).ToList();
    }

    public async Task<SignalAuditPagedResponse> QueryAsync(string userId, SignalsAuditQuery query, CancellationToken ct = default)
    {
        var rows = await BuildRowsAsync(userId, query, ct);
        rows = Sort(rows, query.SortBy, query.SortDir);

        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        var page = Math.Max(1, query.Page);
        return new SignalAuditPagedResponse(
            rows.Skip((page - 1) * pageSize).Take(pageSize).ToList(), rows.Count, page, pageSize);
    }

    public async Task<SignalAuditSummaryDto> GetSummaryAsync(string userId, SignalsAuditQuery query, CancellationToken ct = default)
    {
        var rows = await BuildRowsAsync(userId, query, ct);

        // Initial-baseline records are not genuine transitions, so they stay out of effectiveness unless asked for.
        var statRows = (query.IncludeBaselineInStats || string.Equals(query.Origin, SignalOrigins.InitialBaseline, StringComparison.OrdinalIgnoreCase)
            ? rows
            : rows.Where(r => r.Origin != SignalOrigins.InitialBaseline)).Where(r => r.PerformanceAudited).ToList();
        var effectiveness = statRows.GroupBy(r => r.SignalType).Select(g =>
        {
            var def = SignalTypeCatalog.Find(g.Key);
            var direction = def?.Direction ?? SignalDirections.Neutral;
            var c5 = g.Where(r => r.Return5D.Pct is not null).Select(r => r.Return5D.Pct!.Value).ToList();
            var c20 = g.Where(r => r.Return20D.Pct is not null).Select(r => r.Return20D.Pct!.Value).ToList();
            var c1m = g.Where(r => r.Return1M.Pct is not null).Select(r => r.Return1M.Pct!.Value).ToList();
            // Favourable outcome is judged on the 20D horizon, and only where a direction is defined.
            var favourable = direction == SignalDirections.Neutral ? null
                : (int?)c20.Count(p => direction == SignalDirections.Buy ? p > 0m : p < 0m);
            return new SignalEffectivenessRowDto(
                g.Key, def?.Label ?? g.Key, direction, g.Count(),
                c5.Count, Avg(c5), c20.Count, Avg(c20), c1m.Count, Avg(c1m),
                favourable is null ? 0 : c20.Count,
                favourable is null || c20.Count == 0 ? null : Math.Round(favourable.Value * 100m / c20.Count, 1),
                g.Count(r => SignalMyActions.IsFollowed(r.MyAction)));
        }).OrderBy(r => r.Label).ToList();

        // The category and review cards describe what is open now; Total and Actioned describe the rows in view.
        var open = rows.Where(r => r.Status == SignalStatuses.Active).ToList();
        return new SignalAuditSummaryDto(
            rows.Count,
            open.Count(r => r.SignalType == SignalTypeCatalog.PriorityCandidateKey),
            open.Count(r => r.Category == SignalCategories.Entry),
            open.Count(r => r.Category == SignalCategories.TrimExit),
            rows.Count(r => SignalMyActions.IsFollowed(r.MyAction)),
            open.Count(r => r.MyAction == SignalMyActions.Pending),
            effectiveness);
    }

    public async Task<SignalAuditDetailDto?> GetDetailAsync(string userId, long id, CancellationToken ct = default)
    {
        var ev = await db.SignalAuditEvents.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, ct);
        if (ev is null) return null;

        var row = (await ToRowsAsync(userId, [ev], ct))[0];
        SignalLinkedTransactionDto? linked = null;
        if (ev.LinkedTransactionId is int tid)
        {
            var tx = await db.PortfolioItems.AsNoTracking().FirstOrDefaultAsync(p => p.Id == tid, ct);
            if (tx is not null) linked = ToLinkedDto(tx);
        }

        JsonElement? context = null;
        if (!string.IsNullOrWhiteSpace(ev.ContextJson))
        {
            try { context = JsonDocument.Parse(ev.ContextJson).RootElement.Clone(); }
            catch (JsonException) { /* stored context unreadable: show as unavailable */ }
        }

        return new SignalAuditDetailDto(row, ev.CapturedAtUtc, ev.RuleVersion, ev.MyActionNote,
            ev.MyActionUpdatedUtc, ev.EpisodeEndedDate, context, linked);
    }

    // ── My Action / transaction link ──────────────────────────────────────────

    public async Task<IReadOnlyList<SignalTransactionCandidateDto>?> GetTransactionCandidatesAsync(
        string userId, long id, CancellationToken ct = default)
    {
        var ev = await db.SignalAuditEvents.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, ct);
        if (ev is null) return null;

        var wantType = ev.Direction == SignalDirections.Sell ? "CLOSE" : "OPEN";
        var from = ev.SignalDate.AddDays(-1).ToDateTime(TimeOnly.MinValue);
        var to = ev.SignalDate.AddDays(MaxCandidateWindowDays).ToDateTime(TimeOnly.MaxValue);
        var symbols = new[] { ev.Symbol.ToUpper(), ev.AnalysisSymbol.ToUpper() };

        var txs = await db.PortfolioItems.AsNoTracking()
            .Where(p => (p.UserId == userId || p.UserId == null)
                        && !p.IsManual
                        && p.TransactionType == wantType
                        && symbols.Contains(p.Symbol.ToUpper()))
            .ToListAsync(ct);

        return txs
            .Select(p => (Tx: p, Date: wantType == "CLOSE" ? p.CloseDate : p.OpenDate))
            .Where(x => x.Date is not null && x.Date >= from && x.Date <= to)
            .OrderBy(x => x.Date)
            .Select(x => new SignalTransactionCandidateDto(
                x.Tx.Id, x.Tx.Symbol, x.Tx.TransactionType, x.Tx.AccountType,
                x.Date!.Value.ToString("yyyy-MM-dd"), x.Tx.Shares,
                wantType == "CLOSE" ? x.Tx.ClosingPrice ?? x.Tx.AverageCostBasis : x.Tx.AverageCostBasis,
                wantType == "CLOSE" ? x.Tx.DecisionSourceClosed : x.Tx.DecisionSource,
                DateOnly.FromDateTime(x.Date.Value).DayNumber - ev.SignalDate.DayNumber,
                ev.LinkedTransactionId == x.Tx.Id))
            .ToList();
    }

    public async Task<SignalAuditRowDto?> SaveActionAsync(
        string userId, long id, SaveSignalActionRequest request, CancellationToken ct = default)
    {
        if (!SignalMyActions.IsValid(request.MyAction))
            throw new ArgumentException($"My Action must be one of: {string.Join(", ", SignalMyActions.All)}.");
        if (request.Note is { Length: > MaxNoteLength })
            throw new ArgumentException($"Note cannot exceed {MaxNoteLength} characters.");

        var ev = await db.SignalAuditEvents.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, ct);
        if (ev is null) return null;

        int? link = null;
        if (SignalMyActions.IsFollowed(request.MyAction) && request.LinkedTransactionId is int txId)
        {
            var candidates = await GetTransactionCandidatesAsync(userId, id, ct) ?? [];
            if (candidates.All(c => c.Id != txId))
                throw new ArgumentException("The selected transaction does not match this signal.");
            link = txId;
        }

        var before = $"{ev.MyAction}; tx {ev.LinkedTransactionId?.ToString() ?? "-"}";
        ev.MyAction = request.MyAction;
        ev.MyActionNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        ev.LinkedTransactionId = link;
        ev.MyActionUpdatedUtc = DateTime.UtcNow;
        // Only a full "Actioned" resolves an open recommendation; Partial / Not Actioned / Pending leave it Active.
        if (request.MyAction == SignalMyActions.Actioned)
            RecommendationLifecycle.TryTransition(ev, SignalStatuses.Acted, "Actioned by investor", TorontoToday(), DateTime.UtcNow);
        await db.SaveChangesAsync(ct);

        await audit.LogAsync(AuditCategories.Signals, AuditActions.Update,
            $"Signal {ev.Symbol} {ev.SignalDate:yyyy-MM-dd}: My Action {before} → {ev.MyAction}; tx {link?.ToString() ?? "-"}",
            "SignalAuditEvent", ev.Symbol, ct);

        return (await ToRowsAsync(userId, [ev], ct))[0];
    }

    public async Task<SignalAuditRowDto?> DismissAsync(string userId, long id, string? note, CancellationToken ct = default)
    {
        if (note is { Length: > MaxNoteLength }) throw new ArgumentException($"Note cannot exceed {MaxNoteLength} characters.");
        var ev = await db.SignalAuditEvents.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, ct);
        if (ev is null) return null;
        if (!RecommendationLifecycle.TryTransition(ev, SignalStatuses.Dismissed, string.IsNullOrWhiteSpace(note) ? "Dismissed by investor" : note.Trim(),
                TorontoToday(), DateTime.UtcNow))
            throw new ArgumentException($"Only an Active recommendation can be dismissed (current status: {ev.Status}).");
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(AuditCategories.Signals, AuditActions.Update,
            $"Signal {ev.Symbol} {ev.SignalDate:yyyy-MM-dd}: dismissed", "SignalAuditEvent", ev.Symbol, ct);
        return (await ToRowsAsync(userId, [ev], ct))[0];
    }

    // ── Row building ──────────────────────────────────────────────────────────

    private async Task<List<SignalAuditRowDto>> BuildRowsAsync(string userId, SignalsAuditQuery q, CancellationToken ct)
    {
        var query = db.SignalAuditEvents.AsNoTracking().Where(e => e.UserId == userId);

        // Unaudited recommendations stay out of the history unless asked for, but an OPEN one is a live decision
        // (it is in the Action Center), so it is always listed. They never feed performance analytics.
        if (!q.IncludeUnaudited) query = query.Where(e => e.PerformanceAudited || e.Status == SignalStatuses.Active);
        if (string.Equals(q.Status, "Open", StringComparison.OrdinalIgnoreCase))
            query = query.Where(e => e.Status == SignalStatuses.Active);
        else if (string.Equals(q.Status, "Closed", StringComparison.OrdinalIgnoreCase))
            query = query.Where(e => e.Status != SignalStatuses.Active);
        else if (SignalStatuses.IsValid(q.Status))
            query = query.Where(e => e.Status == q.Status);
        if (!string.IsNullOrWhiteSpace(q.Ticker))
        {
            var t = q.Ticker.Trim().ToUpper();
            query = query.Where(e => e.Symbol.ToUpper().Contains(t));
        }
        if (q.SignalType is { Length: > 0 }) query = query.Where(e => q.SignalType.Contains(e.SignalType));
        if (!string.IsNullOrWhiteSpace(q.Source) && !q.Source.Equals("All", StringComparison.OrdinalIgnoreCase))
            query = query.Where(e => e.Source == q.Source);
        if (q.Role is { Length: > 0 }) query = query.Where(e => e.Role != null && q.Role.Contains(e.Role));
        if (!string.IsNullOrWhiteSpace(q.MyAction) && !q.MyAction.Equals("All", StringComparison.OrdinalIgnoreCase))
            query = query.Where(e => e.MyAction == q.MyAction);
        if (string.Equals(q.Origin, "Live", StringComparison.OrdinalIgnoreCase))
            query = query.Where(e => e.Origin != SignalOrigins.InitialBaseline);
        else if (string.Equals(q.Origin, SignalOrigins.InitialBaseline, StringComparison.OrdinalIgnoreCase))
            query = query.Where(e => e.Origin == SignalOrigins.InitialBaseline);
        if (DateOnly.TryParse(q.From, out var from)) query = query.Where(e => e.SignalDate >= from);
        if (DateOnly.TryParse(q.To, out var to)) query = query.Where(e => e.SignalDate <= to);

        var events = await query.OrderByDescending(e => e.SignalDate).ThenByDescending(e => e.Id)
            .Take(MaxEvaluatedRows).ToListAsync(ct);
        return await ToRowsAsync(userId, events, ct);
    }

    private async Task<List<SignalAuditRowDto>> ToRowsAsync(string userId, IReadOnlyList<SignalAuditEvent> events, CancellationToken ct)
    {
        var series = await LoadSeriesAsync(events.Select(e => e.AnalysisSymbol).Distinct(StringComparer.OrdinalIgnoreCase).ToList(), ct);
        var today = TorontoToday();

        // Effective current state per Final Action recommendation: read-only, cache-only (never scans, publishes or touches a timestamp).
        var nowUtc = DateTime.UtcNow;
        var evalStatus = events.Any(e => e.SignalType.StartsWith(SignalTypeCatalog.FinalActionPrefix, StringComparison.Ordinal))
            ? await EvaluationStatusReader.ReadAsync(db, userId, nowUtc, ct)
            : EvaluationStatus.Unknown;
        var cachedSnapshot = finalActions.TryGetCached(userId);

        var linkIds = events.Where(e => e.LinkedTransactionId is not null).Select(e => e.LinkedTransactionId!.Value).Distinct().ToList();
        var linkedTx = linkIds.Count == 0
            ? new Dictionary<int, PortfolioItem>()
            : await db.PortfolioItems.AsNoTracking().Where(p => linkIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);

        return events.Select(e =>
        {
            var closes = series.GetValueOrDefault(e.AnalysisSymbol.ToUpperInvariant());
            var (referencePrice, basis) = ResolveBasis(e, closes);
            var r = SignalReturnCalculator.Compute(e.SignalDate, referencePrice, closes);
            var days = today.DayNumber - e.SignalDate.DayNumber;
            var def = SignalTypeCatalog.Find(e.SignalType);
            decimal? actionedPrice = null;
            decimal? vsActioned = null;
            if (e.LinkedTransactionId is int linkId && linkedTx.TryGetValue(linkId, out var tx))
            {
                actionedPrice = e.Direction == SignalDirections.Sell && tx.ClosingPrice is > 0m ? tx.ClosingPrice : tx.AverageCostBasis;
                if (actionedPrice is > 0m && r.Today.EvalPrice is > 0m)
                    vsActioned = Math.Round((r.Today.EvalPrice.Value - actionedPrice.Value) / actionedPrice.Value * 100m, 2);
            }
            var effective = EffectiveStateProvider.ForEvent(e, cachedSnapshot, evalStatus, nowUtc);
            return new SignalAuditRowDto(
                e.Id, e.Symbol, e.Role, e.SignalDate.ToString("yyyy-MM-dd"), days, days < 0,
                e.SignalType, def?.Label ?? e.SignalType, e.SignalCategory, e.Direction, e.Source,
                e.SignalPrice, e.Currency, e.Score, r.Return5D, r.Return20D, r.Return1M, r.Today,
                e.MyAction, !string.IsNullOrEmpty(e.MyActionNote), e.LinkedTransactionId,
                LinkKind(e, linkedTx), e.Origin, e.EpisodeEndedDate is null, e.EligibilityStatus, basis,
                actionedPrice, vsActioned, e.Status, e.PerformanceAudited, e.LastValidatedAtUtc, e.StatusReason, e.SupersededByEventId,
                effective?.Status.State.ToString(), effective?.Status.IsActionable, effective?.EvidenceHealth,
                effective?.DataAsOf?.ToString("yyyy-MM-dd"), effective?.Codes);
        }).ToList();
    }

    /// <summary>
    /// Returns must compare like with like. If the provider's history was re-based since capture (a split),
    /// the stored signal price is scaled by the same factor and the row is flagged Adjusted; without a
    /// reference close the basis cannot be proven and the row is flagged Unverified.
    /// </summary>
    public static (decimal Price, string Basis) ResolveBasis(SignalAuditEvent e, IReadOnlyList<MarketDailyClose>? closes)
    {
        if (e.BasisClose is not > 0m || closes is null) return (e.SignalPrice, SignalPriceBasis.Unverified);
        var reference = closes.FirstOrDefault(c => c.Date == e.SignalDate);
        if (reference is null || reference.Close <= 0m) return (e.SignalPrice, SignalPriceBasis.Unverified);
        var factor = reference.Close / e.BasisClose.Value;
        return Math.Abs(factor - 1m) <= BasisTolerance
            ? (e.SignalPrice, SignalPriceBasis.Verified)
            : (e.SignalPrice * factor, SignalPriceBasis.Adjusted);
    }

    /// <summary>TransactionVerified: the linked transaction still exists and matches the signal's ticker and window.</summary>
    private static string LinkKind(SignalAuditEvent e, Dictionary<int, PortfolioItem> linked)
    {
        if (e.LinkedTransactionId is not int id)
            return SignalMyActions.IsFollowed(e.MyAction) ? "ManualConfirmed" : "None";
        if (!linked.TryGetValue(id, out var tx)) return "LinkBroken";
        var date = e.Direction == SignalDirections.Sell ? tx.CloseDate : tx.OpenDate;
        var matches = date is not null
            && DateOnly.FromDateTime(date.Value) >= e.SignalDate.AddDays(-1)
            && (tx.Symbol.Equals(e.Symbol, StringComparison.OrdinalIgnoreCase)
                || tx.Symbol.Equals(e.AnalysisSymbol, StringComparison.OrdinalIgnoreCase));
        return matches ? "TransactionVerified" : "LinkBroken";
    }

    private static List<SignalAuditRowDto> Sort(List<SignalAuditRowDto> rows, string? sortBy, string? sortDir)
    {
        var desc = !string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);
        // Missing returns always sort last, regardless of direction.
        static decimal? P(SignalHorizonDto h) => h.Pct;
        IOrderedEnumerable<SignalAuditRowDto> ordered = (sortBy ?? "signalDate").ToLowerInvariant() switch
        {
            "symbol" => Order(rows, r => r.Symbol, desc),
            "role" => Order(rows, r => r.Role ?? "", desc),
            "dayssince" => Order(rows, r => r.DaysSince, desc),
            "signaltype" => Order(rows, r => r.SignalTypeLabel, desc),
            "source" => Order(rows, r => r.Source, desc),
            "signalprice" => Order(rows, r => r.SignalPrice, desc),
            "return5d" => OrderNullable(rows, r => P(r.Return5D), desc),
            "return20d" => OrderNullable(rows, r => P(r.Return20D), desc),
            "return1m" => OrderNullable(rows, r => P(r.Return1M), desc),
            "today" => OrderNullable(rows, r => P(r.Today), desc),
            "todayvsactioned" => OrderNullable(rows, r => r.TodayVsActionedPct, desc),
            "myaction" => Order(rows, r => r.MyAction, desc),
            _ => Order(rows, r => r.SignalDate, desc),
        };
        return ordered.ThenBy(r => r.SignalTypeLabel, StringComparer.OrdinalIgnoreCase).ThenByDescending(r => r.Id).ToList();
    }

    private static IOrderedEnumerable<SignalAuditRowDto> Order<TKey>(
        IEnumerable<SignalAuditRowDto> rows, Func<SignalAuditRowDto, TKey> key, bool desc) =>
        desc ? rows.OrderByDescending(key) : rows.OrderBy(key);

    private static IOrderedEnumerable<SignalAuditRowDto> OrderNullable(
        IEnumerable<SignalAuditRowDto> rows, Func<SignalAuditRowDto, decimal?> key, bool desc) =>
        desc
            ? rows.OrderBy(r => key(r) is null).ThenByDescending(r => key(r) ?? 0m)
            : rows.OrderBy(r => key(r) is null).ThenBy(r => key(r) ?? 0m);

    // ── Market data ───────────────────────────────────────────────────────────

    private async Task<Dictionary<string, IReadOnlyList<MarketDailyClose>?>> LoadSeriesAsync(
        IReadOnlyList<string> symbols, CancellationToken ct)
    {
        using var gate = new SemaphoreSlim(4);
        var fetched = await Task.WhenAll(symbols.Select(async symbol =>
        {
            var key = $"signals-audit-closes:{symbol.ToUpperInvariant()}";
            if (cache.TryGetValue(key, out IReadOnlyList<MarketDailyClose>? hit)) return (symbol, hit);
            await gate.WaitAsync(ct);
            try
            {
                var closes = await marketData.GetDailyClosesAsync(symbol, ct);
                if (closes is { Count: > 0 }) cache.Set(key, closes, SeriesCacheTtl);
                return (symbol, closes);
            }
            finally { gate.Release(); }
        }));
        return fetched.ToDictionary(x => x.symbol.ToUpperInvariant(), x => x.Item2);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Current Toronto (Eastern) calendar date, independent of server or browser time zone.</summary>
    public static DateOnly TorontoToday() =>
        DateOnly.FromDateTime(MarketHoursGate.GetEasternNow() ?? DateTime.UtcNow);

    /// <summary>Weekend signals belong to the preceding Friday session.</summary>
    public static DateOnly SnapDate(DateOnly date) => date.DayOfWeek switch
    {
        DayOfWeek.Saturday => date.AddDays(-1),
        DayOfWeek.Sunday => date.AddDays(-2),
        _ => date,
    };

    private static decimal? Avg(List<decimal> values) =>
        values.Count == 0 ? null : Math.Round(values.Average(), 2);

    private static SignalLinkedTransactionDto ToLinkedDto(PortfolioItem p) => new(
        p.Id, p.Symbol, p.TransactionType, p.AccountType,
        p.OpenDate?.ToString("yyyy-MM-dd"), p.CloseDate?.ToString("yyyy-MM-dd"),
        p.Shares, p.AverageCostBasis, p.DecisionSource);

    private static SignalsAuditSettingsDto ToDto(SignalsAuditSettings s) => new(
        s.PriorityEnabled, s.PriorityThreshold, s.ActivatedAtUtc, s.BaselineSymbols.Count,
        s.FinalActionEnabled,
        ResolveEntries(s).Select(e =>
        {
            var def = FinalActionCatalog.Describe(e.Id);
            return new AuditedActionEntryDto(e.Id, e.Label, e.Enabled, e.Custom, FinalActionCatalog.IsKnown(e.Id), def.Category, def.Direction);
        }).ToList(),
        s.FinalActionBaseline.Count);

    private async Task<SignalsAuditSettings?> LoadSettingsAsync(string userId, CancellationToken ct)
    {
        var row = await db.UserPreferences.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId && p.PreferenceKey == SignalsAuditSettings.PreferenceKey, ct);
        if (row is null) return null;
        try { return JsonSerializer.Deserialize<SignalsAuditSettings>(row.PreferenceValue, SignalsAuditSettings.Json); }
        catch (JsonException) { return null; }
    }

    private async Task SaveSettingsRowAsync(string userId, SignalsAuditSettings settings, CancellationToken ct)
    {
        await StageSettingsRowAsync(userId, settings, ct);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Adds/updates the settings row in the change tracker without committing.</summary>
    private async Task StageSettingsRowAsync(string userId, SignalsAuditSettings settings, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(settings, SignalsAuditSettings.Json);
        var row = await db.UserPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId && p.PreferenceKey == SignalsAuditSettings.PreferenceKey, ct);
        if (row is null)
            db.UserPreferences.Add(new UserPreference
            {
                UserId = userId, PreferenceKey = SignalsAuditSettings.PreferenceKey,
                PreferenceValue = json, UpdatedAt = DateTime.UtcNow,
            });
        else
        {
            row.PreferenceValue = json;
            row.UpdatedAt = DateTime.UtcNow;
        }
    }
}
