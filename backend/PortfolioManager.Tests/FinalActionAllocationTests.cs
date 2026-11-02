using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using PortfolioManager.Api.Services.FinalAction;
using Xunit;

namespace PortfolioManager.Tests;

/// <summary>Symbol-level aggregation, mixed roles, accounts and explainability (Phase 2).</summary>
public sealed class FinalActionAllocationTests
{
    private static readonly DateTime Old = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

    private static PortfolioSummaryDto Lot(int id, string symbol, decimal shares, string role, string account = "TFSA", string sector = "Tech", decimal price = 100m) => new(
        new PortfolioItemDto(id, symbol, symbol, shares, 90m, sector, "", false, false, null, Old, "OPEN", account, Old, null, null, role),
        new StockQuote { Symbol = symbol, CurrentPrice = price }, PriceStructureResult.None);

    private static readonly RsiScanResult Scan = new()
    {
        Symbol = "MIX", CurrentPrice = 100m, Rsi = 58m, Sector = "Tech", ScannedAt = DateTime.UtcNow,
    };

    private static readonly SinglePositionLimit[] Limits =
    [
        new() { Role = "Strategic", TargetPct = 8m }, new() { Role = "Swing", TargetPct = 4m },
    ];

    private static readonly AllocationSectorTarget[] SectorTargets = [new() { Sector = "Tech", TargetPct = 10m }];

    private static (PortfolioEvidence Evidence, PortfolioItemContext Context) Evidence(
        PortfolioSummaryDto lot, IReadOnlyList<PortfolioSummaryDto> all, IReadOnlyList<SinglePositionLimit>? limits = null)
    {
        var open = all.Where(a => a.Item.TransactionType != "CLOSE").ToList();
        var stock = open.Sum(a => (a.Quote?.CurrentPrice ?? 0m) * a.Item.Shares);
        var grand = (double)stock;                          // no cash/options in these fixtures
        var ctx = FinalActionService.BuildContext(lot, Scan, all, grand);
        return (FinalActionService.BuildEvidence(lot, Scan, open, all, grand, stock, limits ?? Limits, SectorTargets, ctx), ctx);
    }

    private static List<PortfolioSummaryDto> Book(params PortfolioSummaryDto[] lots)
    {
        // Pad with an unrelated holding so the portfolio total is 100,000.
        var held = lots.Sum(l => (l.Quote?.CurrentPrice ?? 0m) * l.Item.Shares);
        return [.. lots, Lot(900, "BASE", (100_000m - held) / 100m, "Core", sector: "Other")];
    }

    // ── Aggregation across lots and accounts, with role-specific limits ──
    [Fact]
    public void SymbolExposure_AggregatesAllLotsAndAccounts_ButEachLotKeepsItsOwnRoleLimit()               // acceptance 12 / adj. 4
    {
        var strategic = Lot(1, "MIX", 20m, "Strategic", account: "TFSA");   // 2%
        var swing = Lot(2, "MIX", 10m, "Swing", account: "Margin");         // 1%
        var all = Book(strategic, swing);

        var (es, _) = Evidence(strategic, all);
        var (ew, _) = Evidence(swing, all);

        Assert.Equal(3.0, es.PositionSizePct!.Value, 6);                      // total symbol exposure on both rows
        Assert.Equal(3.0, ew.PositionSizePct!.Value, 6);
        Assert.Equal(8.0, es.PositionLimitPct);                               // Strategic lot: Strategic limit
        Assert.Equal(4.0, ew.PositionLimitPct);                               // Swing lot: Swing limit
        Assert.False(es.RolePolicyAmbiguous);
    }

    [Fact]
    public void AddOnTheStrategicLot_IsNotBlockedByTheSwingLotsTighterLimit()
    {
        var all = Book(Lot(1, "MIX", 20m, "Strategic"), Lot(2, "MIX", 10m, "Swing"));
        var (es, _) = Evidence(all[0], all);
        var d = new FinalActionDecision("Add / Staged Entry", "", "", "Active Buy Trigger", "", "", "", 0, 0, "", "ma-confirmed-buy", "", "", null, null);
        Assert.Equal("Add / Staged Entry", PortfolioEvidenceGate.Apply(d, "Strategic", es).Decision.FinalAction);   // 3% < Strategic 8%
    }

    [Fact]
    public void AddOnTheSwingLot_IsBlockedOnlyOnceTotalSymbolExposureReachesTheSwingLimit()
    {
        var below = Book(Lot(1, "MIX", 20m, "Strategic"), Lot(2, "MIX", 10m, "Swing"));                        // 3% < 4%
        var (eb, _) = Evidence(below[1], below);
        var d = new FinalActionDecision("Add to Trade", "", "", "Active Buy Trigger", "", "", "", 0, 0, "", "ma-confirmed-buy", "", "", null, null);
        Assert.Equal("Add to Trade", PortfolioEvidenceGate.Apply(d, "Swing", eb).Decision.FinalAction);

        var above = Book(Lot(1, "MIX", 20m, "Strategic"), Lot(2, "MIX", 25m, "Swing"));                        // 4.5% ≥ 4%
        var (ea, _) = Evidence(above[1], above);
        var blocked = PortfolioEvidenceGate.Apply(d, "Swing", ea);
        Assert.Equal("Hold / No Add", blocked.Decision.FinalAction);
        Assert.Contains(GateReasons.AddBlockedPositionLimit, blocked.ReasonCodes);
    }

    [Fact]
    public void UnrecognisedRoleWithMixedLots_IsNeedsReview_NotASilentlyChosenLimit()                          // adj. A
    {
        var all = Book(Lot(1, "MIX", 20m, "Strategic"), Lot(2, "MIX", 10m, "Income"));
        var (income, _) = Evidence(all[1], all);
        Assert.True(income.RolePolicyAmbiguous);
        Assert.Contains("Strategic limit 8%", income.RolePolicyDetail);
        var d = new FinalActionDecision("Add to Position", "", "", "Active Buy Trigger", "", "", "", 0, 0, "", "ma-confirmed-buy", "", "", null, null);
        var o = PortfolioEvidenceGate.Apply(d, "Income", income);
        Assert.Equal("Add / Needs Review", o.Decision.FinalAction);
        Assert.Contains(GateReasons.AddRolePolicyAmbiguous, o.ReasonCodes);
        Assert.Equal(DecisionBucket.None, DecisionEligibility.Classify(o.Decision.FinalAction, "Portfolio", 10m));   // never actionable
    }

    [Fact]
    public void NoConfiguredLimit_ForTheOnlyRole_IsNoConstraint_AndNeverAmbiguous()
    {
        var all = Book(Lot(1, "MIX", 20m, "Income"));
        var (e, _) = Evidence(all[0], all);
        Assert.False(e.RolePolicyAmbiguous);
        Assert.Null(e.PositionLimitPct);
    }

    [Fact]
    public void BlankSectorLot_StillCountsTowardItsSymbolsSector_AndSectorOverBlocksTheAdd()
    {
        // Tech target 10% (tolerance 2%): 8% from a sector-tagged lot + 5% from a blank-sector lot of the SAME symbol = 13% > 12%.
        var tagged = Lot(1, "MIX", 80m, "Strategic", sector: "Tech");
        var blank = Lot(2, "MIX", 50m, "Strategic", sector: "");
        var all = Book(tagged, blank);
        var (e, _) = Evidence(blank, all);
        Assert.True(e.SectorKnown);
        Assert.Equal(13.0, e.SectorPct!.Value, 6);
        Assert.True(e.SectorOverTarget);
    }

    [Fact]
    public void UnknownSector_AppliesNoSectorLimit_AndIsReportedAsUnknown()
    {
        var all = Book(Lot(1, "ZZZ", 10m, "Strategic", sector: ""));
        var scan = new RsiScanResult { Symbol = "ZZZ", CurrentPrice = 100m, Sector = "" };
        var stock = all.Sum(a => (a.Quote?.CurrentPrice ?? 0m) * a.Item.Shares);
        var ctx = FinalActionService.BuildContext(all[0], scan, all, (double)stock);
        var e = FinalActionService.BuildEvidence(all[0], scan, all, all, (double)stock, stock, Limits, SectorTargets, ctx);
        Assert.False(e.SectorKnown);
        Assert.False(e.SectorOverTarget);
    }

    [Fact]
    public void ProfitTakingTiers_StayLotLevel_AndAreNotChangedByOtherLots()                                  // adj. 6
    {
        var big = Lot(1, "MIX", 30m, "Swing");
        var small = Lot(2, "MIX", 10m, "Swing");
        var all = Book(big, small);
        var (_, ctxSmall) = Evidence(small, all);
        var (_, ctxBig) = Evidence(big, all);
        Assert.Equal(1.0, ctxSmall.PositionSizePct!.Value, 6);                 // this lot only (1%), not the 4% symbol total
        Assert.Equal(3.0, ctxBig.PositionSizePct!.Value, 6);
    }

    // ── Explainability ──
    private static FinalActionDecision Decision(string action, string ts = "Confirmed Constructive", string ms = "Uptrend") =>
        new(action, ts, "", ms, "", "", "No Gap", 0, 0, "", FinalActionEngine.FinalActionClass(action), "", "", null, null);

    private static PortfolioEvidence Ev(double? size = 3, double? limit = 8, double? gain = 5) =>
        new(gain, new DateOnly(2026, 8, 1), new DateOnly(2026, 10, 27), DateTime.UtcNow, size, limit, false, false, null,
            SectorPct: 6, SectorTargetPct: 10, SectorKnown: true);

    [Fact]
    public void ActionableAdd_ExplainsSignalExposureLimitAndSector()                                          // acceptance 11
    {
        var d = Decision("Add / Staged Entry", "Quality Trend Entry", "Active Buy Trigger");
        var (codes, text) = FinalActionExplainer.ExplainPortfolio(d, PortfolioEvidenceGate.Apply(d, "Strategic", Ev()), Ev(), "Strategic");
        Assert.Equal([FinalActionExplainer.AddEligible], codes);
        Assert.Contains("Active Buy Trigger", text);
        Assert.Contains("3.0%", text);
        Assert.Contains("Strategic limit of 8.0%", text);
        Assert.Contains("sector 6.0% vs target 10.0%", text);
    }

    [Fact]
    public void ActionableExit_NamesTheQualifyingRiskRule()
    {
        var d = Decision("Exit / Cut Loss", "Cooling", "Breakdown");
        var e = Ev(gain: -4);
        var o = PortfolioEvidenceGate.Apply(d, "Swing", e);
        var (codes, text) = FinalActionExplainer.ExplainPortfolio(o.Decision, o, e, "Swing");
        Assert.Contains(GateReasons.LossBreach, codes);
        Assert.Contains("below cost", text);
    }

    [Fact]
    public void ProfitTaking_NoAdd_And_RiskReview_AreExplained_ButPlainHoldsStayQuiet()
    {
        var none = new GateOutcome(Decision("Hold"), [], null);
        Assert.Empty(FinalActionExplainer.ExplainPortfolio(Decision("Hold"), none, Ev(), "Core").Codes);
        Assert.Null(FinalActionExplainer.ExplainPortfolio(Decision("Hold"), none, Ev(), "Core").Rationale);
        Assert.Empty(FinalActionExplainer.ExplainPortfolio(Decision("Hold / No Chase"), new GateOutcome(Decision("Hold / No Chase"), [], null), Ev(), "Core").Codes);

        var pt = Decision("Trim 33–50% / Trail Remainder");
        Assert.Equal([FinalActionExplainer.ProfitTakingRule],
            FinalActionExplainer.ExplainPortfolio(pt, new GateOutcome(pt, [], null), Ev(gain: 35), "Swing").Codes);

        var cool = Decision("Hold / No Add", "Cooling");
        var (cc, ct) = FinalActionExplainer.ExplainPortfolio(cool, new GateOutcome(cool, [], null), Ev(), "Core");
        Assert.Equal([FinalActionExplainer.NoAddCooling], cc);
        Assert.Contains("cooling", ct);

        var risk = Decision("Hold / Risk Review", "Waterfall / Falling Knife", "Warning");
        var (rc, rt) = FinalActionExplainer.ExplainPortfolio(risk, new GateOutcome(risk, [], null), Ev(), "Core");
        Assert.Equal([FinalActionExplainer.WarningNotInstruction], rc);
        Assert.Contains("not an instruction to trade", rt);
    }

    [Fact]
    public void GateBlockedHold_And_AwaitingData_KeepTheirOwnExplanation()
    {
        var d = Decision("Trim / Review", "Technical Caution", "Active Sell Trigger");
        var o = PortfolioEvidenceGate.Apply(d, "Strategic", Ev());
        var (codes, text) = FinalActionExplainer.ExplainPortfolio(o.Decision, o, Ev(), "Strategic");
        Assert.Contains(GateReasons.TechnicalOnly, codes);
        Assert.Contains("Original technical action: Trim / Review", text);
    }
}
