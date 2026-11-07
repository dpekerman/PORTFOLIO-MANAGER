using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using PortfolioManager.Api.Services.FinalAction;
using Xunit;

namespace PortfolioManager.Tests;

public sealed class FinalActionStatusResolverTests
{
    private static readonly MarketExchange Tsx = MarketExchange.Tsx;
    // Tue 2026-10-27 08:00 EDT: latest completed session is Mon 2026-10-26.
    private static readonly DateTime MorningUtc = new(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Monday = new(2026, 10, 26);

    private static EvidenceInput Input(DateOnly? bar = null, DateTime? quote = null, decimal price = 10m, bool failed = false, string? missing = null) =>
        new(bar ?? Monday, quote, price, failed, missing);

    private static EvidenceAssessment Assess(EvidenceInput e, DateTime? now = null) =>
        FinalActionStatusResolver.AssessEvidence(e, Tsx, now ?? MorningUtc);

    private static PriorRecommendation Prior(string action, string role = "Swing", DateTime? validated = null, string status = "Active", string source = "Portfolio") =>
        new(action, source, role, status, validated ?? new DateTime(2026, 10, 26, 22, 30, 0, DateTimeKind.Utc), validated ?? new DateTime(2026, 10, 26, 22, 30, 0, DateTimeKind.Utc));

    private static ResolveInput In(
        EvidenceAssessment? current = null, DecisionBucket bucket = DecisionBucket.None, PriorRecommendation? prior = null,
        EvaluationRun run = EvaluationRun.None, DateTime? pass = null, bool attemptFailed = false, DateTime? now = null) =>
        new(current, bucket, prior, run, pass, attemptFailed, Tsx, now ?? MorningUtc);

    // ── Evidence health ──
    [Fact]
    public void ScanOfLastCompletedSession_IsFresh_BeforeTheNextEodScan()
    {
        var a = Assess(Input());
        Assert.Equal(EvidenceHealth.Fresh, a.Health);
        Assert.Equal(Monday, a.ExpectedSession);
        Assert.Contains(FinalActionStatusResolver.QuoteTimestampUnavailable, a.Codes);
    }

    [Fact]
    public void ScanOlderThanTheLastCompletedSession_IsStale()
    {
        Assert.Equal(EvidenceHealth.Stale, Assess(Input(bar: Monday.AddDays(-3))).Health);
    }

    [Fact]
    public void OldQuoteWrappedInANewScan_IsStale_ButNoQuoteTimestampFallsBackToTheBar()
    {
        var oldQuote = new DateTime(2026, 10, 22, 20, 0, 0, DateTimeKind.Utc);
        var a = Assess(Input(quote: oldQuote));
        Assert.Equal(EvidenceHealth.Stale, a.Health);
        Assert.Contains(FinalActionStatusResolver.QuoteOlderThanScan, a.Codes);
        Assert.Equal(EvidenceHealth.Fresh, Assess(Input(quote: new DateTime(2026, 10, 26, 20, 0, 0, DateTimeKind.Utc))).Health);
    }

    [Theory]
    [InlineData(true, 10, null, EvidenceHealth.Failed)]
    [InlineData(false, 0, null, EvidenceHealth.Invalid)]
    [InlineData(false, 10, "RSI", EvidenceHealth.Missing)]
    public void FailedInvalidOrMissingRequiredEvidence_IsNeverFresh(bool failed, decimal price, string? missing, EvidenceHealth expected) =>
        Assert.Equal(expected, Assess(Input(price: price, failed: failed, missing: missing)).Health);

    [Fact]
    public void RequiredEvidence_DependsOnTheRule_AndOptionalIndicatorsNeverInvalidate()
    {
        var scan = new RsiScanResult { Symbol = "X", CurrentPrice = 10m, Rsi = 55m, Ema20Price = 9m, Sma50Price = 8m };   // no fib/volume/analyst/macd
        Assert.Null(FinalActionStatusResolver.MissingRequired(scan, "Entry"));
        scan.Rsi = 0m;
        Assert.Equal("RSI", FinalActionStatusResolver.MissingRequired(scan, "Entry"));
        Assert.Null(FinalActionStatusResolver.MissingRequired(scan, "TrimExit"));   // a loss/allocation exit needs price, not RSI
        scan.CurrentPrice = 0m;
        Assert.Equal("PRICE", FinalActionStatusResolver.MissingRequired(scan, "Advisory"));
    }

    // ── Effective state ──
    [Fact]
    public void FreshEvaluation_IsCurrentValid_AndActionableOnlyWhenTheBucketIs()
    {
        var s = FinalActionStatusResolver.Resolve(In(Assess(Input()), DecisionBucket.EntryAdd));
        Assert.Equal(EffectiveState.CurrentValid, s.State);
        Assert.True(s.IsActionable);
        Assert.False(FinalActionStatusResolver.Resolve(In(Assess(Input()), DecisionBucket.None)).IsActionable);
    }

    [Fact]
    public void ValidPriorSessionAction_IsRetained_OvernightWithoutAnEvaluation()          // acceptance 1 / release 2
    {
        var s = FinalActionStatusResolver.Resolve(In(prior: Prior("Add to Trade"), pass: new DateTime(2026, 10, 26, 22, 30, 0, DateTimeKind.Utc)));
        Assert.Equal(EffectiveState.PriorSessionValid, s.State);
        Assert.True(s.IsActionable);
        Assert.Equal(0, s.AgeTradingDays);
    }

    [Fact]
    public void NoCanonicalActionAndNoPrior_IsAwaitingData_NeverAHold()                    // acceptance 2
    {
        var s = FinalActionStatusResolver.Resolve(In());
        Assert.Equal(EffectiveState.AwaitingData, s.State);
        Assert.False(s.IsActionable);
        Assert.Equal(DecisionBucket.None, s.Bucket);
        Assert.Contains(FinalActionStatusResolver.AwaitingNoEvaluation, s.Codes);
    }

    [Theory]
    [InlineData(true, 10, null, null)]
    [InlineData(false, 10, null, "STALE")]
    [InlineData(false, 0, null, null)]
    [InlineData(false, 10, "RSI", null)]
    public void FailedPartialOrStaleEvaluation_IsAwaitingData_AndNotActionable_EvenWithAPriorRecommendation(bool failed, decimal price, string? missing, string? stale)
    {
        var e = Input(bar: stale is null ? Monday : Monday.AddDays(-4), price: price, failed: failed, missing: missing);
        var s = FinalActionStatusResolver.Resolve(In(Assess(e), DecisionBucket.EntryAdd, Prior("Add to Trade")));   // a "current" bucket from bad evidence must not count
        Assert.Equal(EffectiveState.AwaitingData, s.State);
        Assert.False(s.IsActionable);
    }

    [Fact]
    public void PriorRecommendation_BeyondItsWindow_IsAwaiting_ShortHorizonExit3Days()      // acceptance 5
    {
        var validated = new DateTime(2026, 10, 19, 22, 30, 0, DateTimeKind.Utc);            // Mon Oct 19 session
        var s = FinalActionStatusResolver.Resolve(In(prior: Prior("Exit / Cut Loss", validated: validated)));
        Assert.Equal(EffectiveState.AwaitingData, s.State);
        Assert.Contains(FinalActionStatusResolver.LifecycleWindowElapsed, s.Codes);
        Assert.False(s.IsActionable);
    }

    [Fact]
    public void HolidayWeekend_DoesNotConsumeTheLifecycleWindow()
    {
        // TSX closed Mon Oct 12: validated Fri Oct 9, evaluated Tue Oct 13 morning ⇒ 0 sessions old (expected = Fri Oct 9).
        var validated = new DateTime(2026, 10, 9, 22, 30, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);
        var s = FinalActionStatusResolver.Resolve(In(prior: Prior("Exit / Cut Loss", validated: validated), now: now));
        Assert.Equal(EffectiveState.PriorSessionValid, s.State);
        Assert.Equal(0, s.AgeTradingDays);
    }

    [Fact]
    public void CompletePassThatMissedTheSymbol_MeansItWasNotEvaluated_SoAwaitingData()    // partial refresh / failed symbol
    {
        var validated = new DateTime(2026, 10, 22, 22, 30, 0, DateTimeKind.Utc);            // Thu
        var pass = new DateTime(2026, 10, 26, 22, 30, 0, DateTimeKind.Utc);                 // Monday's complete pass skipped it
        var s = FinalActionStatusResolver.Resolve(In(prior: Prior("Add to Trade", validated: validated), pass: pass));
        Assert.Equal(EffectiveState.AwaitingData, s.State);
        Assert.Contains(FinalActionStatusResolver.RevalidationMissed, s.Codes);
    }

    [Fact]
    public void NoPassYetSincePriorSession_StaysPriorSessionValid_WithinWindow()
    {
        var validated = new DateTime(2026, 10, 22, 22, 30, 0, DateTimeKind.Utc);            // Thu; no pass since (EOD not run yet)
        var s = FinalActionStatusResolver.Resolve(In(prior: Prior("Add to Trade", validated: validated), pass: validated));
        Assert.Equal(EffectiveState.PriorSessionValid, s.State);
    }

    [Fact]
    public void FailedLastAttempt_PreventsRenewal_AndStrategicExit_BecomesHistoricalWarning()   // acceptance 3, 10 / adj. 5
    {
        var s = FinalActionStatusResolver.Resolve(In(prior: Prior("Trim / Review", role: "Strategic"), attemptFailed: true));
        Assert.Equal(EffectiveState.HistoricalWarning, s.State);
        Assert.True(s.IsHistoricalWarning);
        Assert.False(s.IsActionable);
        Assert.Equal(DecisionBucket.TrimExit, s.Bucket);
    }

    [Fact]
    public void StrategicExit_HasNoCalendarExpiry_WhileEvidenceIsNotKnownBad()
    {
        var old = new DateTime(2026, 9, 1, 22, 30, 0, DateTimeKind.Utc);
        var s = FinalActionStatusResolver.Resolve(In(prior: Prior("Trim / Review", role: "Strategic", validated: old)));
        Assert.Equal(EffectiveState.PriorSessionValid, s.State);
        Assert.True(s.AgeTradingDays > 3);
    }

    [Fact]
    public void RiskReview_StaleEvidence_StaysListedAsHistoricalWarning()
    {
        var s = FinalActionStatusResolver.Resolve(In(Assess(Input(failed: true)), DecisionBucket.None, Prior("Cut Loss / Thesis Review")));
        Assert.Equal(EffectiveState.HistoricalWarning, s.State);
    }

    [Fact]
    public void ExpiredLifecycle_IsReportedAsExpired()
    {
        Assert.Equal(EffectiveState.Expired, FinalActionStatusResolver.Resolve(In(prior: Prior("Add to Trade", status: "Expired"))).State);
    }

    [Fact]
    public void SuccessfulEvaluation_RestoresTheCanonicalAction()                             // acceptance 6
    {
        var prior = Prior("Add to Trade");
        var awaiting = FinalActionStatusResolver.Resolve(In(Assess(Input(failed: true)), prior: prior));
        Assert.Equal(EffectiveState.AwaitingData, awaiting.State);
        var restored = FinalActionStatusResolver.Resolve(In(Assess(Input()), DecisionBucket.EntryAdd, prior));
        Assert.Equal(EffectiveState.CurrentValid, restored.State);
        Assert.True(restored.IsActionable);
    }

    // ── Evaluating must never get stuck ──
    [Fact]
    public void RunningWithFreshHeartbeat_IsEvaluating_ButStaleHeartbeat_IsInterrupted()
    {
        var now = MorningUtc;
        // The window boundary: exactly 15:00 since the last heartbeat is still running, 15:01 is interrupted.
        Assert.Equal(EvaluationRun.Running, FinalActionStatusResolver.RunState("Running", now - FinalActionStatusResolver.HeartbeatWindow, now.AddHours(-1), now));
        Assert.Equal(EvaluationRun.Interrupted, FinalActionStatusResolver.RunState("Running", now - FinalActionStatusResolver.HeartbeatWindow - TimeSpan.FromSeconds(1), now.AddHours(-1), now));
        Assert.Equal(TimeSpan.FromMinutes(15), FinalActionStatusResolver.HeartbeatWindow);
        Assert.Equal(TimeSpan.FromHours(2), TradingCalendar.EvidenceGrace);
        Assert.Equal(EvaluationRun.Running, FinalActionStatusResolver.RunState("Running", now.AddMinutes(-3), now.AddHours(-1), now));
        Assert.Equal(EvaluationRun.Interrupted, FinalActionStatusResolver.RunState("Running", now.AddMinutes(-40), now.AddHours(-1), now));
        Assert.Equal(EvaluationRun.Interrupted, FinalActionStatusResolver.RunState("Running", null, now.AddHours(-2), now));
        Assert.Equal(EvaluationRun.None, FinalActionStatusResolver.RunState("Success", now, now.AddHours(-1), now));

        Assert.Equal(EffectiveState.Evaluating, FinalActionStatusResolver.Resolve(In(run: EvaluationRun.Running)).State);
        var interrupted = FinalActionStatusResolver.Resolve(In(run: EvaluationRun.Interrupted, prior: Prior("Add to Trade")));
        Assert.Equal(EffectiveState.AwaitingData, interrupted.State);
        Assert.Contains(FinalActionStatusResolver.EvaluationInterrupted, interrupted.Codes);
    }

    // ── Invariant: absence of evidence never becomes Hold / Buy / Sell ──
    [Theory]
    [InlineData("Swing")] [InlineData("Strategic")] [InlineData("Core")] [InlineData("Speculative")] [InlineData("Options")]
    public void Invariant_BadEvidence_NeverProducesAnActionableState_ForAnyRoleOrPrior(string role)
    {
        string[] priors = ["Add to Trade", "Add / Staged Entry", "Exit / Cut Loss", "Trim / Review", "Cut Loss / Thesis Review", "Hold", "Hold / Monitor"];
        foreach (var action in priors.Append(null))
        foreach (var run in new[] { EvaluationRun.None, EvaluationRun.Running, EvaluationRun.Interrupted })
        foreach (var bad in new[] { Input(failed: true), Input(price: 0m), Input(missing: "RSI"), Input(bar: Monday.AddDays(-5)) })
        {
            var s = FinalActionStatusResolver.Resolve(In(Assess(bad), DecisionBucket.TrimExit, action is null ? null : Prior(action, role), run));
            Assert.False(s.IsActionable);
            Assert.NotEqual(EffectiveState.CurrentValid, s.State);
            Assert.NotEqual(EffectiveState.PriorSessionValid, s.State);
        }
    }
}
