using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using PortfolioManager.Api.Services.Risk;

namespace PortfolioManager.Tests;

// Phase 0 safety assertions intentionally expose existing defects before production remediation.
// Only synthetic data in a uniquely named EF InMemory database; no API host or SQL Server connection.
public class Pm033RemediationReproductionTests
{
    private sealed class TestAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }
    private static AppDbContext NewDb()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        Pm033RiskTests.SimulateRowVersions(db);
        db.AccountTypes.Add(new AccountType { Name = "A", NormalizedName = "A" });
        db.SaveChanges();
        return db;
    }

    private static IHttpContextAccessor Principal(string uid = "u1") => new TestAccessor
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, uid), new Claim(ClaimTypes.Role, "Trader")], "test")),
        },
    };

    private static PortfolioService Portfolio(AppDbContext db, string uid = "u1") =>
        new(db, null!, new MutationClock(), Principal(uid));

    private static Task<PortfolioItemDto> Add(PortfolioService svc, string role = "Swing", int day = 1) =>
        svc.AddAsync(new AddPortfolioItemRequest("SYNTH.TO", "Synthetic", 100, 10, "OPEN", "A",
            new DateTime(2026, 9, day), HoldingRole: role));

    private static Task<UpdatePortfolioItemResponse?> Close(PortfolioService svc, int id) =>
        svc.UpdateAsync(id, new UpdatePortfolioItemRequest("Synthetic", 100, 10, TransactionType: "CLOSE",
            AccountType: "A", OpenDate: new DateTime(2026, 9, 1),
            CloseDate: new DateTime(2026, 9, 10), ClosingPrice: 12, HoldingRole: "Swing"));

    private static Task<RiskPositionDto> Stop(RiskService svc) => svc.SaveBrokerStopAsync(
        new SaveBrokerStopRequest(null, "A", "SYNTH.TO", "StopMarket", 9, null, 100,
            null, null, null, null, null, false, null));

    private static Task<RiskPositionDto> Plan(RiskService svc, string thesis, string? version = null) =>
        svc.SavePlanAsync(new SaveRiskPlanRequest("A", "SYNTH.TO", 9, null, "Fixed",
            null, thesis, null, null, version));

    private static SwingTradingService Workspace(AppDbContext db) =>
        new(db, Principal(), new ConfigurationBuilder().Build());

    [Fact]
    public async Task Finding02_existing_order_survives_later_non_swing_addition()
    {
        await using var db = NewDb();
        var portfolio = Portfolio(db);
        await Add(portfolio);
        await Stop(new RiskService(db, Principal()));
        await Add(portfolio, "Strategic", 2);

        var row = (await Workspace(db).GetOpenTradesAsync()).Rows.Single();
        row.TotalQuantity.Should().Be(200);
        row.SwingQuantity.Should().Be(100);
        row.BrokerStops.Should().ContainSingle("the actual account order has not disappeared or been cancelled");
        row.CoverageState.Should().Be(CoverageStates.Ambiguous);
    }

    [Fact]
    public async Task Finding03_last_swing_exit_closes_episode_but_preserves_continuing_account_order()
    {
        await using var db = NewDb();
        var portfolio = Portfolio(db);
        var swing = await Add(portfolio);
        await Stop(new RiskService(db, Principal()));
        var episode = (await db.PortfolioItems.FindAsync(swing.Id))!.TradeEpisodeId;
        await Add(portfolio, "Strategic", 2);
        await Close(portfolio, swing.Id);

        var order = await db.BrokerStopRecords.SingleAsync();
        order.UserStatus.Should().Be(BrokerUserStatuses.UserConfirmedEstablished);
        order.BrokerVerificationStatus.Should().Be(BrokerVerification.Unverified);
        order.CoveredQuantity.Should().Be(100);
        (await db.TradeEpisodes.SingleAsync(e => e.Id == episode)).Status.Should().Be(EpisodeStatuses.Closed);
    }

    [Fact]
    public async Task Continuing_strategic_exposure_keeps_old_order_visible_but_new_swing_episode_never_inherits_it()
    {
        await using var db = NewDb();
        var portfolio = Portfolio(db);
        var original = await Add(portfolio);
        var risk = new RiskService(db, Principal());
        await Stop(risk);
        var order = await db.BrokerStopRecords.SingleAsync();
        var identity = order.OrderIdentity;
        var oldEpisode = order.TradeEpisodeId;
        await Add(portfolio, "Strategic", 2);
        await Close(portfolio, original.Id);
        var closedWorkspace = await Workspace(db).GetOpenTradesAsync();
        closedWorkspace.Rows.Should().BeEmpty();
        closedWorkspace.BrokerOrderReminders.Single().Stop.OrderIdentity.Should().Be(identity);

        var reopened = await Add(portfolio, "Swing", 11);
        ((await db.PortfolioItems.FindAsync(reopened.Id))!.TradeEpisodeId == oldEpisode).Should().BeFalse();
        var workspace = await Workspace(db).GetOpenTradesAsync();
        workspace.Rows.Single().BrokerStops.Should().BeEmpty();
        workspace.Rows.Single().RiskStatuses.Should().Contain(RiskStatuses.NoBrokerProtection);
        workspace.BrokerOrderReminders.Single().Stop.OrderIdentity.Should().Be(identity);
        order.TradeEpisodeId.Should().Be(oldEpisode);
        order.TriggerPrice.Should().Be(9);
        order.UserStatus.Should().Be(BrokerUserStatuses.UserConfirmedEstablished);

        await risk.TransitionBrokerStopAsync(order.Id, new BrokerStopTransitionRequest(
            BrokerUserStatuses.UserReportedCancelled, null, "Synthetic broker confirmation", RiskSupport.Version(order.RowVersion)));
        (await Workspace(db).GetOpenTradesAsync()).BrokerOrderReminders.Should().BeEmpty();
        order.BrokerVerificationStatus.Should().Be(BrokerVerification.Unverified);
        (await db.RiskAuditEvents.OrderByDescending(e => e.Id).FirstAsync()).TradeEpisodeId.Should().Be(oldEpisode);
    }

    [Fact]
    public async Task Legacy_date_only_order_is_not_inherited_into_a_new_persisted_episode_on_the_same_day()
    {
        await using var db = NewDb();
        await Add(Portfolio(db));
        var position = (await new RiskService(db, Principal()).LoadPositionAsync("A", "SYNTH.TO", default)).Position;
        position.Should().NotBeNull();
        RiskService.BelongsToEpisode(null, position!.EpisodeOpenDate, position).Should().BeFalse();
    }

    [Fact]
    public async Task Stop_import_identity_cannot_be_reused_across_owners()
    {
        await using var db = NewDb();
        await Add(Portfolio(db));
        db.BrokerStopRecords.Add(new BrokerStopRecord
        {
            UserId = "u2", AccountType = "A", Symbol = "SYNTH.TO", TriggerPrice = 9,
            ImportKey = "synthetic:1:1", Currency = "CAD",
        });
        await db.SaveChangesAsync();
        var result = await new StopSheetImportService(db, Principal()).ImportAsync(new StopSheetRequest(
            "synthetic", "1", [new StopSheetRow("SYNTH.TO", 9, "StopMarket", "CAD", "A", 1)]));
        result.Rows.Single().Action.Should().Be(StopSheetActions.Conflict);
        result.Rows.Single().ExistingStop.Should().BeNull();
        (await db.BrokerStopRecords.SingleAsync()).UserId.Should().Be("u2");
    }

    [Fact]
    public async Task Risk_writes_use_the_resolved_canonical_account_scope()
    {
        await using var db = NewDb();
        await Add(Portfolio(db));
        var risk = new RiskService(db, Principal());
        var saved = await risk.SavePlanAsync(new SaveRiskPlanRequest(" A ", " synth.to ", 9, null, "Fixed",
            null, "Synthetic", null, null, null));
        saved.AccountType.Should().Be("A");
        (await db.RiskPlans.SingleAsync()).AccountType.Should().Be("A");
        (await db.RiskAuditEvents.SingleAsync()).AccountType.Should().Be("A");
        (await risk.GetPositionAsync(" A ", "SYNTH.TO")).Plan!.Id.Should().Be(saved.Plan!.Id);
    }

    [Fact]
    public void Provider_source_time_is_required_and_checked_against_the_actual_completed_session()
    {
        var now = new DateTime(2026, 10, 28, 22, 15, 0, DateTimeKind.Utc);
        var captured = new DateTimeOffset(now).ToUnixTimeSeconds();
        var close = new DateTimeOffset(new DateTime(2026, 10, 28, 20, 0, 0, DateTimeKind.Utc)).ToUnixTimeSeconds();
        PriceEvidence.FromQuote(10, close + 900, "CLOSED", "SYNTH.TO", now.AddHours(-2), close).Basis.Should().Be(PriceBases.Stale,
            "the existing calendar's two-hour evidence grace is preserved");
        PriceEvidence.FromQuote(10, captured, "CLOSED", "SYNTH.TO", now, close).Basis.Should().Be(PriceBases.Eod);
        PriceEvidence.FromQuote(10, captured, "CLOSED", "SYNTH.TO", now, close - 3600).Basis.Should().Be(PriceBases.Stale);
        PriceEvidence.FromQuote(10, captured, "CLOSED", "SYNTH.TO", now, close - 86400).Basis.Should().Be(PriceBases.Stale);
        PriceEvidence.FromQuote(10, captured, "CLOSED", "SYNTH.TO", now, captured + 60).Basis.Should().Be(PriceBases.Stale);
        PriceEvidence.FromQuote(10, captured, "", "SYNTH.TO", now, close).Basis.Should().Be(PriceBases.Stale);
        PriceEvidence.FromQuote(10, captured, "CLOSED", "SYNTH.TO", now).IsUsable.Should().BeFalse();
    }

    [Fact]
    public async Task Finding03_protected_delete_refuses_before_losing_lot_identity()
    {
        await using var db = NewDb();
        var portfolio = Portfolio(db);
        var lot = await Add(portfolio);
        await Stop(new RiskService(db, Principal()));

        Func<Task> act = () => portfolio.DeleteAsync(lot.Id);
        await act.Should().ThrowAsync<RiskValidationException>("protected episode history cannot be silently disconnected");
        (await db.PortfolioItems.FindAsync(lot.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task Finding03_account_rename_preserves_risk_scope()
    {
        await using var db = NewDb();
        await Add(Portfolio(db));
        await Stop(new RiskService(db, Principal()));
        var account = await db.AccountTypes.SingleAsync();
        await new AccountTypeService(db).RenameAsync(account.Id,
            new UpdateAccountTypeRequest("Renamed", account.Version), CancellationToken.None);

        (await db.PortfolioItems.SingleAsync()).AccountType.Should().Be("Renamed");
        (await db.BrokerStopRecords.SingleAsync()).AccountType.Should().Be("Renamed");
        (await db.TradeEpisodes.SingleAsync()).AccountType.Should().Be("Renamed");
    }

    [Theory]
    [InlineData("u2")]
    [InlineData(null)]
    public async Task Finding04_new_lot_never_joins_another_owner_or_legacy_partition(string? existingOwner)
    {
        await using var db = NewDb();
        var foreignEpisode = new TradeEpisode
        {
            UserId = existingOwner, AccountType = "A", Symbol = "SYNTH.TO", OpenDate = new DateTime(2026, 9, 1),
        };
        db.TradeEpisodes.Add(foreignEpisode);
        db.PortfolioItems.Add(new PortfolioItem
        {
            UserId = existingOwner, Symbol = "SYNTH.TO", CompanyName = "Synthetic", AccountType = "A",
            Shares = 100, AverageCostBasis = 10, TransactionType = "OPEN", HoldingRole = "Swing",
            OpenDate = foreignEpisode.OpenDate, TradeEpisodeId = foreignEpisode.Id,
        });
        await db.SaveChangesAsync();

        var added = await Add(Portfolio(db), day: 2);
        var id = (await db.PortfolioItems.FindAsync(added.Id))!.TradeEpisodeId;
        id.Should().NotBe(foreignEpisode.Id, "visibility must not merge exact ownership partitions");
        id.Should().NotBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("CLOSED")]
    public void Finding05_capture_timestamp_alone_does_not_prove_a_completed_close(string? state)
    {
        var now = new DateTime(2026, 10, 28, 23, 0, 0, DateTimeKind.Utc);
        var evidence = PriceEvidence.FromQuote(8, new DateTimeOffset(now).ToUnixTimeSeconds(), state, "SYNTH.TO", now);
        evidence.Basis.Should().NotBe(PriceBases.Eod, "source price/session provenance was never supplied");
        StopRiskEvaluator.Evaluate(new RiskEvalInput(evidence, 9, [], 100, 100, 5))
            .Statuses.Should().NotContain(RiskStatuses.StopBreached);
    }

    [Fact]
    public async Task Finding07_retired_order_can_be_explicitly_resolved_without_reactivation()
    {
        await using var db = NewDb();
        var portfolio = Portfolio(db);
        var lot = await Add(portfolio);
        var risk = new RiskService(db, Principal());
        var position = await Stop(risk);
        await Close(portfolio, lot.Id);
        var retired = await db.BrokerStopRecords.SingleAsync();
        retired.BrokerCancellationUnverified.Should().BeTrue();

        var resolved = await risk.TransitionBrokerStopAsync(position.BrokerStops.Single().Id,
            new BrokerStopTransitionRequest(BrokerUserStatuses.UserReportedCancelled, null,
                "Synthetic statement confirmation", RiskSupport.Version(retired.RowVersion)));
        resolved.BrokerStops.Single().UserStatus.Should().Be(BrokerUserStatuses.UserReportedCancelled);
        resolved.BrokerStops.Single().BrokerCancellationUnverified.Should().BeFalse();
        resolved.BrokerStops.Single().BrokerVerificationStatus.Should().Be(BrokerVerification.Unverified);
    }

    [Fact]
    public async Task Finding08_new_episode_plan_does_not_erase_original_journal_thesis()
    {
        await using var db = NewDb();
        var portfolio = Portfolio(db);
        var lot = await Add(portfolio);
        var risk = new RiskService(db, Principal());
        var original = await Plan(risk, "Original synthetic thesis");
        await Close(portfolio, lot.Id);
        await Add(portfolio, day: 20);
        await Plan(risk, "New synthetic thesis", original.Plan!.RowVersion);

        var journal = await Workspace(db).GetJournalAsync();
        journal.Single().Thesis.Should().Be("Original synthetic thesis");
        journal.Single().RiskHistory.Should().Contain(h => h.Summary.Contains("Original synthetic thesis"));
        journal.Single().RiskHistory.Should().NotContain(h => h.Summary.Contains("New synthetic thesis"));
        (await db.RiskPlans.FindAsync(original.Plan.Id))!.TradeRationale.Should().Be("Original synthetic thesis");
        (await db.RiskPlans.CountAsync()).Should().Be(2);
    }

    private static PriceEvidence Live() => new(20, DateTime.UtcNow, new DateOnly(2026, 10, 28), PriceBases.Live, "synthetic");

    [Fact]
    public void Finding09_broker_order_does_not_hide_missing_plan()
    {
        StopRiskEvaluator.Evaluate(new RiskEvalInput(Live(), null,
            [new StopLite(1, 9, 100, BrokerVerification.Unverified)], 100, 100, 5))
            .Statuses.Should().Contain(RiskStatuses.MissingRiskPlan);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(50)]
    public void Finding09_unknown_or_mixed_allocation_does_not_hide_known_excess(decimal swingQuantity)
    {
        StopRiskEvaluator.Evaluate(new RiskEvalInput(Live(), 9,
            [new StopLite(1, 9, 200, BrokerVerification.Unverified), new StopLite(2, 9, null, BrokerVerification.Unverified)],
            swingQuantity, 100, 5)).Statuses.Should().Contain(RiskStatuses.OverCovered);
    }

    [Fact]
    public void Finding09_medium_partial_protection_requires_attention()
    {
        var evaluation = StopRiskEvaluator.Evaluate(new RiskEvalInput(Live(), 9,
            [new StopLite(1, 9, 50, BrokerVerification.Unverified)], 100, 100, 5));
        evaluation.Statuses.Should().Contain(RiskStatuses.PartialProtection);
        evaluation.NeedsAttention.Should().BeTrue();
    }

    [Fact]
    public async Task Finding10_existing_plan_update_requires_a_version()
    {
        await using var db = NewDb();
        await Add(Portfolio(db));
        var risk = new RiskService(db, Principal());
        await Plan(risk, "Original");
        Func<Task> act = () => Plan(risk, "Unversioned change");
        await act.Should().ThrowAsync<RiskValidationException>();
        (await db.RiskPlans.SingleAsync()).TradeRationale.Should().Be("Original");
    }
}
