using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using PortfolioManager.Api.Services.Risk;

namespace PortfolioManager.Tests;

/// <summary>PM-033: trade episodes, risk plans, user-recorded broker stops, stop-sheet import and stop-risk evaluation.</summary>
public class Pm033RiskTests
{
    private static AppDbContext NewDb()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        foreach (var n in new[] { "A", "B", "Corp_TD", "Margin_L_TD", "TFSA_D_TD", "TFSA_L_TD" })
            db.AccountTypes.Add(new AccountType { Name = n, NormalizedName = n.ToUpperInvariant() });
        db.SaveChanges();
        return db;
    }

    private sealed class FixedAccessor : IHttpContextAccessor { public HttpContext? HttpContext { get; set; } }

    private static IHttpContextAccessor Principal(string role = "Admin", string uid = "u1") => new FixedAccessor
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, uid), new Claim(ClaimTypes.Name, "tester"), new Claim(ClaimTypes.Role, role)], "test")),
        },
    };

    private static PortfolioItem Lot(int id, string sym, string acct, decimal sh, string type, string? open, string? close = null,
        string? role = "Swing", decimal avg = 10m, decimal? closing = null, Guid? ep = null, string? uid = "u1", string? src = null) => new()
    {
        Id = id, Symbol = sym, CompanyName = sym, Shares = sh, AverageCostBasis = avg, AccountType = acct, TransactionType = type,
        OpenDate = open is null ? null : DateTime.Parse(open), CloseDate = close is null ? null : DateTime.Parse(close),
        HoldingRole = role, ClosingPrice = closing, TradeEpisodeId = ep, UserId = uid, DecisionSource = src,
    };

    /// <summary>The exact rows read (read-only) from PortfolioManagerLocal for the eight stop-sheet symbols on 2026-10-28.</summary>
    private static IEnumerable<PortfolioItem> LiveStopSheetLots() =>
    [
        Lot(596, "DIS.TO", "TFSA_L_TD", 1500, "OPEN", "2026-08-13", avg: 11.66m, src: "Rebalance"),
        Lot(574, "EFR.TO", "TFSA_D_TD", 1000, "CLOSE", "2026-07-21", "2026-07-23", avg: 17.48m, uid: null),
        Lot(638, "EFR.TO", "TFSA_D_TD", 1000, "OPEN", "2026-10-15", avg: 12.13m),
        Lot(572, "EMP-A.TO", "TFSA_L_TD", 200, "OPEN", "2026-07-21", avg: 51.15m, uid: null),
        Lot(528, "MAL.TO", "Corp_TD", 350, "CLOSE", null, "2026-08-14", role: null, avg: 25.6143m, uid: null),
        Lot(584, "MAL.TO", "Corp_TD", 350, "CLOSE", "2026-05-01", "2026-08-05", role: "Strategic", avg: 25.6143m),
        Lot(642, "MAL.TO", "TFSA_D_TD", 200, "OPEN", "2026-10-26", avg: 34.1m),
        Lot(639, "NOVO.TO", "TFSA_D_TD", 600, "OPEN", "2026-10-15", avg: 17m),
        Lot(607, "NVDA.TO", "Corp_TD", 400, "OPEN", "2026-08-27", avg: 49.52m),
        Lot(541, "TA.TO", "Margin_L_TD", 500, "CLOSE", "2026-06-05", "2026-06-29", role: null, avg: 19.2m, uid: null),
        Lot(506, "TA.TO", "Corp_TD", 1000, "OPEN", "2026-06-05", avg: 19.2m, uid: null),
        Lot(637, "WELL.TO", "Corp_TD", 1, "CLOSE", "2026-10-12", "2026-10-12", role: null, avg: 4.2m),
        Lot(595, "WELL.TO", "Corp_TD", 2000, "OPEN", "2026-08-13", avg: 4.4m),
    ];

    private static readonly (string Sym, decimal Stop)[] Sheet =
        [("DIS.TO", 11.85m), ("NVDA.TO", 46.50m), ("NOVO.TO", 16.20m), ("EMP-A.TO", 48.25m),
         ("EFR.TO", 10.75m), ("TA.TO", 16.75m), ("WELL.TO", 3.95m), ("MAL.TO", 32.40m)];

    private static StopSheetRequest SheetRequest(bool dryRun = true) => new("user-stop-sheet", "2026-10-28",
        Sheet.Select((s, i) => new StopSheetRow(s.Sym, s.Stop, "StopMarket", "CAD", null, i + 1)).ToList(), dryRun);

    private static LotRef R(PortfolioItem i) => LotRef.From(i);

    // â”€â”€ 1. Episode reconstruction with real transaction patterns â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Real_patterns_resolve_reliably_or_stay_unresolved()
    {
        var lots = LiveStopSheetLots().Select(R).ToList();
        var pos = TradeEpisodeResolver.ResolveOpenSwingPositions(lots).ToDictionary(p => p.Symbol);

        foreach (var s in new[] { "DIS.TO", "NVDA.TO", "NOVO.TO", "EMP-A.TO", "EFR.TO", "MAL.TO" })
            pos[s].Resolution.Should().Be(EpisodeResolutions.Derived, s);
        pos["EFR.TO"].EpisodeOpenDate.Should().Be(new DateTime(2026, 10, 15), "the earlier EFR trade closed on 07-23 so this is a new episode");
        pos["MAL.TO"].Account.Should().Be("TFSA_D_TD", "the stop attaches to the open lot, not the older Corp_TD history");
        pos["TA.TO"].Resolution.Should().Be(EpisodeResolutions.Unresolved, "a CLOSE in another account overlaps the open period");
        pos["WELL.TO"].Resolution.Should().Be(EpisodeResolutions.Unresolved, "the 1-share same-day CLOSE cannot be proven related");
        pos["WELL.TO"].SwingQuantity.Should().Be(2000, "an unresolved boundary does not stop the open position being managed");
    }

    [Fact]
    public void Additions_do_not_reset_days_held_and_reopen_is_new_episode()
    {
        var added = new[] { R(Lot(1, "ABC.TO", "A", 100, "OPEN", "2026-09-01")), R(Lot(2, "ABC.TO", "A", 50, "OPEN", "2026-10-10")) };
        var p = TradeEpisodeResolver.ResolveOpenSwingPositions(added).Single();
        p.EpisodeOpenDate.Should().Be(new DateTime(2026, 9, 1));
        p.LastExecutionDate.Should().Be(new DateTime(2026, 10, 10));
        TradeEpisodeResolver.DaysHeld(p.EpisodeOpenDate, new DateTime(2026, 10, 28, 15, 0, 0, DateTimeKind.Utc)).Should().Be(57);

        var reopened = new[] { R(Lot(1, "ABC.TO", "A", 100, "CLOSE", "2026-09-01", "2026-09-20")), R(Lot(3, "ABC.TO", "A", 100, "OPEN", "2026-10-05")) };
        var q = TradeEpisodeResolver.ResolveOpenSwingPositions(reopened).Single();
        q.EpisodeOpenDate.Should().Be(new DateTime(2026, 10, 5));
        q.Resolution.Should().Be(EpisodeResolutions.Derived);
    }

    [Fact]
    public void Partial_close_remainder_with_matching_dates_is_not_trusted_without_an_id()
    {
        // Same OpenDate remainder + CLOSE row: indistinguishable from independent lots, so never assigned by dates alone.
        var lots = new[] { R(Lot(1, "XYZ.TO", "A", 40, "CLOSE", "2026-09-01", "2026-09-15")), R(Lot(2, "XYZ.TO", "A", 60, "OPEN", "2026-09-01")) };
        TradeEpisodeResolver.ResolveOpenSwingPositions(lots).Single().Resolution.Should().Be(EpisodeResolutions.Unresolved);
        var plan = TradeEpisodeResolver.PlanBackfill(lots);
        plan.Assignments.Should().BeEmpty();
        plan.Unresolved.Select(u => u.ItemId).Should().BeEquivalentTo([1, 2]);
    }

    [Fact]
    public void Backfill_links_only_unambiguous_history()
    {
        var plan = TradeEpisodeResolver.PlanBackfill(LiveStopSheetLots().Select(R).ToList());
        var assigned = plan.Assignments.Select(a => a.ItemId).ToHashSet();
        assigned.Should().Contain([596, 638, 572, 639, 607, 642, 574], "single-lot or zero-open-gap history is unambiguous");
        assigned.Should().NotContain([506, 541, 595, 637], "TA.TO / WELL.TO relationships stay unresolved for review");
        plan.Unresolved.Select(u => u.ItemId).Should().Contain([506, 541, 595, 637]);
        plan.Episodes.Single(e => e.Symbol == "EFR.TO" && e.Status == EpisodeStatuses.Closed).ClosedDate.Should().Be(new DateTime(2026, 7, 23));
    }

    [Fact]
    public void Mixed_role_account_is_unresolved_without_stored_id_and_reports_swing_and_total()
    {
        var lots = new[] { R(Lot(1, "MIX.TO", "A", 100, "OPEN", "2026-09-01", role: "Swing")), R(Lot(2, "MIX.TO", "A", 300, "OPEN", "2026-08-01", role: "Strategic")) };
        var p = TradeEpisodeResolver.ResolveOpenSwingPositions(lots).Single();
        p.SwingQuantity.Should().Be(100);
        p.TotalQuantity.Should().Be(400);
        p.MixedRole.Should().BeTrue();
        p.Resolution.Should().Be(EpisodeResolutions.Unresolved);
    }

    // â”€â”€ 2. Episode assignment inside PortfolioService writes â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static PortfolioService Svc(AppDbContext db) => new(db, null!, new MutationClock(), Principal());

    [Fact]
    public async Task New_trade_gets_episode_addition_joins_partial_close_propagates_and_reopen_is_new()
    {
        await using var db = NewDb();
        var svc = Svc(db);
        var first = await svc.AddAsync(new AddPortfolioItemRequest("ABC.TO", "ABC", 100, 10, "OPEN", "A", new DateTime(2026, 9, 1), HoldingRole: "Swing"));
        var epId = (await db.PortfolioItems.FindAsync(first.Id))!.TradeEpisodeId;
        epId.Should().NotBeNull();

        var add = await svc.AddAsync(new AddPortfolioItemRequest("ABC.TO", "ABC", 50, 11, "OPEN", "A", new DateTime(2026, 9, 10), HoldingRole: "Swing"));
        (await db.PortfolioItems.FindAsync(add.Id))!.TradeEpisodeId.Should().Be(epId, "an addition joins the single established episode");

        var res = await svc.UpdateAsync(first.Id, new UpdatePortfolioItemRequest("ABC", 40, 10, TransactionType: "CLOSE", AccountType: "A",
            OpenDate: new DateTime(2026, 9, 1), CloseDate: new DateTime(2026, 9, 20), ClosingPrice: 12, HoldingRole: "Swing"));
        res!.NewOpenItem.Should().NotBeNull();
        (await db.PortfolioItems.FindAsync(res.NewOpenItem!.Id))!.TradeEpisodeId.Should().Be(epId, "the split remainder keeps the episode");
        (await db.TradeEpisodes.FindAsync(epId!.Value))!.Status.Should().Be(EpisodeStatuses.Open);

        // Close everything, then reopen.
        foreach (var id in new[] { add.Id, res.NewOpenItem.Id })
            await svc.UpdateAsync(id, new UpdatePortfolioItemRequest("ABC", (await db.PortfolioItems.FindAsync(id))!.Shares, 10, TransactionType: "CLOSE",
                AccountType: "A", OpenDate: new DateTime(2026, 9, 1), CloseDate: new DateTime(2026, 10, 1), ClosingPrice: 13, HoldingRole: "Swing"));
        (await db.TradeEpisodes.FindAsync(epId.Value))!.Status.Should().Be(EpisodeStatuses.Closed);

        var reopened = await svc.AddAsync(new AddPortfolioItemRequest("ABC.TO", "ABC", 70, 14, "OPEN", "A", new DateTime(2026, 10, 15), HoldingRole: "Swing"));
        var newEp = (await db.PortfolioItems.FindAsync(reopened.Id))!.TradeEpisodeId;
        newEp.Should().NotBeNull();
        (newEp == epId).Should().BeFalse("a reopened trade is a new episode");
    }

    [Fact]
    public async Task Addition_in_a_mixed_role_account_is_left_unlinked()
    {
        await using var db = NewDb();
        db.PortfolioItems.Add(Lot(1, "MIX.TO", "A", 300, "OPEN", "2026-08-01", role: "Strategic", ep: Guid.NewGuid()));
        await db.SaveChangesAsync();
        var add = await Svc(db).AddAsync(new AddPortfolioItemRequest("MIX.TO", "MIX", 100, 10, "OPEN", "A", new DateTime(2026, 9, 1), HoldingRole: "Swing"));
        (await db.PortfolioItems.FindAsync(add.Id))!.TradeEpisodeId.Should().BeNull();
    }

    // â”€â”€ 3. Stop-risk evaluation â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static PriceEvidence Eod(decimal p) => new(p, DateTime.UtcNow, DateOnly.FromDateTime(DateTime.UtcNow), PriceBases.Eod, "test");
    private static StopLite Stop(decimal trig, decimal? covered = null, string v = "Unverified") => new(1, trig, covered, v);

    [Fact]
    public void Breach_with_unknown_coverage_is_critical_and_never_claims_execution()
    {
        var e = StopRiskEvaluator.Evaluate(new RiskEvalInput(Eod(10m), null, [Stop(10.5m)], 100, 100, 5));
        e.Statuses.Should().Contain(RiskStatuses.StopBreached).And.Contain(RiskStatuses.UnknownCoverage);
        e.Severity.Should().Be("Critical");
        e.Observation.Should().Contain("market observation").And.Contain("Verify with your broker");
        e.Observation!.ToLowerInvariant().Should().NotContain("executed").And.NotContain("filled at");
    }

    [Fact]
    public void Stale_or_unavailable_evidence_never_produces_breach_or_approach()
    {
        foreach (var basis in new[] { PriceBases.Stale, PriceBases.Unavailable })
        {
            var ev = new PriceEvidence(9m, DateTime.UtcNow, null, basis, "t");
            var e = StopRiskEvaluator.Evaluate(new RiskEvalInput(ev, null, [Stop(10m)], 100, 100, 5));
            e.Statuses.Should().Contain(RiskStatuses.StaleEvidence).And.NotContain(RiskStatuses.StopBreached).And.NotContain(RiskStatuses.ApproachingStop);
            e.DistancePct.Should().BeNull();
        }
    }

    [Fact]
    public void Approaching_uses_the_advisory_threshold()
    {
        StopRiskEvaluator.Evaluate(new RiskEvalInput(Eod(100m), null, [Stop(96m, 100)], 100, 100, 5)).Statuses.Should().Contain(RiskStatuses.ApproachingStop);
        StopRiskEvaluator.Evaluate(new RiskEvalInput(Eod(100m), null, [Stop(90m, 100)], 100, 100, 5)).Statuses.Should().NotContain(RiskStatuses.ApproachingStop);
    }

    [Fact]
    public void Coverage_states_unknown_partial_full_ambiguous_and_none()
    {
        StopRiskEvaluator.CoverageFor([], 100, 100, out _).Should().Be(CoverageStates.None);
        StopRiskEvaluator.CoverageFor([Stop(9)], 100, 100, out var c1).Should().Be(CoverageStates.Unknown); c1.Should().BeNull("unknown is never reported as zero or full");
        StopRiskEvaluator.CoverageFor([Stop(9, 40)], 100, 100, out var c2).Should().Be(CoverageStates.Partial); c2.Should().Be(40);
        StopRiskEvaluator.CoverageFor([Stop(9, 60), Stop(9, 40)], 100, 100, out _).Should().Be(CoverageStates.Full);
        StopRiskEvaluator.CoverageFor([Stop(9, 150)], 100, 100, out _).Should().Be(CoverageStates.OverCovered);
        StopRiskEvaluator.CoverageFor([Stop(9, 100)], 100, 400, out _).Should().Be(CoverageStates.Ambiguous, "a broker stop may cover non-Swing lots too");
    }

    [Fact]
    public void Fully_protected_is_never_shown_for_user_confirmed_orders()
    {
        var unverified = StopRiskEvaluator.Evaluate(new RiskEvalInput(Eod(100m), 90m, [Stop(90m, 100)], 100, 100, 5));
        unverified.Protection.Should().Be("Broker Stop Recorded");
        var unknown = StopRiskEvaluator.Evaluate(new RiskEvalInput(Eod(100m), 90m, [Stop(90m)], 100, 100, 5));
        unknown.Protection.Should().Be("Broker Stop Recorded").And.NotBe("Fully Protected");
    }

    [Fact]
    public void Planned_and_broker_stops_stay_distinct_and_mismatch_is_flagged()
    {
        var e = StopRiskEvaluator.Evaluate(new RiskEvalInput(Eod(100m), 92m, [Stop(90m)], 100, 100, 5));
        e.Statuses.Should().Contain(RiskStatuses.StopMismatch);
        e.GoverningStop.Should().Be(90m);
        StopRiskEvaluator.Evaluate(new RiskEvalInput(Eod(100m), 92m, [], 100, 100, 5)).Statuses.Should().Contain(RiskStatuses.NoBrokerProtection);
        StopRiskEvaluator.Evaluate(new RiskEvalInput(Eod(100m), null, [], 100, 100, 5)).Statuses.Should().Contain(RiskStatuses.MissingRiskPlan);
    }

    [Fact]
    public void Imminent_breach_with_no_protection_outranks_everything_else()
    {
        var critical = StopRiskEvaluator.Evaluate(new RiskEvalInput(Eod(9m), 10m, [], 100, 100, 5));
        var high = StopRiskEvaluator.Evaluate(new RiskEvalInput(Eod(9m), 10m, [Stop(10m, 100)], 100, 100, 5));
        var medium = StopRiskEvaluator.Evaluate(new RiskEvalInput(Eod(100m), 90m, [], 100, 100, 5));
        critical.Rank.Should().BeLessThan(high.Rank);
        high.Rank.Should().BeLessThan(medium.Rank);
    }

    [Fact]
    public void Price_evidence_without_a_quote_is_unavailable_and_old_snapshot_is_stale()
    {
        PriceEvidence.FromQuote(0, 0, "CLOSED", "ABC.TO", DateTime.UtcNow).Basis.Should().Be(PriceBases.Unavailable);
        var old = DateTimeOffset.UtcNow.AddDays(-10).ToUnixTimeSeconds();
        PriceEvidence.FromQuote(10, old, "CLOSED", "ABC.TO", DateTime.UtcNow).Basis.Should().Be(PriceBases.Stale);
    }

    // â”€â”€ 4. Stop-sheet import (initial eight stops) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static async Task<AppDbContext> SeededAsync()
    {
        var db = NewDb();
        db.PortfolioItems.AddRange(LiveStopSheetLots());
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task Dry_run_maps_all_eight_stops_to_the_expected_accounts_without_writing()
    {
        await using var db = await SeededAsync();
        var res = await new StopSheetImportService(db, Principal()).ImportAsync(SheetRequest());

        res.DryRun.Should().BeTrue(); res.Applied.Should().BeFalse(); res.Blocked.Should().Be(0); res.ToImport.Should().Be(8);
        var byS = res.Rows.ToDictionary(r => r.Symbol);
        byS["DIS.TO"].Account.Should().Be("TFSA_L_TD"); byS["DIS.TO"].SwingQuantity.Should().Be(1500);
        byS["NVDA.TO"].Account.Should().Be("Corp_TD"); byS["NVDA.TO"].TotalQuantity.Should().Be(400);
        byS["NOVO.TO"].Account.Should().Be("TFSA_D_TD");
        byS["EMP-A.TO"].Account.Should().Be("TFSA_L_TD");
        byS["EFR.TO"].EpisodeOpenDate.Should().Be(new DateTime(2026, 10, 15));
        byS["TA.TO"].Account.Should().Be("Corp_TD"); byS["TA.TO"].EpisodeResolution.Should().Be(EpisodeResolutions.Unresolved);
        byS["WELL.TO"].SwingQuantity.Should().Be(2000); byS["WELL.TO"].EpisodeResolution.Should().Be(EpisodeResolutions.Unresolved);
        byS["MAL.TO"].Account.Should().Be("TFSA_D_TD"); byS["MAL.TO"].SwingQuantity.Should().Be(200);
        res.Rows.Select(r => r.TriggerPrice).Should().BeEquivalentTo(Sheet.Select(s => s.Stop), "prices are preserved exactly");
        res.Rows.Should().OnlyContain(r => r.Currency == "CAD");
        (await db.BrokerStopRecords.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Apply_is_atomic_audited_unverified_with_unknown_coverage_and_idempotent()
    {
        await using var db = await SeededAsync();
        var svc = new StopSheetImportService(db, Principal());
        var res = await svc.ImportAsync(SheetRequest(dryRun: false));
        res.Applied.Should().BeTrue();

        var recs = await db.BrokerStopRecords.ToListAsync();
        recs.Should().HaveCount(8);
        recs.Should().OnlyContain(r => r.OrderType == "StopMarket" && r.UserStatus == BrokerUserStatuses.UserConfirmedEstablished
            && r.BrokerVerificationStatus == BrokerVerification.Unverified && r.CoveredQuantity == null
            && r.ConfirmationSource == ConfirmationSources.UserSuppliedStopSheet && r.Currency == "CAD");
        recs.Single(r => r.Symbol == "MAL.TO").AccountType.Should().Be("TFSA_D_TD");
        recs.Select(r => r.TriggerPrice).Should().BeEquivalentTo(Sheet.Select(s => s.Stop));
        (await db.RiskAuditEvents.CountAsync()).Should().Be(8);
        (await db.AuditLogs.CountAsync(a => a.Category == AuditCategories.Risk)).Should().Be(8);

        var again = await svc.ImportAsync(SheetRequest(dryRun: false));
        again.Applied.Should().BeFalse(); again.Skipped.Should().Be(8);
        (await db.BrokerStopRecords.CountAsync()).Should().Be(8, "re-running never duplicates");
    }

    [Fact]
    public async Task Same_sheet_row_with_a_different_price_is_a_conflict_not_a_silent_update()
    {
        await using var db = await SeededAsync();
        var svc = new StopSheetImportService(db, Principal());
        await svc.ImportAsync(SheetRequest(dryRun: false));
        var changed = SheetRequest() with { Rows = [new StopSheetRow("DIS.TO", 11.50m, "StopMarket", "CAD", null, 1)] };
        var res = await svc.ImportAsync(changed);
        res.Rows.Single().Action.Should().Be(StopSheetActions.Conflict);
        (await db.BrokerStopRecords.FirstAsync(r => r.Symbol == "DIS.TO")).TriggerPrice.Should().Be(11.85m);
    }

    [Fact]
    public async Task Existing_different_stop_blocks_import_unless_explicitly_a_separate_real_order()
    {
        await using var db = await SeededAsync();
        var svc = new StopSheetImportService(db, Principal());
        await svc.ImportAsync(SheetRequest(dryRun: false));

        var second = new StopSheetRequest("second-sheet", "1", [new StopSheetRow("DIS.TO", 11.85m, "StopMarket", "CAD", null, 1)]);
        (await svc.ImportAsync(second)).Rows.Single().Action.Should().Be(StopSheetActions.Conflict);

        var allowed = await svc.ImportAsync(second with { DryRun = false, AllowAdditionalOrders = true });
        allowed.Applied.Should().BeTrue();
        (await db.BrokerStopRecords.CountAsync(r => r.Symbol == "DIS.TO")).Should().Be(2, "two genuine identical orders keep separate identities");
        (await db.BrokerStopRecords.Where(r => r.Symbol == "DIS.TO").Select(r => r.OrderIdentity).Distinct().CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Duplicate_symbols_across_accounts_are_ambiguous_missing_are_unmatched_and_currency_must_match()
    {
        await using var db = NewDb();
        db.PortfolioItems.AddRange(Lot(1, "DUP.TO", "A", 10, "OPEN", "2026-09-01"), Lot(2, "DUP.TO", "B", 20, "OPEN", "2026-09-01"));
        await db.SaveChangesAsync();
        var svc = new StopSheetImportService(db, Principal());

        var amb = await svc.ImportAsync(new StopSheetRequest("s", "1", [new StopSheetRow("DUP.TO", 5m, null, null, null, 1)]));
        amb.Rows.Single().Action.Should().Be(StopSheetActions.Ambiguous);
        var picked = await svc.ImportAsync(new StopSheetRequest("s", "1", [new StopSheetRow("DUP.TO", 5m, null, null, "B", 1)]));
        picked.Rows.Single().Account.Should().Be("B");

        (await svc.ImportAsync(new StopSheetRequest("s", "1", [new StopSheetRow("NOPE.TO", 5m, null, null, null, 1)])))
            .Rows.Single().Action.Should().Be(StopSheetActions.Unmatched);
        (await svc.ImportAsync(new StopSheetRequest("s", "1", [new StopSheetRow("DUP.TO", 5m, null, "USD", "A", 1)])))
            .Rows.Single().Action.Should().Be(StopSheetActions.Invalid);
    }

    [Fact]
    public async Task A_blocked_row_prevents_any_write_and_non_admin_cannot_apply()
    {
        await using var db = await SeededAsync();
        var req = SheetRequest(dryRun: false) with { Rows = [.. SheetRequest().Rows, new StopSheetRow("MISSING.TO", 1m, null, null, null, 99)] };
        var res = await new StopSheetImportService(db, Principal()).ImportAsync(req);
        res.Applied.Should().BeFalse(); res.Blocked.Should().Be(1);
        (await db.BrokerStopRecords.CountAsync()).Should().Be(0);

        var act = () => new StopSheetImportService(db, Principal("Trader", "u2")).ImportAsync(SheetRequest(dryRun: false));
        await act.Should().ThrowAsync<RiskValidationException>();
    }

    // â”€â”€ 5. Risk service: plan vs broker records, audit, validation, concurrency â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static async Task<(AppDbContext Db, RiskService Svc)> RiskAsync()
    {
        var db = NewDb();
        db.PortfolioItems.Add(Lot(1, "ABC.TO", "A", 100, "OPEN", "2026-09-01"));
        await db.SaveChangesAsync();
        return (db, new RiskService(db, Principal()));
    }

    [Fact]
    public async Task Changing_planned_stop_never_touches_the_broker_record_and_is_audited()
    {
        var (db, svc) = await RiskAsync();
        await using var _ = db;
        await svc.SaveBrokerStopAsync(new SaveBrokerStopRequest(null, "A", "ABC.TO", "StopMarket", 9m, null, null, null, null, null, null, null, false, null));
        var p = await svc.SavePlanAsync(new SaveRiskPlanRequest("A", "ABC.TO", 9.5m, 12m, "Fixed", "why", null, null, "initial", null));
        p.Plan!.PlannedStopPrice.Should().Be(9.5m);
        p.BrokerStops.Single().TriggerPrice.Should().Be(9m, "the broker order is unchanged; both values stay visible");

        var again = await svc.SavePlanAsync(new SaveRiskPlanRequest("A", "ABC.TO", 9.5m, 12m, "Fixed", "why", null, null, null, p.Plan.RowVersion));
        again.History.Count(h => h.EventType.StartsWith("Plan")).Should().Be(1, "an unchanged save writes no audit event");

        var moved = await svc.SavePlanAsync(new SaveRiskPlanRequest("A", "ABC.TO", 9.25m, 12m, "Fixed", "why", null, null, "tighten", p.Plan.RowVersion));
        var ev = moved.History.First(h => h.EventType == "PlanUpdated");
        ev.Changes.Should().ContainSingle(c => c.Field == "PlannedStopPrice" && c.Old == "9.5" && c.New == "9.25");
        ev.Reason.Should().Be("tighten");
        moved.BrokerStops.Single().TriggerPrice.Should().Be(9m);
        (await db.AuditLogs.CountAsync(a => a.Category == AuditCategories.Risk)).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Validation_rejects_bad_prices_precision_and_over_coverage()
    {
        var (db, svc) = await RiskAsync();
        await using var _ = db;
        var bad = (decimal? price, decimal? cov, bool allow, string? reason) => svc.SaveBrokerStopAsync(
            new SaveBrokerStopRequest(null, "A", "ABC.TO", "StopMarket", price, null, cov, null, null, null, null, reason, allow, null));
        await ((Func<Task>)(() => bad(-1m, null, false, null))).Should().ThrowAsync<RiskValidationException>();
        await ((Func<Task>)(() => bad(0m, null, false, null))).Should().ThrowAsync<RiskValidationException>();
        await ((Func<Task>)(() => bad(9.12345m, null, false, null))).Should().ThrowAsync<RiskValidationException>();
        await ((Func<Task>)(() => bad(9m, 101m, false, null))).Should().ThrowAsync<RiskValidationException>();
        await ((Func<Task>)(() => bad(9m, 0m, false, null))).Should().ThrowAsync<RiskValidationException>();
        await bad(9m, 101m, true, "broker lot rounding");
        (await db.BrokerStopRecords.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Stale_row_version_is_a_concurrency_conflict()
    {
        var (db, svc) = await RiskAsync();
        await using var _ = db;
        var created = await svc.SavePlanAsync(new SaveRiskPlanRequest("A", "ABC.TO", 9m, null, null, null, null, null, null, null));
        var v = created.Plan!.RowVersion;
        db.RiskPlans.Single().RowVersion = [1, 2, 3, 4]; // simulates another writer changing the row
        await db.SaveChangesAsync();
        var act = () => svc.SavePlanAsync(new SaveRiskPlanRequest("A", "ABC.TO", 8m, null, null, null, null, null, null, v));
        await act.Should().ThrowAsync<RiskConcurrencyException>();
    }

    [Fact]
    public async Task Status_transitions_are_audited_filled_is_user_reported_and_records_are_never_deleted()
    {
        var (db, svc) = await RiskAsync();
        await using var _ = db;
        var pos = await svc.SaveBrokerStopAsync(new SaveBrokerStopRequest(null, "A", "ABC.TO", null, 9m, null, 100m, null, null, null, null, null, false, null));
        var id = pos.BrokerStops.Single().Id;
        var filled = await svc.TransitionBrokerStopAsync(id, new BrokerStopTransitionRequest(BrokerUserStatuses.UserReportedFilled, null, "saw fill on statement", null));
        filled.BrokerStops.Single().UserStatus.Should().Be(BrokerUserStatuses.UserReportedFilled);
        filled.BrokerStops.Single().BrokerVerificationStatus.Should().Be(BrokerVerification.Unverified, "a user report is not broker verification");
        (await db.BrokerStopRecords.CountAsync()).Should().Be(1);
        await ((Func<Task>)(() => svc.TransitionBrokerStopAsync(id, new BrokerStopTransitionRequest(BrokerUserStatuses.UserReportedCancelled, null, null, null))))
            .Should().ThrowAsync<RiskValidationException>();
    }

    // â”€â”€ 6. Stop lifecycle: additions, partial sales, full exit, reopen â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task Holding_changes_never_alter_covered_quantity_and_coverage_is_recomputed()
    {
        await using var db = NewDb();
        var psvc = Svc(db);
        var lot = await psvc.AddAsync(new AddPortfolioItemRequest("ABC.TO", "ABC", 100, 10, "OPEN", "A", new DateTime(2026, 9, 1), HoldingRole: "Swing"));
        var risk = new RiskService(db, Principal());
        await risk.SaveBrokerStopAsync(new SaveBrokerStopRequest(null, "A", "ABC.TO", null, 9m, null, 100m, null, null, null, null, null, false, null));

        await psvc.AddAsync(new AddPortfolioItemRequest("ABC.TO", "ABC", 50, 11, "OPEN", "A", new DateTime(2026, 9, 10), HoldingRole: "Swing"));
        (await db.BrokerStopRecords.SingleAsync()).CoveredQuantity.Should().Be(100m, "an addition does not change the recorded covered quantity");

        await psvc.UpdateAsync(lot.Id, new UpdatePortfolioItemRequest("ABC", 30, 10, TransactionType: "CLOSE", AccountType: "A",
            OpenDate: new DateTime(2026, 9, 1), CloseDate: new DateTime(2026, 9, 20), ClosingPrice: 12, HoldingRole: "Swing"));
        (await db.BrokerStopRecords.SingleAsync()).CoveredQuantity.Should().Be(100m, "a partial sale does not change it either");

        var trades = await new SwingTradingService(db, Principal(), new ConfigurationBuilder().Build()).GetOpenTradesAsync();
        var row = trades.Rows.Single();
        row.TotalQuantity.Should().Be(120);
        row.CoveredQuantity.Should().Be(100);
        row.RiskStatuses.Should().Contain(RiskStatuses.PartialProtection);
    }

    [Fact]
    public async Task Full_exit_retires_orders_without_claiming_cancellation_and_reopen_never_inherits_them()
    {
        await using var db = NewDb();
        var psvc = Svc(db);
        var lot = await psvc.AddAsync(new AddPortfolioItemRequest("ABC.TO", "ABC", 100, 10, "OPEN", "A", new DateTime(2026, 9, 1), HoldingRole: "Swing"));
        var risk = new RiskService(db, Principal());
        await risk.SaveBrokerStopAsync(new SaveBrokerStopRequest(null, "A", "ABC.TO", null, 9m, null, null, null, null, null, null, null, false, null));
        await risk.SavePlanAsync(new SaveRiskPlanRequest("A", "ABC.TO", 9.2m, null, null, null, null, null, null, null));

        await psvc.UpdateAsync(lot.Id, new UpdatePortfolioItemRequest("ABC", 100, 10, TransactionType: "CLOSE", AccountType: "A",
            OpenDate: new DateTime(2026, 9, 1), CloseDate: new DateTime(2026, 9, 20), ClosingPrice: 12, HoldingRole: "Swing"));
        var retired = await db.BrokerStopRecords.SingleAsync();
        retired.UserStatus.Should().Be(BrokerUserStatuses.EpisodeClosed);
        retired.BrokerCancellationUnverified.Should().BeTrue();
        retired.UserStatus.Should().NotBe(BrokerUserStatuses.UserReportedCancelled);

        await psvc.AddAsync(new AddPortfolioItemRequest("ABC.TO", "ABC", 80, 13, "OPEN", "A", new DateTime(2026, 10, 15), HoldingRole: "Swing"));
        var row = (await new SwingTradingService(db, Principal(), new ConfigurationBuilder().Build()).GetOpenTradesAsync()).Rows.Single();
        row.BrokerStops.Should().BeEmpty("the previous episode's order is not active on the reopened trade");
        row.Protection.Should().Be("No Stop");
        row.PlannedStop.Should().BeNull("the previous episode's plan is not inherited either");
        row.PlanFromPriorEpisode.Should().BeTrue();
        row.RiskStatuses.Should().Contain(RiskStatuses.MissingRiskPlan);

        var pos = await risk.GetPositionAsync("A", "ABC.TO");
        pos.BrokerStops.Single().BelongsToCurrentEpisode.Should().BeFalse();
        pos.History.Should().Contain(h => h.EventType == "EpisodeClosedBrokerOrderRetired");
    }

    [Fact]
    public async Task Unresolved_positions_remain_manageable_and_stops_attach_to_the_current_open_position()
    {
        await using var db = await SeededAsync();
        var risk = new RiskService(db, Principal());
        var pos = await risk.SaveBrokerStopAsync(new SaveBrokerStopRequest(null, "Corp_TD", "WELL.TO", null, 3.95m, null, null, null, null, null, null, null, false, null));
        pos.EpisodeResolution.Should().Be(EpisodeResolutions.Unresolved);
        pos.BrokerStops.Single().BelongsToCurrentEpisode.Should().BeTrue();
        pos.TotalQuantity.Should().Be(2000);
    }

    // â”€â”€ 7. Open trades, mixed roles, ownership â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task Open_trades_show_swing_exposure_and_total_and_flag_ambiguous_coverage()
    {
        await using var db = NewDb();
        db.PortfolioItems.AddRange(Lot(1, "MIX.TO", "A", 100, "OPEN", "2026-09-01", role: "Swing"), Lot(2, "MIX.TO", "A", 300, "OPEN", "2026-08-01", role: "Strategic"));
        await db.SaveChangesAsync();
        await new RiskService(db, Principal()).SaveBrokerStopAsync(
            new SaveBrokerStopRequest(null, "A", "MIX.TO", null, 9m, null, 100m, null, null, null, null, null, false, null));
        var row = (await new SwingTradingService(db, Principal(), new ConfigurationBuilder().Build()).GetOpenTradesAsync()).Rows.Single();
        row.SwingQuantity.Should().Be(100);
        row.TotalQuantity.Should().Be(400);
        row.MixedRole.Should().BeTrue();
        row.CoverageState.Should().Be(CoverageStates.Ambiguous);
        row.RiskStatuses.Should().Contain(RiskStatuses.AmbiguousCoverage);
    }

    [Fact]
    public async Task Legacy_null_owner_rows_follow_the_existing_ownership_rule()
    {
        await using var db = NewDb();
        db.PortfolioItems.Add(Lot(1, "OLD.TO", "A", 100, "OPEN", "2026-09-01", uid: null));
        await db.SaveChangesAsync();
        var cfg = new ConfigurationBuilder().Build();
        (await new SwingTradingService(db, Principal("Trader", "someone"), cfg).GetOpenTradesAsync()).Rows.Should().HaveCount(1, "same visibility as PortfolioService.OwnedItems");
        db.PortfolioItems.Add(Lot(2, "OTHER.TO", "A", 100, "OPEN", "2026-09-01", uid: "another-user"));
        await db.SaveChangesAsync();
        (await new SwingTradingService(db, Principal("Trader", "someone"), cfg).GetOpenTradesAsync()).Rows.Should().NotContain(r => r.Symbol == "OTHER.TO");
    }

    [Fact]
    public async Task Open_trades_without_a_quote_report_unavailable_evidence_not_a_fabricated_risk_state()
    {
        await using var db = await SeededAsync();
        await new StopSheetImportService(db, Principal()).ImportAsync(SheetRequest(dryRun: false));
        var res = await new SwingTradingService(db, Principal(), new ConfigurationBuilder().Build()).GetOpenTradesAsync();
        res.Summary.OpenTrades.Should().Be(8);
        res.Summary.AnyUnavailable.Should().BeTrue();
        res.Rows.Should().OnlyContain(r => r.PriceBasis == PriceBases.Unavailable && r.CurrentPrice == null && r.MarketValue == null);
        res.Rows.Should().OnlyContain(r => !r.RiskStatuses.Contains(RiskStatuses.StopBreached));
        res.Rows.Should().OnlyContain(r => r.Protection == "Broker Stop Recorded" && r.CoveredQuantity == null && r.CoverageState == CoverageStates.Unknown);
        res.ThresholdNote.Should().Contain("not a broker order level");
    }

    // â”€â”€ 8. Trade journal â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task Journal_groups_partial_exits_by_episode_and_never_merges_unlinked_or_separate_episodes()
    {
        await using var db = NewDb();
        var e1 = Guid.NewGuid(); var e2 = Guid.NewGuid();
        db.TradeEpisodes.AddRange(
            new TradeEpisode { Id = e1, AccountType = "A", Symbol = "ABC.TO", OpenDate = new DateTime(2026, 6, 1), ClosedDate = new DateTime(2026, 6, 30), Status = "Closed" },
            new TradeEpisode { Id = e2, AccountType = "A", Symbol = "ABC.TO", OpenDate = new DateTime(2026, 8, 1), ClosedDate = new DateTime(2026, 8, 10), Status = "Closed" });
        db.PortfolioItems.AddRange(
            Lot(1, "ABC.TO", "A", 60, "CLOSE", "2026-06-01", "2026-06-15", avg: 10, closing: 12, ep: e1, src: "Catalyst"),
            Lot(2, "ABC.TO", "A", 40, "CLOSE", "2026-06-01", "2026-06-30", avg: 10, closing: 9, ep: e1),
            Lot(3, "ABC.TO", "A", 100, "CLOSE", "2026-08-01", "2026-08-10", avg: 20, closing: 22, ep: e2),
            Lot(4, "UNL.TO", "A", 10, "CLOSE", "2026-05-01", "2026-05-05", avg: 5, closing: 6),
            Lot(5, "UNL.TO", "A", 10, "CLOSE", "2026-05-01", "2026-05-06", avg: 5, closing: 4));
        await db.SaveChangesAsync();

        var rows = await new SwingTradingService(db, Principal(), new ConfigurationBuilder().Build()).GetJournalAsync();
        rows.Count(r => r.Symbol == "ABC.TO").Should().Be(2, "separate episodes are not merged");
        var one = rows.Single(r => r.EpisodeId == e1);
        one.ClosedQuantity.Should().Be(100);
        one.RealizedPnl.Should().Be(60 * 2 + 40 * -1, "(closing - avg) x shares, the Transactions formula");
        one.FinalCloseDate.Should().Be(new DateTime(2026, 6, 30));
        one.DaysHeld.Should().Be(29);
        one.OriginalDecisionSource.Should().Be("Catalyst");
        one.Outcome.Should().Be("Win");
        rows.Count(r => r.Symbol == "UNL.TO").Should().Be(2, "unlinked CLOSE records stay separate");
        rows.Where(r => r.Symbol == "UNL.TO").Should().OnlyContain(r => r.EpisodeResolution == EpisodeResolutions.Unresolved);
    }

    // â”€â”€ 9. No brokerage execution â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Risk_services_resolve_from_the_container_with_their_optional_dependencies()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddHttpContextAccessor();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddScoped<IRiskService, RiskService>();
        services.AddScoped<IStopSheetImportService, StopSheetImportService>();
        services.AddScoped<ISwingTradingService, SwingTradingService>();
        using var provider = services.BuildServiceProvider(new Microsoft.Extensions.DependencyInjection.ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetService(typeof(IRiskService)).Should().NotBeNull();
        scope.ServiceProvider.GetService(typeof(IStopSheetImportService)).Should().NotBeNull();
        scope.ServiceProvider.GetService(typeof(ISwingTradingService)).Should().NotBeNull();
    }

    [Fact]
    public void Risk_services_have_no_market_data_or_http_dependencies_so_they_cannot_place_orders()
    {
        var types = typeof(RiskService).Assembly.GetTypes().Where(t => t.Namespace == "PortfolioManager.Api.Services.Risk" && t.IsClass && !t.IsAbstract);
        var banned = new[] { "HttpClient", "IMarketDataProvider", "IHttpClientFactory", "YahooFinanceService" };
        foreach (var t in types)
            foreach (var c in t.GetConstructors())
                c.GetParameters().Select(p => p.ParameterType.Name).Should().NotIntersectWith(banned, t.Name);
    }
}
