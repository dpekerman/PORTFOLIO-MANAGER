using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using PortfolioManager.Api.Services.FinalAction;

namespace PortfolioManager.Tests;

public sealed class OpenDecisionsServiceTests
{
    private const string User = "user-1";

    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static SignalAuditEvent Row(string symbol, string actionId, string source, string status = SignalStatuses.Active,
        bool validated = true, bool audited = true, string user = User, string? type = null, string role = "Core", string date = "2026-10-20")
    {
        var d = DateOnly.Parse(date);
        return new SignalAuditEvent
        {
            UserId = user, Symbol = symbol, AnalysisSymbol = symbol,
            SignalType = type ?? SignalTypeCatalog.FinalActionKey(actionId), SignalCategory = SignalCategories.Entry,
            Direction = SignalDirections.Buy, SignalDate = d, SignalPrice = 10m, Source = source, Role = role,
            EpisodeKey = $"{actionId}|{symbol}|{source}|{date}|{status}", Status = status, PerformanceAudited = audited,
            LastValidatedAtUtc = validated ? DateTime.UtcNow : null,
            EpisodeEndedDate = status == SignalStatuses.Active ? null : d,
            ContextJson = "{\"finalActionReason\":\"Volume and momentum confirmed\"}",
        };
    }

    private static async Task<(AppDbContext Db, OpenDecisionsService Svc)> SeedAsync(params SignalAuditEvent[] rows)
    {
        var db = NewDb();
        db.SignalAuditEvents.AddRange(rows);
        await db.SaveChangesAsync();
        return (db, new OpenDecisionsService(db));
    }

    [Fact]
    public async Task OnlyActiveValidatedEntryTrimExitAndRiskDecisionsAreCounted()
    {
        var (_, svc) = await SeedAsync(
            Row("ADD1", "ADD/STAGED ENTRY", SignalSources.Portfolio),
            Row("ENT1", "STARTER ENTRY", SignalSources.Watchlist),
            Row("TRM1", "TRIM/REVIEW", SignalSources.Portfolio),
            Row("RSK1", "CUT LOSS/THESIS REVIEW", SignalSources.Portfolio),
            Row("WATCH", "BUY WATCH", SignalSources.Watchlist),
            Row("AVD", "AVOID", SignalSources.Watchlist),
            Row("WAIT", "WAIT FOR PULLBACK", SignalSources.Watchlist),
            Row("DNC", "WATCH/DO NOT CHASE", SignalSources.Portfolio),
            Row("HREV", "HOLD/REVIEW", SignalSources.Portfolio),
            Row("OLD", "STARTER ENTRY", SignalSources.Watchlist, SignalStatuses.Expired),
            Row("DONE", "STARTER ENTRY", SignalSources.Watchlist, SignalStatuses.Dismissed),
            Row("BASE", "STARTER ENTRY", SignalSources.Watchlist, validated: false),
            Row("PC", "X", SignalSources.Watchlist, type: SignalTypeCatalog.PriorityCandidateKey),
            Row("OTHER", "STARTER ENTRY", SignalSources.Watchlist, user: "user-2"));

        var r = await svc.GetAsync(User, null);
        Assert.Equal(2, r.Summary.EntryAdd);
        Assert.Equal(1, r.Summary.TrimExit);
        Assert.Equal(1, r.Summary.RiskReview);
        Assert.Equal(4, r.Summary.TotalOpen);
        Assert.Equal(r.Summary.TotalOpen, r.Items.Count);               // summary reconciles with the list
        Assert.DoesNotContain(r.Items, i => i.Symbol is "WATCH" or "AVD" or "WAIT" or "DNC" or "HREV" or "OLD" or "DONE" or "BASE" or "PC" or "OTHER");
    }

    private static async Task SeedSettingsAsync(AppDbContext db, SignalsAuditSettings settings)
    {
        db.UserPreferences.Add(new UserPreference
        {
            UserId = User, PreferenceKey = SignalsAuditSettings.PreferenceKey,
            PreferenceValue = System.Text.Json.JsonSerializer.Serialize(settings, SignalsAuditSettings.Json),
        });
        await db.SaveChangesAsync();
    }

    /// <summary>Rows built by <see cref="Row"/> are stamped "now"; the evaluation is placed a minute earlier so they count as confirmed by it.</summary>
    private static SignalsAuditSettings Evaluated(DateTime completeAt, bool lastPassComplete = true, DateTime? attemptAt = null) =>
        new()
        {
            InitialBaselineDone = true, LifecycleBaselineDone = true,
            LastCompleteEvaluationUtc = completeAt.AddMinutes(-1), LastEodEvaluationUtc = attemptAt ?? completeAt, LastEodEvaluationComplete = lastPassComplete,
        };

    [Fact]
    public async Task Validation_NeverEvaluated_IsPending_EvenWithNoRows_NeverAnAllClear()
    {
        var (_, empty) = await SeedAsync();
        Assert.Equal("Pending", (await empty.GetAsync(User, null)).Validation.State);

        var (_, withRows) = await SeedAsync(Row("OLD", "STARTER ENTRY", SignalSources.Watchlist, validated: false));
        var r = await withRows.GetAsync(User, null);
        Assert.Equal("Pending", r.Validation.State);
        Assert.Equal(1, r.Validation.PendingCount);
        Assert.Equal(0, r.Summary.TotalOpen);                  // unvalidated rows are hidden, not forced into the counts
    }

    [Fact]
    public async Task Validation_CompleteRecentEvaluation_IsValidated_AndEmptyIsGenuine()
    {
        var (db, svc) = await SeedAsync(Row("OK", "STARTER ENTRY", SignalSources.Watchlist));
        await SeedSettingsAsync(db, Evaluated(DateTime.UtcNow));
        var r = await svc.GetAsync(User, null);
        Assert.Equal("Validated", r.Validation.State);
        Assert.Equal(0, r.Validation.PendingCount);

        var (db2, none) = await SeedAsync();
        await SeedSettingsAsync(db2, Evaluated(DateTime.UtcNow));
        var empty = await none.GetAsync(User, null);
        Assert.Equal("Validated", empty.Validation.State);
        Assert.Equal(0, empty.Summary.TotalOpen);
    }

    [Fact]
    public async Task Validation_RowsStampedByTheSamePass_AreNotPending_DespiteMillisecondTruncation()
    {
        // SQL Server stores LastValidatedAtUtc as datetime2(3); the settings JSON keeps the full ticks of the same instant.
        var passAt = new DateTime(2026, 10, 26, 20, 47, 39, DateTimeKind.Utc).AddTicks(1656731);
        var row = Row("SAME", "TRIM/REVIEW", SignalSources.Portfolio);
        row.LastValidatedAtUtc = new DateTime(2026, 10, 26, 20, 47, 39, 165, DateTimeKind.Utc);
        var (db, svc) = await SeedAsync(row);
        await SeedSettingsAsync(db, new SignalsAuditSettings
        {
            InitialBaselineDone = true, LifecycleBaselineDone = true,
            LastCompleteEvaluationUtc = passAt, LastEodEvaluationUtc = passAt, LastEodEvaluationComplete = true,
        });
        var r = await svc.GetAsync(User, null);
        Assert.Equal(0, r.Validation.PendingCount);
    }

    [Fact]
    public async Task Validation_RecommendationNotRevalidatedByTheLatestCompletePass_IsPending()
    {
        var stale = Row("MISS", "SMALL ADD", SignalSources.Portfolio);
        stale.LastValidatedAtUtc = DateTime.UtcNow.AddHours(-30);
        var (db, svc) = await SeedAsync(stale, Row("FRESH", "STARTER ENTRY", SignalSources.Watchlist));
        await SeedSettingsAsync(db, Evaluated(DateTime.UtcNow));
        var r = await svc.GetAsync(User, null);
        Assert.Equal("Pending", r.Validation.State);
        Assert.Equal(1, r.Validation.PendingCount);
    }

    [Fact]
    public async Task Validation_FailedOrIncompleteLatestPass_IsUnavailable_AndKeepsExistingRows()
    {
        var (db, svc) = await SeedAsync(Row("KEEP", "TRIM/REVIEW", SignalSources.Portfolio), Row("ADD1", "ADD/STAGED ENTRY", SignalSources.Portfolio, role: "Swing"));
        await SeedSettingsAsync(db, Evaluated(DateTime.UtcNow.AddHours(-20), lastPassComplete: false, attemptAt: DateTime.UtcNow));
        var r = await svc.GetAsync(User, null);
        Assert.Equal("Unavailable", r.Validation.State);
        // A failed evaluation cannot renew a decision: it is no longer counted as actionable, but nothing is hidden or deleted.
        Assert.Equal(0, r.Summary.TotalOpen);
        Assert.Equal(1, r.Summary.HistoricalWarnings);          // confirmed Strategic/Core risk stays visible as a historical warning
        Assert.Equal(1, r.Summary.AwaitingData);
        Assert.Equal(["ADD1", "KEEP"], r.NotActionable!.Select(i => i.Symbol).Order());
        Assert.True(r.NotActionable!.Single(i => i.Symbol == "KEEP").IsHistoricalWarning);
        Assert.Equal(2, (await db.SignalAuditEvents.CountAsync(e => e.Status == SignalStatuses.Active)));   // history intact
    }

    [Fact]
    public async Task Validation_StaleLastCompleteEvaluation_IsUnavailable()
    {
        var (db, svc) = await SeedAsync();
        await SeedSettingsAsync(db, Evaluated(DateTime.UtcNow.AddDays(-10)));
        Assert.Equal("Unavailable", (await svc.GetAsync(User, null)).Validation.State);
    }

    [Fact]
    public async Task Validation_UnauditedRecommendation_DoesNotChangeTheState()
    {
        var (db, svc) = await SeedAsync(Row("UNA", "TRIM/REVIEW", SignalSources.Portfolio, audited: false));
        await SeedSettingsAsync(db, Evaluated(DateTime.UtcNow));
        var r = await svc.GetAsync(User, null);
        Assert.Equal("Validated", r.Validation.State);
        Assert.Equal(1, r.Summary.TotalOpen);
    }

    [Fact]
    public async Task UnauditedRecommendations_AreStillOpenDecisions()
    {
        var (_, svc) = await SeedAsync(Row("UNA", "TRIM/REVIEW", SignalSources.Portfolio, audited: false));
        Assert.Equal(1, (await svc.GetAsync(User, null)).Summary.TotalOpen);
    }

    [Fact]
    public async Task Filters_ReturnSubsetsOfTheSameSet_AndSortingPutsRiskThenHoldingsThenWatchlist()
    {
        var (_, svc) = await SeedAsync(
            Row("WL", "STARTER ENTRY", SignalSources.Watchlist, date: "2026-10-23"),
            Row("HADD", "SMALL ADD", SignalSources.Portfolio),
            Row("HTRM", "CUT LOSS/EXIT", SignalSources.Portfolio),
            Row("HRSK", "CLOSE/PROTECT", SignalSources.Portfolio));

        var all = await svc.GetAsync(User, "All");
        Assert.Equal(["HRSK", "HTRM", "HADD", "WL"], all.Items.Select(i => i.Symbol));
        Assert.Equal("Urgent", all.Items[0].Urgency);

        Assert.Equal(["HADD", "WL"], (await svc.GetAsync(User, "Entry")).Items.Select(i => i.Symbol).Order());
        Assert.Equal(["HTRM"], (await svc.GetAsync(User, "ReduceExit")).Items.Select(i => i.Symbol));
        Assert.Equal(["HRSK"], (await svc.GetAsync(User, "Risk")).Items.Select(i => i.Symbol));
        Assert.Equal(3, (await svc.GetAsync(User, "Holdings")).Items.Count);
        Assert.Equal(["WL"], (await svc.GetAsync(User, "Watchlist")).Items.Select(i => i.Symbol));
        Assert.All(new[] { "Entry", "ReduceExit", "Risk", "Holdings", "Watchlist" },
            f => Assert.Equal(4, svc.GetAsync(User, f).Result.Summary.TotalOpen));   // counts never depend on the filter
    }

    [Fact]
    public async Task Rows_CarryRationaleEvidenceAndFreshness()
    {
        var (_, svc) = await SeedAsync(Row("HPS", "ADD/STAGED ENTRY", SignalSources.Portfolio));
        var item = Assert.Single((await svc.GetAsync(User, null)).Items);
        Assert.Equal("Volume and momentum confirmed", item.Rationale);
        Assert.Equal("Holding", item.Context);
        Assert.False(item.IsStale);
        Assert.NotNull(item.LastValidatedAtUtc);
    }

    [Fact]
    public async Task OldValidation_IsShownAsStale_NotHidden()
    {
        var row = Row("STL", "SMALL ADD", SignalSources.Portfolio);
        row.LastValidatedAtUtc = DateTime.UtcNow.AddDays(-6);
        var (_, svc) = await SeedAsync(row);
        var item = Assert.Single((await svc.GetAsync(User, null)).Items);
        Assert.True(item.IsStale);
    }

    [Fact]
    public async Task Empty_ReturnsZeroSummary()
    {
        var (_, svc) = await SeedAsync();
        var r = await svc.GetAsync(User, null);
        Assert.Equal(0, r.Summary.TotalOpen);
        Assert.Null(r.Summary.LastValidatedAtUtc);
        Assert.Empty(r.Items);
    }

    // ── Phase 2: effective states, Awaiting Data and counts ──

    private sealed class CachedFinalActions(FinalActionSnapshotDto? snapshot) : PortfolioManager.Api.Services.FinalAction.IFinalActionService
    {
        public Task<FinalActionSnapshotDto> ComputeAsync(string userId, bool fresh = false, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> PublishPortfolioActionsAsync(string userId, FinalActionSnapshotDto snapshot, CancellationToken ct = default) => throw new NotSupportedException();
        public FinalActionSnapshotDto? TryGetCached(string userId) => snapshot;
    }

    private static FinalActionResultDto Result(string symbol, string source, string action, string state, string health) =>
        new(symbol, source, null, "Swing", action, FinalActionCatalog.CanonicalId(action), FinalActionCatalog.Describe(action).Category, "Buy",
            "hold", "INFORMATIONAL", "ma-standby", "", "", "", "", "", null, false, 0m, symbol, null,
            EvaluationState: state, EvidenceHealth: health, ReasonCodes: ["AWAITING_REFRESH_FAILED"]);

    [Fact]
    public async Task PriorSessionRecommendation_WithinItsWindow_StaysActionable_WithoutASameDayEvaluation()   // acceptance 1
    {
        var (_, svc) = await SeedAsync(Row("PRIOR", "SMALL ADD", SignalSources.Portfolio, role: "Swing"));
        var r = await svc.GetAsync(User, null);
        var item = Assert.Single(r.Items);
        Assert.Equal("PriorSessionValid", item.EffectiveState);
        Assert.Equal(1, r.Summary.TotalOpen);
        Assert.Empty(r.NotActionable!);
    }

    [Fact]
    public async Task RecommendationBeyondItsWindow_IsAwaitingData_Listed_NotCounted_AndNotDeleted()            // acceptance 5, 9
    {
        var row = Row("OLDADD", "SMALL ADD", SignalSources.Portfolio, role: "Swing");
        row.LastValidatedAtUtc = DateTime.UtcNow.AddDays(-30);
        var (db, svc) = await SeedAsync(row, Row("OK", "STARTER ENTRY", SignalSources.Watchlist));
        var r = await svc.GetAsync(User, null);
        Assert.Equal(1, r.Summary.TotalOpen);
        Assert.Equal(1, r.Summary.AwaitingData);
        Assert.Equal(r.Summary.TotalOpen, r.Items.Count);
        var awaiting = Assert.Single(r.NotActionable!);
        Assert.Equal("OLDADD", awaiting.Symbol);
        Assert.Equal("AwaitingData", awaiting.EffectiveState);
        Assert.Contains("LIFECYCLE_WINDOW_ELAPSED", awaiting.ReasonCodes!);
        Assert.Equal(SignalStatuses.Active, (await db.SignalAuditEvents.SingleAsync(e => e.Symbol == "OLDADD")).Status);   // history untouched
    }

    [Fact]
    public async Task FailedRefreshForASymbol_MakesItAwaitingData_EvenInsideItsLifecycle_ButKeepsTheOthers()   // acceptance 3, 4
    {
        var bad = Result("BAD", SignalSources.Portfolio, "Awaiting Data", "AwaitingData", "Failed");
        var snapshot = new FinalActionSnapshotDto([], [bad], DateTime.UtcNow);
        var db = NewDb();
        db.SignalAuditEvents.AddRange(Row("BAD", "SMALL ADD", SignalSources.Portfolio, role: "Swing"), Row("GOOD", "SMALL ADD", SignalSources.Portfolio, role: "Swing"));
        await db.SaveChangesAsync();
        var r = await new OpenDecisionsService(db, new CachedFinalActions(snapshot)).GetAsync(User, null);
        Assert.Equal(["GOOD"], r.Items.Select(i => i.Symbol));
        Assert.Equal(["BAD"], r.NotActionable!.Select(i => i.Symbol));
        Assert.Equal(1, r.Summary.TotalOpen);
    }

    [Fact]
    public async Task StrategicExit_HasNoCalendarExpiry_AndBecomesAHistoricalWarning_WhenEvidenceIsUnavailable()   // acceptance 10 / adj. 5
    {
        var row = Row("STRAT", "TRIM/REVIEW", SignalSources.Portfolio, role: "Strategic");
        row.LastValidatedAtUtc = DateTime.UtcNow.AddDays(-45);
        var (_, svc) = await SeedAsync(row);
        var counted = await svc.GetAsync(User, null);
        Assert.Equal("PriorSessionValid", Assert.Single(counted.Items).EffectiveState);                 // no calendar expiry

        var snapshot = new FinalActionSnapshotDto([], [Result("STRAT", SignalSources.Portfolio, "Awaiting Data", "AwaitingData", "Stale")], DateTime.UtcNow);
        var db = NewDb();
        db.SignalAuditEvents.Add(row);
        await db.SaveChangesAsync();
        var r = await new OpenDecisionsService(db, new CachedFinalActions(snapshot)).GetAsync(User, null);
        Assert.Empty(r.Items);
        var warning = Assert.Single(r.NotActionable!);
        Assert.True(warning.IsHistoricalWarning);
        Assert.Equal("HistoricalWarning", warning.EffectiveState);
        Assert.Equal(1, r.Summary.HistoricalWarnings);
        Assert.Equal(0, r.Summary.TotalOpen);
    }

    [Fact]
    public async Task EvaluatingIsDeterministic_FreshHeartbeatEvaluates_StaleHeartbeatIsAwaitingNotStuck()   // adj. 7 / acceptance 22
    {
        var (db, svc) = await SeedAsync(Row("EV", "SMALL ADD", SignalSources.Portfolio, role: "Swing"));
        var run = new AutomationRunLog
        {
            RunId = Guid.NewGuid(), TradingDate = "2026-10-27", OverallStatus = AutomationStatuses.Running,
            ActualStartUtc = DateTime.UtcNow.AddMinutes(-50), LastHeartbeatAtUtc = DateTime.UtcNow.AddMinutes(-2),
        };
        db.AutomationRunLogs.Add(run);
        await db.SaveChangesAsync();

        // The row is valid as of the last completed session, but a fresh heartbeat means an evaluation is in progress:
        // it is shown as Evaluating (never actionable) until the run finishes.
        var running = await svc.GetAsync(User, null);
        Assert.Equal("Evaluating", Assert.Single(running.NotActionable!).EffectiveState);
        Assert.Equal(0, running.Summary.TotalOpen);

        run.LastHeartbeatAtUtc = DateTime.UtcNow.AddMinutes(-40);
        await db.SaveChangesAsync();
        var interrupted = await svc.GetAsync(User, null);
        var item = Assert.Single(interrupted.NotActionable!);
        Assert.Equal("AwaitingData", item.EffectiveState);
        Assert.Contains("EVALUATION_INTERRUPTED", item.ReasonCodes!);

        run.OverallStatus = AutomationStatuses.Succeeded;
        await db.SaveChangesAsync();
        Assert.Equal(1, (await svc.GetAsync(User, null)).Summary.TotalOpen);                            // recovers by itself
    }

    [Fact]
    public async Task Summary_AlwaysEqualsTheItemsList_AndFiltersNeverChangeIt()                          // acceptance 9, 13 / release 6
    {
        var old = Row("A1", "SMALL ADD", SignalSources.Portfolio, role: "Swing");
        old.LastValidatedAtUtc = DateTime.UtcNow.AddDays(-40);
        var (_, svc) = await SeedAsync(old, Row("B1", "STARTER ENTRY", SignalSources.Watchlist), Row("C1", "CUT LOSS/EXIT", SignalSources.Portfolio, role: "Swing"));
        foreach (var f in new[] { null, "Entry", "ReduceExit", "Holdings" })
        {
            var r = await svc.GetAsync(User, f);
            Assert.Equal(2, r.Summary.TotalOpen);
            Assert.Equal(r.Summary.EntryAdd + r.Summary.TrimExit + r.Summary.RiskReview, r.Summary.TotalOpen);
        }
    }

    [Fact]
    public void Orchestrator_EvaluationIsCompleteOnlyWhenRefreshAndRsiReallySucceeded()
    {
        static AutomationRunLog Log(string refresh, string rsi) => new() { RefreshStatus = refresh, RsiStatus = rsi };
        Assert.True(EodAutomationOrchestratorService.IsEvaluationComplete(Log(AutomationStatuses.Succeeded, AutomationStatuses.Succeeded)));
        Assert.False(EodAutomationOrchestratorService.IsEvaluationComplete(Log(AutomationStatuses.Failed, AutomationStatuses.Succeeded)));   // false-success RSI
        Assert.False(EodAutomationOrchestratorService.IsEvaluationComplete(Log(AutomationStatuses.Succeeded, AutomationStatuses.NotEligible)));
        Assert.False(EodAutomationOrchestratorService.IsEvaluationComplete(Log("", "")));
    }
}
