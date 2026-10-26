using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using PortfolioManager.Api.Services.FinalAction;

namespace PortfolioManager.Tests;

public sealed class SignalsAuditServiceTests
{
    private const string User = "user-1";

    private static ActionScoreDto Score(string symbol, decimal total, decimal price = 50m, string role = "Strategic") => new(
        symbol, symbol, role, "Strategic", 10m, 20m, 15m, 10m, total, "WATCH", "Bullish Shift", 45m, "under", price);

    [Fact]
    public async Task Capture_FirstRun_RecordsCurrentQualifiersAsInitialBaseline_DatedToday()
    {
        var h = new Harness([Score("AAA", 90m, 42m), Score("LOW", 40m)]);
        var result = await h.Service.CaptureAsync(User);

        Assert.Equal(1, result.Created);
        var ev = Assert.Single(h.Db.SignalAuditEvents);
        Assert.Equal("AAA", ev.Symbol);
        Assert.Equal(SignalOrigins.InitialBaseline, ev.Origin);
        Assert.Equal(90m, ev.Score);
        Assert.Equal(42m, ev.SignalPrice);
        Assert.Equal("Strategic", ev.Role);
        Assert.Null(ev.EpisodeEndedDate);
    }

    [Fact]
    public async Task Capture_RepeatedRunsAfterInitialBaseline_DoNotDuplicate_AndLaterTransitionsAreLive()
    {
        var h = new Harness([Score("AAA", 90m)]);
        await h.Service.CaptureAsync(User);
        Assert.Equal(0, (await h.Service.CaptureAsync(User)).Created);
        Assert.Equal(0, (await h.Service.CaptureAsync(User)).Created);
        Assert.Single(h.Db.SignalAuditEvents);

        h.Scores.Items = [Score("AAA", 90m), Score("NEW", 88m)];
        Assert.Equal(1, (await h.Service.CaptureAsync(User)).Created);
        Assert.Equal(SignalOrigins.Live, h.Db.SignalAuditEvents.Single(e => e.Symbol == "NEW").Origin);
        Assert.Equal(SignalOrigins.InitialBaseline, h.Db.SignalAuditEvents.Single(e => e.Symbol == "AAA").Origin);
    }

    [Fact]
    public async Task Capture_InitialBaseline_SurvivesConfigurationChanges_AndCanBeFilteredOutOfStats()
    {
        var h = new Harness([Score("AAA", 90m)]);
        await h.Service.CaptureAsync(User);
        await h.Service.SaveSettingsAsync(User, new SaveSignalsAuditSettingsRequest(true, 80m));
        await h.Service.CaptureAsync(User);

        Assert.Equal(SignalOrigins.InitialBaseline, Assert.Single(h.Db.SignalAuditEvents).Origin);

        var all = await h.Service.QueryAsync(User, new SignalsAuditQuery());
        Assert.Equal(1, all.Total);
        Assert.Equal(0, (await h.Service.QueryAsync(User, new SignalsAuditQuery(Origin: "Live"))).Total);
        Assert.Equal(1, (await h.Service.QueryAsync(User, new SignalsAuditQuery(Origin: SignalOrigins.InitialBaseline))).Total);

        Assert.Empty((await h.Service.GetSummaryAsync(User, new SignalsAuditQuery())).Effectiveness);
        Assert.NotEmpty((await h.Service.GetSummaryAsync(User, new SignalsAuditQuery(IncludeBaselineInStats: true))).Effectiveness);
    }

    [Fact]
    public async Task Capture_InitialBaseline_IsTakenEvenWhenSettingsWereSavedBeforeFirstCapture()
    {
        var h = new Harness([Score("AAA", 90m)]);
        await h.Service.SaveSettingsAsync(User, new SaveSignalsAuditSettingsRequest(true, 75m));

        Assert.Equal(1, (await h.Service.CaptureAsync(User)).Created);
        Assert.Equal(SignalOrigins.InitialBaseline, h.Db.SignalAuditEvents.Single().Origin);
    }

    [Fact]
    public async Task Capture_ThresholdIsStrictlyGreaterThan_76QualifiesButExactly75DoesNot()
    {
        var h = new Harness([Score("AAA", 10m)]);
        await h.Service.CaptureAsync(User); // initialise settings

        h.Scores.Items = [Score("AAA", 10m), Score("B76", 76m), Score("B75", 75m)];
        var result = await h.Service.CaptureAsync(User);

        Assert.Equal(1, result.Created);
        var ev = Assert.Single(h.Db.SignalAuditEvents);
        Assert.Equal("B76", ev.Symbol);
        Assert.Equal(76m, ev.Score);
        Assert.Equal(SignalTypeCatalog.PriorityCandidateKey, ev.SignalType);
        Assert.Equal(SignalSources.Watchlist, ev.Source);
        Assert.Equal("Strategic", ev.Role);
        Assert.Contains("\"portfolioNeed\"", ev.ContextJson);
    }

    [Fact]
    public async Task Capture_IsIdempotent_RepeatedRunsDoNotDuplicate()
    {
        var h = await Harness.WithNewQualifierAsync("NEW", 80m);
        await h.Service.CaptureAsync(User);
        await h.Service.CaptureAsync(User);

        Assert.Equal(1, h.Db.SignalAuditEvents.Count());
    }

    [Fact]
    public async Task Capture_ConcurrentRunsInProcess_ProduceOneEvent()
    {
        var h = await Harness.WithNewQualifierAsync("NEW", 80m, capture: false);
        await Task.WhenAll(h.Service.CaptureAsync(User), h.Service.CaptureAsync(User), h.Service.CaptureAsync(User));

        Assert.Equal(1, h.Db.SignalAuditEvents.Count());
    }

    [Fact]
    public async Task Capture_EpisodeEndsWhenScoreDrops_AndNewEpisodeCreatesNewEvent_PreservingHistory()
    {
        var h = await Harness.WithNewQualifierAsync("NEW", 80m);
        var first = h.Db.SignalAuditEvents.Single();
        Assert.Null(first.EpisodeEndedDate);

        h.Scores.Items = [Score("NEW", 70m, 55m)];
        var closed = await h.EodAsync();
        Assert.Equal(1, closed.Closed);
        Assert.NotNull(h.Db.SignalAuditEvents.Single().EpisodeEndedDate);

        h.Scores.Items = [Score("NEW", 88m, 61m)];
        var reopened = await h.Service.CaptureAsync(User);
        Assert.Equal(1, reopened.Created);

        var all = h.Db.SignalAuditEvents.OrderBy(e => e.Id).ToList();
        Assert.Equal(2, all.Count);
        Assert.Equal(50m, all[0].SignalPrice);      // original point-in-time facts untouched
        Assert.Equal(80m, all[0].Score);
        Assert.NotEqual(all[0].EpisodeKey, all[1].EpisodeKey);
    }

    [Fact]
    public async Task Capture_ScoreChangeWhileStillQualifying_DoesNotRewriteOrDuplicate()
    {
        var h = await Harness.WithNewQualifierAsync("NEW", 80m);
        h.Scores.Items = [Score("NEW", 95m, 99m)];
        await h.Service.CaptureAsync(User);

        var ev = Assert.Single(h.Db.SignalAuditEvents);
        Assert.Equal(80m, ev.Score);
        Assert.Equal(50m, ev.SignalPrice);
    }

    [Fact]
    public async Task Capture_WhenDisabled_ClosesOpenEpisodes_KeepsHistory_AndCreatesNothingNew()
    {
        var h = await Harness.WithNewQualifierAsync("NEW", 80m);
        await h.Service.SaveSettingsAsync(User, new SaveSignalsAuditSettingsRequest(false, 75m));

        h.Scores.Items = [Score("NEW", 90m), Score("OTHER", 99m)];
        var result = await h.EodAsync();

        Assert.Equal(0, result.Created);
        Assert.Equal(1, result.Closed);
        Assert.NotNull(Assert.Single(h.Db.SignalAuditEvents).EpisodeEndedDate);
    }

    [Fact]
    public async Task SaveSettings_Enabling_BaselinesCurrentQualifiers_SoHistoryIsNotFlooded()
    {
        var h = new Harness([Score("AAA", 10m)]);
        await h.Service.CaptureAsync(User);
        await h.Service.SaveSettingsAsync(User, new SaveSignalsAuditSettingsRequest(false, 75m));

        h.Scores.Items = [Score("X1", 90m), Score("X2", 80m), Score("LOW", 40m)];
        var saved = await h.Service.SaveSettingsAsync(User, new SaveSignalsAuditSettingsRequest(true, 75m));
        Assert.Equal(2, saved.BaselineCount);
        Assert.NotNull(saved.ActivatedAtUtc);

        var result = await h.Service.CaptureAsync(User);
        Assert.Equal(0, result.Created);

        // A baselined symbol that leaves and re-qualifies is a legitimate new signal.
        h.Scores.Items = [Score("X1", 50m), Score("X2", 80m)];
        await h.Service.CaptureAsync(User);
        h.Scores.Items = [Score("X1", 90m), Score("X2", 80m)];
        Assert.Equal(1, (await h.Service.CaptureAsync(User)).Created);
        Assert.Equal("X1", h.Db.SignalAuditEvents.Single().Symbol);
    }

    [Fact]
    public async Task SaveSettings_RejectsThresholdOutOfRange_AndPersistsAcrossInstances()
    {
        var h = new Harness([]);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            h.Service.SaveSettingsAsync(User, new SaveSignalsAuditSettingsRequest(true, 101m)));

        await h.Service.SaveSettingsAsync(User, new SaveSignalsAuditSettingsRequest(true, 80m));
        var fresh = h.NewService();
        Assert.Equal(80m, (await fresh.GetSettingsAsync(User)).PriorityThreshold);
    }

    [Fact]
    public async Task SaveAction_Actioned_WithMatchingTransaction_LinksAndVerifies()
    {
        var h = await Harness.WithNewQualifierAsync("NEW", 80m);
        var ev = h.Db.SignalAuditEvents.Single();
        var buy = h.AddTransaction("NEW", "OPEN", ev.SignalDate.AddDays(2));

        var candidates = await h.Service.GetTransactionCandidatesAsync(User, ev.Id);
        Assert.Equal(buy.Id, Assert.Single(candidates!).Id);

        var row = await h.Service.SaveActionAsync(User, ev.Id,
            new SaveSignalActionRequest(SignalMyActions.Actioned, "  followed the score  ", buy.Id));

        Assert.Equal(SignalMyActions.Actioned, row!.MyAction);
        Assert.Equal(buy.Id, row.LinkedTransactionId);
        Assert.Equal("TransactionVerified", row.LinkKind);
        Assert.Equal("followed the score", h.Db.SignalAuditEvents.Single().MyActionNote);
        Assert.Equal(1, h.Db.PortfolioItems.Count()); // never creates transactions
    }

    [Fact]
    public async Task SaveAction_ActionedWithoutLink_IsManualConfirmed_AndNoMatchReturnsEmptyCandidates()
    {
        var h = await Harness.WithNewQualifierAsync("NEW", 80m);
        var ev = h.Db.SignalAuditEvents.Single();

        Assert.Empty((await h.Service.GetTransactionCandidatesAsync(User, ev.Id))!);
        var row = await h.Service.SaveActionAsync(User, ev.Id, new SaveSignalActionRequest(SignalMyActions.Partial, null, null));
        Assert.Equal("ManualConfirmed", row!.LinkKind);
    }

    [Fact]
    public async Task SaveAction_RejectsUnrelatedTransaction_InvalidStatus_AndOtherUsersSignals()
    {
        var h = await Harness.WithNewQualifierAsync("NEW", 80m);
        var ev = h.Db.SignalAuditEvents.Single();
        var unrelated = h.AddTransaction("OTHER", "OPEN", ev.SignalDate);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            h.Service.SaveActionAsync(User, ev.Id, new SaveSignalActionRequest(SignalMyActions.Actioned, null, unrelated.Id)));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            h.Service.SaveActionAsync(User, ev.Id, new SaveSignalActionRequest("Maybe", null, null)));
        Assert.Null(await h.Service.SaveActionAsync("someone-else", ev.Id, new SaveSignalActionRequest(SignalMyActions.Pending, null, null)));
    }

    [Fact]
    public async Task SaveAction_ChangingToNotActioned_RemovesTransactionLink()
    {
        var h = await Harness.WithNewQualifierAsync("NEW", 80m);
        var ev = h.Db.SignalAuditEvents.Single();
        var buy = h.AddTransaction("NEW", "OPEN", ev.SignalDate);
        await h.Service.SaveActionAsync(User, ev.Id, new SaveSignalActionRequest(SignalMyActions.Actioned, null, buy.Id));

        var row = await h.Service.SaveActionAsync(User, ev.Id, new SaveSignalActionRequest(SignalMyActions.NotActioned, "too extended", buy.Id));

        Assert.Null(row!.LinkedTransactionId);
        Assert.Equal("None", row.LinkKind);
    }

    [Fact]
    public async Task Query_FiltersSortsPagesAndFlagsFutureDatedSignals()
    {
        var h = new Harness([]);
        var today = SignalsAuditService_TorontoToday();
        h.AddEvent("AAA", today.AddDays(-30), "Core", SignalMyActions.Pending);
        h.AddEvent("BBB", today.AddDays(-10), "Swing", SignalMyActions.Actioned);
        h.AddEvent("CCC", today.AddDays(5), "Core", SignalMyActions.Pending); // future-dated anomaly
        h.Market.Series["AAA"] = [.. Enumerable.Range(0, 45).Select(i => new MarketDailyClose(today.AddDays(-30 + i), 100m + i))];

        var all = await h.Service.QueryAsync(User, new SignalsAuditQuery());
        Assert.Equal(["CCC", "BBB", "AAA"], all.Items.Select(i => i.Symbol));
        Assert.True(all.Items[0].DateAnomaly);
        Assert.Equal(-5, all.Items[0].DaysSince);
        Assert.Equal(30, all.Items[2].DaysSince);

        Assert.Equal(["AAA", "CCC"], (await h.Service.QueryAsync(User, new SignalsAuditQuery(Role: ["Core"], SortBy: "symbol", SortDir: "asc"))).Items.Select(i => i.Symbol));
        Assert.Equal(["BBB"], (await h.Service.QueryAsync(User, new SignalsAuditQuery(MyAction: "Actioned"))).Items.Select(i => i.Symbol));
        Assert.Equal(["BBB"], (await h.Service.QueryAsync(User, new SignalsAuditQuery(Ticker: "bb"))).Items.Select(i => i.Symbol));
        Assert.Single((await h.Service.QueryAsync(User, new SignalsAuditQuery(From: today.AddDays(-20).ToString("yyyy-MM-dd"), To: today.ToString("yyyy-MM-dd")))).Items);
        Assert.Single((await h.Service.QueryAsync(User, new SignalsAuditQuery(PageSize: 1, Page: 2))).Items);

        var aaa = all.Items.Single(i => i.Symbol == "AAA");
        Assert.Equal(SignalReturnCalculator.Complete, aaa.Return5D.Status);
        Assert.Equal(SignalReturnCalculator.NotAvailable, all.Items.Single(i => i.Symbol == "BBB").Return5D.Status);
    }

    [Fact]
    public async Task Query_DoesNotLeakOtherUsersSignals()
    {
        var h = new Harness([]);
        h.AddEvent("AAA", SignalsAuditService_TorontoToday(), "Core", SignalMyActions.Pending, user: "someone-else");
        Assert.Empty((await h.Service.QueryAsync(User, new SignalsAuditQuery())).Items);
        Assert.Null(await h.Service.GetDetailAsync(User, h.Db.SignalAuditEvents.Single().Id));
    }

    [Fact]
    public async Task Summary_ReflectsFilters_AndComputesEffectivenessWithObservationCounts()
    {
        var h = new Harness([]);
        var start = new DateOnly(2026, 1, 5);
        h.AddEvent("UP", start, "Core", SignalMyActions.Actioned, price: 100m);
        h.AddEvent("DOWN", start, "Core", SignalMyActions.Pending, price: 100m);
        h.AddEvent("NEWER", SignalsAuditService_TorontoToday(), "Swing", SignalMyActions.Pending, price: 100m);
        h.Market.Series["UP"] = Series(start, 40, i => 100m + i);
        h.Market.Series["DOWN"] = Series(start, 40, i => 100m - i);

        var summary = await h.Service.GetSummaryAsync(User, new SignalsAuditQuery());
        Assert.Equal(3, summary.Total);
        Assert.Equal(3, summary.PriorityCandidate);
        Assert.Equal(3, summary.EntryAdd);
        Assert.Equal(0, summary.TrimExit);
        Assert.Equal(1, summary.Actioned);
        Assert.Equal(2, summary.PendingReview);

        var row = Assert.Single(summary.Effectiveness);
        Assert.Equal(3, row.Count);
        Assert.Equal(2, row.Observations5D);   // NEWER is incomplete -> excluded, not zero
        Assert.Equal(0m, row.Avg5D);           // +5% and -5%
        Assert.Equal(2, row.Observations20D);
        Assert.Equal(50m, row.FavourablePct);  // buy: 1 of 2 positive
        Assert.Equal(1, row.ActionedCount);

        var filtered = await h.Service.GetSummaryAsync(User, new SignalsAuditQuery(Role: ["Swing"]));
        Assert.Equal(1, filtered.Total);
        Assert.Equal(0, filtered.Actioned);
    }

    [Fact]
    public async Task SignalTypes_IncludeCatalogAndHistoricalDisabledTypes()
    {
        var h = new Harness([]);
        var ev = h.AddEvent("AAA", SignalsAuditService_TorontoToday(), "Core", SignalMyActions.Pending);
        ev.SignalType = "LEGACY_DISABLED_ACTION";
        await h.Db.SaveChangesAsync();

        var types = await h.Service.GetSignalTypesAsync(User);
        Assert.Contains(types, t => t.Key == SignalTypeCatalog.PriorityCandidateKey);
        Assert.Contains(types, t => t.Key == "LEGACY_DISABLED_ACTION" && t.Direction == SignalDirections.Neutral);
    }

    // ── Priority eligibility (same rules as the Action Center widget) ────────

    [Fact]
    public async Task Capture_PriorityCandidate_RequiresActionCenterEligibility_NotJustScore()
    {
        var h = new Harness([Score("BASE", 10m)]);
        await h.Service.CaptureAsync(User);

        h.Scores.Items = [Score("BASE", 10m), Score("WAIT", 90m), Score("OK", 90m)];
        h.FinalActions.WatchlistAction["WAIT"] = "WAIT FOR PULLBACK"; // ineligible per widget rules
        await h.Service.CaptureAsync(User);

        var ev = Assert.Single(h.Db.SignalAuditEvents.Where(e => e.SignalType == SignalTypeCatalog.PriorityCandidateKey));
        Assert.Equal("OK", ev.Symbol);
        Assert.Equal(SignalEligibility.Verified, ev.EligibilityStatus);
    }

    [Fact]
    public async Task Capture_PriorityCandidate_ExcludedByHardStructuralNegative_AndScoreBelow50()
    {
        var h = new Harness([Score("BASE", 10m)]);
        await h.Service.CaptureAsync(User);
        await h.Service.SaveSettingsAsync(User, new SaveSignalsAuditSettingsRequest(true, 40m));

        h.FinalActions.HardNegative = true;
        h.Scores.Items = [Score("BASE", 10m), Score("DMG", 90m)];
        await h.Service.CaptureAsync(User);
        Assert.Empty(h.Db.SignalAuditEvents);

        h.FinalActions.HardNegative = false;
        h.Scores.Items = [Score("BASE", 10m), Score("LOW", 45m)]; // > 40 threshold but < 50 widget floor
        await h.Service.CaptureAsync(User);
        Assert.Empty(h.Db.SignalAuditEvents);
    }

    [Fact]
    public async Task Capture_IsSkipped_WhenFinalActionEvaluationFails_SoNothingIsFalselyClosed()
    {
        var h = await Harness.WithNewQualifierAsync("NEW", 80m);
        h.FinalActions.Fail = true;
        var result = await h.Service.CaptureAsync(User);

        Assert.True(result.Skipped);
        Assert.Null(h.Db.SignalAuditEvents.Single().EpisodeEndedDate);
    }

    // ── Final Action list: defaults, add / edit / delete ─────────────────────

    [Fact]
    public async Task Settings_DefaultToTheSixBusinessActions_WithRulesBackedFlags()
    {
        var h = new Harness([]);
        var entries = (await h.Service.GetSettingsAsync(User)).Entries;

        Assert.Equal(["STARTER ENTRY", "ADD/STAGGED ENTRY", "SMALL ADD", "TRIM/EXIT", "TRIM/REVIEW", "CUT LOSS/EXIT"], entries.Select(e => e.Label));
        Assert.All(entries, e => Assert.True(e.Enabled));
        Assert.Equal("ADD/STAGED ENTRY", entries[1].Id);              // spelling variant maps to the id the rules emit
        Assert.True(entries[1].KnownToRules);
        Assert.False(entries[3].KnownToRules);                        // TRIM/EXIT is not an action the rules emit
        Assert.Equal(SignalCategories.TrimExit, entries[4].Category);
        Assert.Equal(SignalDirections.Sell, entries[5].Direction);
    }

    [Fact]
    public async Task Settings_AddEditDeleteEntries_PersistAndValidate()
    {
        var h = new Harness([]);
        var current = (await h.Service.GetSettingsAsync(User)).Entries
            .Select(e => new AuditedActionEntry(e.Id, e.Label, e.Enabled, e.Custom)).ToList();

        // add (from catalog and custom), edit (rename + disable), delete
        current.RemoveAll(e => e.Id == "SMALL ADD");
        current.Add(new AuditedActionEntry("BUY WATCH", "Buy Watch", true, false));
        current.Add(new AuditedActionEntry("", "  my own action  ", true, true));
        current[0] = current[0] with { Label = "Starter (mine)", Enabled = false };

        var saved = await h.Service.SaveSettingsAsync(User, new SaveSignalsAuditSettingsRequest(true, 75m, true, current));
        Assert.Equal(7, saved.Entries.Count);
        Assert.DoesNotContain(saved.Entries, e => e.Id == "SMALL ADD");
        var starter = saved.Entries.Single(e => e.Id == "STARTER ENTRY");
        Assert.Equal("Starter (mine)", starter.Label);
        Assert.False(starter.Enabled);
        var custom = saved.Entries.Single(e => e.Custom && e.Id == "MY OWN ACTION");
        Assert.False(custom.KnownToRules);

        var reloaded = await h.NewService().GetSettingsAsync(User);
        Assert.Equal(saved.Entries.Select(e => e.Id), reloaded.Entries.Select(e => e.Id));

        await Assert.ThrowsAsync<ArgumentException>(() => h.Service.SaveSettingsAsync(User,
            new SaveSignalsAuditSettingsRequest(true, 75m, true, [new("A", "dup", true, true), new("", "DUP", true, true)])));
        await Assert.ThrowsAsync<ArgumentException>(() => h.Service.SaveSettingsAsync(User,
            new SaveSignalsAuditSettingsRequest(true, 75m, true, [new("", "   ", true, true)])));
    }

    // ── Final Action capture ─────────────────────────────────────────────────

    private static async Task<Harness> WithFinalActionAsync(string symbol, string action)
    {
        var h = new Harness([Score("BASE", 10m)]);
        await h.Service.CaptureAsync(User);                           // first run: nothing live yet
        h.FinalActions.ExtraWatchlist.Add(Result(symbol, action, SignalSources.Watchlist, 50m));
        return h;
    }

    [Fact]
    public async Task FinalActionCapture_SymbolBothHeldAndWatched_CapturesTheHoldingsAction()
    {
        var h = await WithFinalActionAsync("EFR", "WAIT FOR RECLAIM");
        h.FinalActions.ExtraWatchlist.Add(Result("EFR", "CUT LOSS/EXIT", SignalSources.Portfolio, 50m));
        await h.Service.CaptureAsync(User);

        var ev = Assert.Single(h.Db.SignalAuditEvents);
        Assert.Equal("FA:CUT LOSS/EXIT", ev.SignalType);
        Assert.Equal(SignalSources.Portfolio, ev.Source);
    }

    [Fact]
    public async Task FinalActionCapture_FirstRun_RecordsExistingActionsAsInitialBaseline_ThenOnlyTransitions()
    {
        var h = new Harness([Score("BASE", 10m)]);
        h.FinalActions.ExtraWatchlist.Add(Result("OLD", "STARTER ENTRY", SignalSources.Watchlist, 44m));
        await h.Service.CaptureAsync(User);
        await h.Service.CaptureAsync(User);

        var baseline = Assert.Single(h.Db.SignalAuditEvents);
        Assert.Equal("FA:STARTER ENTRY", baseline.SignalType);
        Assert.Equal(SignalOrigins.InitialBaseline, baseline.Origin);
        Assert.Equal(44m, baseline.SignalPrice);

        h.FinalActions.ExtraWatchlist.Add(Result("NEWER", "SMALL ADD", SignalSources.Portfolio, 12m));
        await h.Service.CaptureAsync(User);
        Assert.Equal(SignalOrigins.Live, h.Db.SignalAuditEvents.Single(e => e.Symbol == "NEWER").Origin);
        Assert.Equal(2, h.Db.SignalAuditEvents.Count());
    }

    [Fact]
    public async Task FinalActionCapture_EnabledAction_CreatesOneEvent_WithOriginalPriceAndMetadata_Idempotently()
    {
        var h = await WithFinalActionAsync("FOO", "STARTER ENTRY");
        await h.Service.CaptureAsync(User);
        await h.Service.CaptureAsync(User);

        var ev = Assert.Single(h.Db.SignalAuditEvents);
        Assert.Equal("FA:STARTER ENTRY", ev.SignalType);
        Assert.Equal(SignalCategories.Entry, ev.SignalCategory);
        Assert.Equal(SignalDirections.Buy, ev.Direction);
        Assert.Equal(50m, ev.SignalPrice);
        Assert.Equal("Strategic", ev.Role);
        Assert.Equal(SignalSources.Watchlist, ev.Source);
        Assert.Contains("\"finalActionId\":\"STARTER ENTRY\"", ev.ContextJson);

        h.FinalActions.ExtraWatchlist[0] = Result("FOO", "STARTER ENTRY", SignalSources.Watchlist, 99m);
        await h.Service.CaptureAsync(User);
        Assert.Equal(50m, h.Db.SignalAuditEvents.Single().SignalPrice); // never rewritten
    }

    [Fact]
    public async Task FinalActionCapture_DisablingAuditedAction_DoesNotCloseTheRecommendation_AndItStaysOpen()
    {
        var h = await WithFinalActionAsync("FOO", "WAIT FOR PULLBACK"); // not actionable, not audited
        await h.Service.CaptureAsync(User);
        Assert.Empty(h.Db.SignalAuditEvents);

        h.FinalActions.ExtraWatchlist[0] = Result("FOO", "STARTER ENTRY", SignalSources.Watchlist, 50m);
        await h.Service.CaptureAsync(User);
        Assert.Single(h.Db.SignalAuditEvents);

        var entries = (await h.Service.GetSettingsAsync(User)).Entries
            .Select(e => new AuditedActionEntry(e.Id, e.Label, e.Id != "STARTER ENTRY" && e.Enabled, e.Custom)).ToList();
        await h.Service.SaveSettingsAsync(User, new SaveSignalsAuditSettingsRequest(true, 75m, true, entries));
        await h.EodAsync();

        // Auditing is analytics configuration: the condition still holds, so the recommendation stays open.
        var ev = Assert.Single(h.Db.SignalAuditEvents);
        Assert.Null(ev.EpisodeEndedDate);
        Assert.Equal(SignalStatuses.Active, ev.Status);
        Assert.True(ev.PerformanceAudited);                            // history flag is never rewritten
    }

    [Fact]
    public async Task FinalActionCapture_TransitionToAnotherEnabledAction_ClosesOldAndCreatesNew()
    {
        var h = await WithFinalActionAsync("HOLD", "STARTER ENTRY");
        await h.EodAsync();

        h.FinalActions.ExtraWatchlist[0] = Result("HOLD", "Small Add", SignalSources.Portfolio, 51m);
        await h.EodAsync();
        h.FinalActions.ExtraWatchlist[0] = Result("HOLD", "WAIT FOR PULLBACK", SignalSources.Portfolio, 52m);
        await h.EodAsync();
        h.FinalActions.ExtraWatchlist[0] = Result("HOLD", "STARTER ENTRY", SignalSources.Portfolio, 53m);
        await h.EodAsync();

        var events = h.Db.SignalAuditEvents.OrderBy(e => e.Id).ToList();
        Assert.Equal(["FA:STARTER ENTRY", "FA:SMALL ADD", "FA:STARTER ENTRY"], events.Select(e => e.SignalType));
        Assert.Equal([50m, 51m, 53m], events.Select(e => e.SignalPrice));
        Assert.All(events.Take(2), e => Assert.NotNull(e.EpisodeEndedDate));
        Assert.Null(events[2].EpisodeEndedDate);
        Assert.Equal(3, events.Select(e => e.EpisodeKey).Distinct().Count());
    }

    [Fact]
    public async Task FinalActionCapture_EnablingAnAction_DoesNotFloodWithStocksAlreadyShowingIt()
    {
        var h = new Harness([Score("BASE", 10m)]);
        await h.Service.CaptureAsync(User);
        h.FinalActions.ExtraWatchlist.Add(Result("OLD", "BUY WATCH", SignalSources.Watchlist, 40m));
        await h.Service.CaptureAsync(User);
        Assert.Empty(h.Db.SignalAuditEvents);                          // BUY WATCH not audited yet

        var entries = (await h.Service.GetSettingsAsync(User)).Entries
            .Select(e => new AuditedActionEntry(e.Id, e.Label, e.Enabled, e.Custom)).ToList();
        entries.Add(new AuditedActionEntry("BUY WATCH", "BUY WATCH", true, false));
        var saved = await h.Service.SaveSettingsAsync(User, new SaveSignalsAuditSettingsRequest(true, 75m, true, entries));
        Assert.Equal(2, saved.FinalActionBaselineCount);               // OLD plus the BASE fixture symbol, both already BUY WATCH

        await h.Service.CaptureAsync(User);
        Assert.Empty(h.Db.SignalAuditEvents);                          // baseline: needs a legitimate new transition

        h.FinalActions.ExtraWatchlist[0] = Result("OLD", "WAIT FOR PULLBACK", SignalSources.Watchlist, 41m);
        await h.Service.CaptureAsync(User);
        h.FinalActions.ExtraWatchlist[0] = Result("OLD", "BUY WATCH", SignalSources.Watchlist, 42m);
        await h.Service.CaptureAsync(User);
        Assert.Equal(42m, Assert.Single(h.Db.SignalAuditEvents).SignalPrice);
    }

    [Fact]
    public async Task FinalActionCapture_CustomEntryMatchesRulesOutputById()
    {
        var h = new Harness([Score("BASE", 10m)]);
        await h.Service.CaptureAsync(User);
        var entries = (await h.Service.GetSettingsAsync(User)).Entries
            .Select(e => new AuditedActionEntry(e.Id, e.Label, e.Enabled, e.Custom)).ToList();
        entries.Add(new AuditedActionEntry("", "avoid  /  wait", true, true)); // user-typed spelling
        await h.Service.SaveSettingsAsync(User, new SaveSignalsAuditSettingsRequest(true, 75m, true, entries));

        h.FinalActions.ExtraWatchlist.Add(Result("X", "Avoid / Wait", SignalSources.Watchlist, 10m));
        await h.Service.CaptureAsync(User);

        var ev = Assert.Single(h.Db.SignalAuditEvents);
        Assert.Equal("FA:AVOID/WAIT", ev.SignalType);                  // typed "avoid  /  wait" resolved to the canonical id
        Assert.Equal(10m, ev.SignalPrice);
    }

    // ── Price basis (splits) ─────────────────────────────────────────────────

    [Fact]
    public void ResolveBasis_FlagsSplitAdjustedHistory_AndScalesTheSignalPrice()
    {
        var date = new DateOnly(2026, 3, 6);
        var ev = new SignalAuditEvent { SignalDate = date, SignalPrice = 100m, BasisClose = 100m };

        // unchanged history
        var ok = SignalsAuditService.ResolveBasis(ev, [new MarketDailyClose(date, 100.2m)]);
        Assert.Equal((100m, SignalPriceBasis.Verified), ok);

        // 2-for-1 split since capture: provider now reports 50 for that day
        var split = SignalsAuditService.ResolveBasis(ev, [new MarketDailyClose(date, 50m)]);
        Assert.Equal(SignalPriceBasis.Adjusted, split.Basis);
        Assert.Equal(50m, split.Price);

        // no reference close stored, or none available for the date
        Assert.Equal(SignalPriceBasis.Unverified, SignalsAuditService.ResolveBasis(new SignalAuditEvent { SignalDate = date, SignalPrice = 100m }, [new MarketDailyClose(date, 100m)]).Basis);
        Assert.Equal(SignalPriceBasis.Unverified, SignalsAuditService.ResolveBasis(ev, [new MarketDailyClose(date.AddDays(1), 100m)]).Basis);
        Assert.Equal(SignalPriceBasis.Unverified, SignalsAuditService.ResolveBasis(ev, null).Basis);
    }

    [Fact]
    public async Task Query_UsesAdjustedBasisForReturns_AndExposesBasisAndEligibility()
    {
        var h = new Harness([]);
        var start = new DateOnly(2026, 1, 5);
        var ev = h.AddEvent("SPLIT", start, "Core", SignalMyActions.Pending, price: 100m);
        ev.BasisClose = 100m;
        ev.EligibilityStatus = SignalEligibility.Verified;
        await h.Db.SaveChangesAsync();
        h.Market.Series["SPLIT"] = Series(start, 40, i => 50m + i / 2m); // history re-based by a 2:1 split

        var row = (await h.Service.QueryAsync(User, new SignalsAuditQuery())).Items.Single();
        Assert.Equal(SignalPriceBasis.Adjusted, row.PriceBasis);
        Assert.Equal(SignalEligibility.Verified, row.EligibilityStatus);
        Assert.Equal(100m, row.SignalPrice);                           // original recorded price is preserved
        Assert.InRange(row.Return5D.Pct!.Value, 4m, 6m);               // ~+5% on the adjusted baseline, not -47%
    }

    [Fact]
    public async Task LegacyEvents_AreLabelledUnverified()
    {
        var h = new Harness([]);
        h.AddEvent("OLD", SignalsAuditService_TorontoToday(), "Core", SignalMyActions.Pending);
        var row = (await h.Service.QueryAsync(User, new SignalsAuditQuery())).Items.Single();
        Assert.Equal(SignalEligibility.Unverified, row.EligibilityStatus);
        Assert.Equal(SignalPriceBasis.Unverified, row.PriceBasis);
    }

    [Fact]
    public async Task SignalTypes_ListConfiguredFinalActions_WithCatalogMetadata()
    {
        var h = new Harness([]);
        var types = await h.Service.GetSignalTypesAsync(User);
        var trim = Assert.Single(types, t => t.Key == "FA:TRIM/REVIEW");
        Assert.Equal(SignalCategories.TrimExit, trim.Category);
        Assert.Equal(SignalDirections.Sell, trim.Direction);
        Assert.Contains(types, t => t.Key == SignalTypeCatalog.PriorityCandidateKey);
    }

    [Fact]
    public void SnapDate_MovesWeekendSignalsToFriday()
    {
        Assert.Equal(new DateOnly(2026, 3, 6), SignalsAuditService.SnapDate(new DateOnly(2026, 3, 7)));
        Assert.Equal(new DateOnly(2026, 3, 6), SignalsAuditService.SnapDate(new DateOnly(2026, 3, 8)));
        Assert.Equal(new DateOnly(2026, 3, 9), SignalsAuditService.SnapDate(new DateOnly(2026, 3, 9)));
    }

    private static DateOnly SignalsAuditService_TorontoToday() => SignalsAuditService.TorontoToday();

    private static List<MarketDailyClose> Series(DateOnly start, int count, Func<int, decimal> price)
    {
        var list = new List<MarketDailyClose>();
        for (var d = start; list.Count < count; d = d.AddDays(1))
            if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
                list.Add(new MarketDailyClose(d, price(list.Count)));
        return list;
    }

    // ── Lifecycle (Phase 1): evaluation outcomes, identity, status, audit flag ────────

    private static async Task<(Harness H, SignalAuditEvent Ev)> OpenRecommendationAsync(string symbol = "FOO", string action = "STARTER ENTRY")
    {
        var h = await WithFinalActionAsync(symbol, action);
        await h.EodAsync();
        return (h, h.Db.SignalAuditEvents.Single(e => e.Symbol == symbol));
    }

    [Fact]
    public async Task Eod_ConfirmedRecommendation_IsRevalidated_NotDuplicated()
    {
        var (h, ev) = await OpenRecommendationAsync();
        Assert.NotNull(ev.LastValidatedAtUtc);
        var first = ev.LastValidatedAtUtc;
        await Task.Delay(5);
        var again = await h.EodAsync();
        Assert.Equal(0, again.Created);
        Assert.Equal(0, again.Closed);
        Assert.Single(h.Db.SignalAuditEvents);
        Assert.True(h.Db.SignalAuditEvents.Single().LastValidatedAtUtc > first);
    }

    [Fact]
    public async Task Eod_CompleteEvaluationThatNoLongerProducesTheAction_InvalidatesImmediately()
    {
        var (h, _) = await OpenRecommendationAsync();
        h.FinalActions.ExtraWatchlist[0] = Result("FOO", "WAIT FOR PULLBACK", SignalSources.Watchlist, 50m);
        await h.EodAsync();

        var ev = h.Db.SignalAuditEvents.Single();
        Assert.Equal(SignalStatuses.Invalidated, ev.Status);
        Assert.NotNull(ev.EpisodeEndedDate);
        Assert.Equal(50m, ev.SignalPrice);
    }

    [Fact]
    public async Task Eod_UnknownSymbol_PreservesRecommendation_NeverInvalidates()
    {
        var (h, _) = await OpenRecommendationAsync();
        h.FinalActions.ExtraWatchlist.Clear();           // scan failed: no result for FOO
        h.FinalActions.Unknown.Add("FOO");
        var result = await h.EodAsync();

        var ev = h.Db.SignalAuditEvents.Single();
        Assert.Equal(0, result.Closed);
        Assert.Equal(SignalStatuses.Active, ev.Status);
        Assert.Null(ev.EpisodeEndedDate);
    }

    [Fact]
    public async Task Eod_PartialRun_OnlyEvaluatedSecuritiesChange()
    {
        var (h, _) = await OpenRecommendationAsync("AAA");
        h.FinalActions.ExtraWatchlist.Add(Result("BBB", "STARTER ENTRY", SignalSources.Watchlist, 20m));
        await h.EodAsync();

        h.FinalActions.ExtraWatchlist.Clear();
        h.FinalActions.ExtraWatchlist.Add(Result("BBB", "WAIT FOR PULLBACK", SignalSources.Watchlist, 20m));
        h.FinalActions.Unknown.Add("AAA");
        await h.EodAsync();

        Assert.Equal(SignalStatuses.Active, h.Db.SignalAuditEvents.Single(e => e.Symbol == "AAA").Status);
        Assert.Equal(SignalStatuses.Invalidated, h.Db.SignalAuditEvents.Single(e => e.Symbol == "BBB").Status);
    }

    [Fact]
    public async Task Eod_IncompletePass_AndManualCapture_NeverCloseOrRevalidate()
    {
        var (h, ev) = await OpenRecommendationAsync();
        var validated = ev.LastValidatedAtUtc;
        h.FinalActions.ExtraWatchlist[0] = Result("FOO", "WAIT FOR PULLBACK", SignalSources.Watchlist, 50m);

        await h.EodAsync(complete: false);
        await h.Service.CaptureAsync(User);

        ev = h.Db.SignalAuditEvents.Single();
        Assert.Equal(SignalStatuses.Active, ev.Status);
        Assert.Null(ev.EpisodeEndedDate);
        Assert.Equal(validated, ev.LastValidatedAtUtc);
    }

    [Fact]
    public async Task Eod_StalePortfolioSnapshot_DoesNotInvalidateHoldings()
    {
        var h = new Harness([Score("BASE", 10m)]);
        await h.Service.CaptureAsync(User);
        h.FinalActions.ExtraWatchlist.Add(Result("HLD", "Small Add", SignalSources.Portfolio, 30m));
        await h.EodAsync();
        h.FinalActions.ExtraWatchlist[0] = Result("HLD", "WAIT FOR PULLBACK", SignalSources.Portfolio, 30m);

        var snap = await h.FinalActions.ComputeAsync(User);
        var stale = snap with { Meta = snap.Meta! with { PortfolioSnapshotAtUtc = DateTime.UtcNow.AddHours(-30) } };
        await h.Service.CaptureAsync(User, stale, new CaptureOptions(true, true, DateTime.UtcNow.AddHours(-1)));

        Assert.Equal(SignalStatuses.Active, h.Db.SignalAuditEvents.Single().Status);
    }

    [Fact]
    public async Task Eod_ChangedAction_SupersedesPredecessor_AndKeepsOriginalFacts()
    {
        var h = new Harness([Score("BASE", 10m)]);
        await h.Service.CaptureAsync(User);
        h.FinalActions.ExtraWatchlist.Add(Result("HLD", "Small Add", SignalSources.Portfolio, 30m));
        await h.EodAsync();
        h.FinalActions.ExtraWatchlist[0] = Result("HLD", "Cut Loss / Exit", SignalSources.Portfolio, 25m);
        await h.EodAsync();

        var all = h.Db.SignalAuditEvents.OrderBy(e => e.Id).ToList();
        Assert.Equal(2, all.Count);
        Assert.Equal(SignalStatuses.Superseded, all[0].Status);
        Assert.Equal(all[1].Id, all[0].SupersededByEventId);
        Assert.Equal(30m, all[0].SignalPrice);
        Assert.Equal(SignalStatuses.Active, all[1].Status);
        Assert.Null(all[1].EpisodeEndedDate);
    }

    [Fact]
    public async Task Eod_HoldingAndWatchlistOfSameAction_AreDistinctContexts()
    {
        var h = new Harness([Score("BASE", 10m)]);
        await h.Service.CaptureAsync(User);
        h.FinalActions.ExtraWatchlist.Add(Result("DUP", "STARTER ENTRY", SignalSources.Watchlist, 10m));
        h.FinalActions.ExtraWatchlist.Add(Result("DUP", "STARTER ENTRY", SignalSources.Portfolio, 10m));
        await h.EodAsync();
        await h.EodAsync();

        Assert.Equal(2, h.Db.SignalAuditEvents.Count());
    }

    [Fact]
    public async Task Eod_InitialBaselineRecord_IsRevalidatedInPlace_WithoutResettingHistory()
    {
        var h = new Harness([Score("BASE", 10m)]);
        h.FinalActions.ExtraWatchlist.Add(Result("OLD", "STARTER ENTRY", SignalSources.Watchlist, 44m));
        await h.Service.CaptureAsync(User);                      // first run => INITIAL_BASELINE
        var baseline = h.Db.SignalAuditEvents.Single();
        Assert.Null(baseline.LastValidatedAtUtc);                 // hidden from Action Center until revalidated
        var (id, date, price) = (baseline.Id, baseline.SignalDate, baseline.SignalPrice);

        await h.EodAsync();

        var after = Assert.Single(h.Db.SignalAuditEvents);
        Assert.Equal((id, date, price), (after.Id, after.SignalDate, after.SignalPrice));
        Assert.Equal(SignalOrigins.InitialBaseline, after.Origin);
        Assert.NotNull(after.LastValidatedAtUtc);
    }

    [Fact]
    public async Task Capture_UnauditedActionableRecommendation_IsRecorded_ButExcludedFromPerformanceStats()
    {
        var h = await WithFinalActionAsync("TRM", "Trim / Review");
        // TRIM/REVIEW is a default audited action; remove it from the audited list.
        var entries = (await h.Service.GetSettingsAsync(User)).Entries
            .Select(e => new AuditedActionEntry(e.Id, e.Label, e.Id != "TRIM/REVIEW" && e.Enabled, e.Custom)).ToList();
        await h.Service.SaveSettingsAsync(User, new SaveSignalsAuditSettingsRequest(true, 75m, true, entries));
        h.FinalActions.ExtraWatchlist.Clear();
        h.FinalActions.ExtraWatchlist.Add(Result("TRM", "Trim / Review", SignalSources.Portfolio, 60m));
        await h.EodAsync();

        var ev = Assert.Single(h.Db.SignalAuditEvents);
        Assert.False(ev.PerformanceAudited);
        Assert.Equal(SignalStatuses.Active, ev.Status);
        Assert.NotNull(ev.LastValidatedAtUtc);

        Assert.Equal(0, (await h.Service.QueryAsync(User, new SignalsAuditQuery())).Total);
        Assert.Equal(1, (await h.Service.QueryAsync(User, new SignalsAuditQuery(IncludeUnaudited: true))).Total);
        Assert.Empty((await h.Service.GetSummaryAsync(User, new SignalsAuditQuery(IncludeUnaudited: true, IncludeBaselineInStats: true))).Effectiveness);
    }

    [Fact]
    public async Task Dismiss_IsExplicit_AndNotActionedStaysActive()
    {
        var (h, ev) = await OpenRecommendationAsync();

        await h.Service.SaveActionAsync(User, ev.Id, new SaveSignalActionRequest(SignalMyActions.NotActioned, null, null));
        Assert.Equal(SignalStatuses.Active, h.Db.SignalAuditEvents.Single().Status);
        await h.Service.SaveActionAsync(User, ev.Id, new SaveSignalActionRequest(SignalMyActions.Partial, null, null));
        Assert.Equal(SignalStatuses.Active, h.Db.SignalAuditEvents.Single().Status);

        await h.Service.DismissAsync(User, ev.Id, "not now");
        Assert.Equal(SignalStatuses.Dismissed, h.Db.SignalAuditEvents.Single().Status);
        Assert.Equal(SignalMyActions.Partial, h.Db.SignalAuditEvents.Single().MyAction);   // user response untouched
        await Assert.ThrowsAsync<ArgumentException>(() => h.Service.DismissAsync(User, ev.Id, null));
    }

    [Fact]
    public async Task SaveAction_Actioned_MovesActiveToActed_AndLaterEvaluationDoesNotOverwriteIt()
    {
        var (h, ev) = await OpenRecommendationAsync();
        await h.Service.SaveActionAsync(User, ev.Id, new SaveSignalActionRequest(SignalMyActions.Actioned, null, null));
        Assert.Equal(SignalStatuses.Acted, h.Db.SignalAuditEvents.Single().Status);

        h.FinalActions.ExtraWatchlist[0] = Result("FOO", "WAIT FOR PULLBACK", SignalSources.Watchlist, 50m);
        await h.EodAsync();

        var after = h.Db.SignalAuditEvents.Single();
        Assert.Equal(SignalStatuses.Acted, after.Status);
        Assert.Equal(SignalMyActions.Actioned, after.MyAction);
        Assert.NotNull(after.EpisodeEndedDate);               // the condition ended; the user's status is kept
    }

    [Fact]
    public async Task Expiry_OnlyAppliesToSecuritiesLeftUnevaluatedInACompletePass_AfterTheWindow()
    {
        var (h, ev) = await OpenRecommendationAsync();
        ev.CapturedAtUtc = DateTime.UtcNow.AddDays(-20);
        ev.LastValidatedAtUtc = DateTime.UtcNow.AddDays(-20);
        await h.Db.SaveChangesAsync();
        h.FinalActions.ExtraWatchlist.Clear();
        h.FinalActions.Unknown.Add("FOO");

        await h.EodAsync(complete: false);                    // outage: never expires
        Assert.Equal(SignalStatuses.Active, h.Db.SignalAuditEvents.Single().Status);

        await h.EodAsync();                                   // complete pass, FOO still unevaluated past 5 days
        Assert.Equal(SignalStatuses.Expired, h.Db.SignalAuditEvents.Single().Status);
    }

    [Theory]
    [InlineData("STARTER ENTRY", SignalSources.Watchlist, DecisionBucket.EntryAdd)]
    [InlineData("Add / Staged Entry", SignalSources.Portfolio, DecisionBucket.EntryAdd)]
    [InlineData("Small Add", SignalSources.Portfolio, DecisionBucket.EntryAdd)]
    [InlineData("Trim / Review", SignalSources.Portfolio, DecisionBucket.TrimExit)]
    [InlineData("Exit / Take Profit", SignalSources.Portfolio, DecisionBucket.TrimExit)]
    [InlineData("Cut Loss / Exit", SignalSources.Portfolio, DecisionBucket.TrimExit)]
    [InlineData("Cut Loss / Thesis Review", SignalSources.Portfolio, DecisionBucket.RiskReview)]
    [InlineData("Close / Protect", SignalSources.Portfolio, DecisionBucket.RiskReview)]
    [InlineData("Cut Loss / Exit", SignalSources.Watchlist, DecisionBucket.None)]
    [InlineData("Trim / Exit Watch", SignalSources.Portfolio, DecisionBucket.None)]
    [InlineData("BUY WATCH", SignalSources.Watchlist, DecisionBucket.None)]
    [InlineData("AVOID", SignalSources.Watchlist, DecisionBucket.None)]
    [InlineData("Avoid / Wait", SignalSources.Portfolio, DecisionBucket.None)]
    [InlineData("Watch / Do Not Chase", SignalSources.Portfolio, DecisionBucket.None)]
    [InlineData("Hold / Review", SignalSources.Portfolio, DecisionBucket.None)]
    [InlineData("Hold / Risk Review", SignalSources.Portfolio, DecisionBucket.None)]
    [InlineData("WAIT FOR PULLBACK", SignalSources.Watchlist, DecisionBucket.None)]
    public void DecisionEligibility_ClassifiesFromTheCatalog(string action, string source, DecisionBucket expected) =>
        Assert.Equal(expected, DecisionEligibility.Classify(Result("X", action, source, 10m)));

    [Fact]
    public void DecisionEligibility_NoPrice_IsNotActionable() =>
        Assert.Equal(DecisionBucket.None, DecisionEligibility.Classify(Result("X", "STARTER ENTRY", SignalSources.Watchlist, 0m)));

    [Theory]
    [InlineData("Active", "Acted", true)]
    [InlineData("Active", "Dismissed", true)]
    [InlineData("Active", "Expired", true)]
    [InlineData("Active", "Invalidated", true)]
    [InlineData("Active", "Superseded", true)]
    [InlineData("Active", "Active", false)]
    [InlineData("Acted", "Invalidated", false)]
    [InlineData("Dismissed", "Active", false)]
    [InlineData("Expired", "Active", false)]
    [InlineData("Invalidated", "Superseded", false)]
    [InlineData("Superseded", "Expired", false)]
    public void Lifecycle_TransitionMatrix(string from, string to, bool allowed) =>
        Assert.Equal(allowed, RecommendationLifecycle.CanTransition(from, to));

    [Fact]
    public void Lifecycle_ExpiryWindows_AreFiveForEntryAndThreeForTrimExit()
    {
        Assert.Equal(5, RecommendationLifecycle.ExpiryWindowDays(SignalCategories.Entry));
        Assert.Equal(3, RecommendationLifecycle.ExpiryWindowDays(SignalCategories.TrimExit));
        Assert.Equal(5, RecommendationLifecycle.WeekdaysBetween(new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 26)));
    }

    // â”€â”€ Test doubles â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private sealed class Harness
    {
        public AppDbContext Db { get; } = new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public FakeScores Scores { get; }
        public FakeMarket Market { get; } = new();
        public SignalsAuditService Service { get; }
        private readonly MemoryCache _cache = new(new MemoryCacheOptions());

        public SignalsAuditService NewService() => new(
            Db, Scores, FinalActions, new FakeResolver(), Market, _cache, new FakeAudit(), NullLogger<SignalsAuditService>.Instance);

        public FakeFinalActions FinalActions { get; }

        /// <summary>A complete EOD pass over the current fake evaluation.</summary>
        public async Task<SignalCaptureResultDto> EodAsync(bool complete = true)
        {
            var snap = await FinalActions.ComputeAsync(User);
            return await Service.CaptureAsync(User, snap, new CaptureOptions(IsEodPass: true, EvaluationComplete: complete));
        }
        public Harness(List<ActionScoreDto> initial)
        {
            Scores = new FakeScores { Items = initial };
            FinalActions = new FakeFinalActions(Scores);
            Service = NewService();
        }

        /// <summary>Initialises settings with an unrelated low score, then makes <paramref name="symbol"/> a fresh qualifier.</summary>
        public static async Task<Harness> WithNewQualifierAsync(string symbol, decimal score, bool capture = true)
        {
            var h = new Harness([Score("BASE", 10m)]);
            await h.Service.CaptureAsync(User);
            h.Scores.Items = [Score("BASE", 10m), Score(symbol, score)];
            if (capture) await h.Service.CaptureAsync(User);
            return h;
        }

        public PortfolioItem AddTransaction(string symbol, string type, DateOnly date)
        {
            var item = new PortfolioItem
            {
                UserId = User, Symbol = symbol, CompanyName = symbol, Shares = 10, AverageCostBasis = 28.71m,
                TransactionType = type, AccountType = "TFSA", DecisionSource = "App Signal",
                OpenDate = date.ToDateTime(TimeOnly.MinValue),
            };
            Db.PortfolioItems.Add(item);
            Db.SaveChanges();
            return item;
        }

        public SignalAuditEvent AddEvent(string symbol, DateOnly date, string role, string myAction,
            decimal price = 100m, string user = User)
        {
            var ev = new SignalAuditEvent
            {
                UserId = user, Symbol = symbol, AnalysisSymbol = symbol, SignalType = SignalTypeCatalog.PriorityCandidateKey,
                SignalCategory = SignalCategories.Entry, Direction = SignalDirections.Buy, SignalDate = date,
                SignalPrice = price, Role = role, Source = SignalSources.Watchlist, Score = 80m, MyAction = myAction,
                EpisodeKey = $"PC|{symbol}|{date:yyyyMMdd}|1",
            };
            Db.SignalAuditEvents.Add(ev);
            Db.SaveChanges();
            return ev;
        }
    }

    private sealed class FakeScores : IPortfolioActionScoreService
    {
        public List<ActionScoreDto> Items { get; set; } = [];
        public Task<IReadOnlyList<ActionScoreDto>> GetScoresAsync(string userId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ActionScoreDto>>(Items.ToList());
    }

    /// <summary>
    /// Every scored symbol gets a watchlist decision of BUY WATCH (Priority-eligible, not in the default audited
    /// list) unless overridden, so Priority tests are not polluted by Final Action events.
    /// </summary>
    private sealed class FakeFinalActions(FakeScores scores) : IFinalActionService
    {
        public Dictionary<string, string> WatchlistAction { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<FinalActionResultDto> Portfolio { get; } = [];
        public List<FinalActionResultDto> ExtraWatchlist { get; } = [];
        public bool Fail { get; set; }
        public bool HardNegative { get; set; }
        public HashSet<string> Unknown { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<int> PublishPortfolioActionsAsync(string userId, FinalActionSnapshotDto snapshot, CancellationToken ct = default) =>
            Task.FromResult(snapshot.Portfolio.Count);

        public Task<FinalActionSnapshotDto> ComputeAsync(string userId, bool fresh = false, CancellationToken ct = default)
        {
            if (Fail) throw new InvalidOperationException("scan failed");
            var watch = scores.Items
                .Select(s => Result(s.Symbol, WatchlistAction.GetValueOrDefault(s.Symbol, "BUY WATCH"), SignalSources.Watchlist, s.CurrentPrice, HardNegative))
                .Concat(ExtraWatchlist).ToList();
            var meta = new FinalActionEvaluationMeta(Unknown.Select(s => s.ToUpperInvariant()).ToHashSet(), DateTime.UtcNow, true);
            return Task.FromResult(new FinalActionSnapshotDto(watch, Portfolio.ToList(), DateTime.UtcNow, meta));
        }
    }

    private static FinalActionResultDto Result(string symbol, string action, string source, decimal price, bool hardNegative = false)
    {
        var def = FinalActionCatalog.Describe(action);
        return new FinalActionResultDto(symbol, source, null, "Strategic", action, def.Id, def.Category, def.Direction, "buy", "DEVELOPING",
            "ma-confirmed-buy", "Confirmed Constructive", "Uptrend", "Hold Longs", "No Gap", "hover", "reason", hardNegative,
            price, symbol, "USD");
    }

    private sealed class FakeResolver : ISecurityAnalysisResolver
    {
        public Task<ResolvedSecurityAnalysis> ResolveAsync(string tradingTicker, string? userId, CancellationToken ct = default) =>
            Task.FromResult(new ResolvedSecurityAnalysis(tradingTicker, tradingTicker, "US", "USD", false,
                default, null));
        public Task<bool> ValidateUnderlyingTickerAsync(string underlyingTicker, CancellationToken ct = default) => Task.FromResult(true);
        public Task<ResolvedSecurityAnalysis> SaveUserMappingAsync(string tradingTicker, string underlyingTicker, string userId, bool useUnderlyingForAnalysis, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> RemoveUserMappingAsync(string tradingTicker, string userId, CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class FakeAudit : IAuditService
    {
        public Task<AuditPagedResponse> QueryAsync(AuditQuery query, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AuditLogDto?> GetByIdAsync(long id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AuditFiltersDto> GetFiltersAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> DeleteAsync(long id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> DeleteManyAsync(IReadOnlyList<long> ids, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string> ExportCsvAsync(AuditQuery query, CancellationToken ct = default) => throw new NotSupportedException();
        public Task LogAsync(string category, string action, string summary, string? entityType = null,
            string? symbol = null, CancellationToken ct = default, string? changesJson = null) => Task.CompletedTask;
    }

    private sealed class FakeMarket : IMarketDataProvider
    {
        public Dictionary<string, List<MarketDailyClose>> Series { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyList<MarketDailyClose>?> GetDailyClosesAsync(string symbol, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<MarketDailyClose>?>(Series.GetValueOrDefault(symbol));

        public Task<StockQuote?> GetQuoteAsync(string symbol, CancellationToken ct = default) => Task.FromResult<StockQuote?>(null);
        public Task<Dictionary<string, StockQuote>> GetBatchQuotesAsync(IEnumerable<string> symbols, CancellationToken ct = default) => Task.FromResult(new Dictionary<string, StockQuote>());
        public Task<(string sector, string industry)> GetSectorAsync(string symbol, CancellationToken ct = default) => Task.FromResult(("", ""));
        public Task<IReadOnlyList<SymbolSearchResult>> SearchSymbolAsync(string query, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SymbolSearchResult>>([]);
        public Task<Dictionary<string, decimal>> GetAnalystTargetsAsync(IEnumerable<string> symbols, CancellationToken ct = default) => Task.FromResult(new Dictionary<string, decimal>());
        public Task<FundamentalsSnapshot?> GetFundamentalsAsync(string symbol, CancellationToken ct = default) => Task.FromResult<FundamentalsSnapshot?>(null);
        public Task<Dictionary<string, DateTime>> GetEarningsDatesAsync(IEnumerable<string> symbols, CancellationToken ct = default) => Task.FromResult(new Dictionary<string, DateTime>());
        public Task<Dictionary<string, decimal>> GetHistoricalClosingPricesAsync(string dateStr, IEnumerable<string> symbols, CancellationToken ct = default) => Task.FromResult(new Dictionary<string, decimal>());
    }
}
