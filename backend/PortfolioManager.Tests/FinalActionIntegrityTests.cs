using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using PortfolioManager.Api.Services.FinalAction;
using PortfolioManager.Api.Services.Risk;

namespace PortfolioManager.Tests;

/// <summary>
/// PM-024 Stage 2-4: stop-risk integration through the single IOpenRiskReader/StopRiskEvaluator path, FX-safe allocation
/// evidence and ownership transparency. In-memory databases and synthetic rows only.
/// </summary>
public sealed class FinalActionIntegrityTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly DateTime Stamp = new(2026, 10, 20, 21, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ClockUtc = new(2026, 10, 28, 15, 0, 0, DateTimeKind.Utc);   // Wednesday, regular session
    private static DateOnly Expected => TradingCalendar.LastCompletedSession(DateTime.UtcNow, MarketExchange.Nyse);

    private sealed class FixedClock(DateTime utc) : TimeProvider { public override DateTimeOffset GetUtcNow() => new(utc, TimeSpan.Zero); }

    private sealed class FixedQuoteReader(OpenRiskReader inner, Dictionary<string, StockQuote> quotes) : IOpenRiskReader
    {
        public int Calls { get; private set; }
        public Task<OpenRiskSnapshot> ReadAsync(RiskScope scope, IReadOnlyDictionary<string, StockQuote> _, CancellationToken ct = default)
        {
            Calls++;
            return inner.ReadAsync(scope, quotes, ct);
        }
    }

    private sealed class FakeScanner(params RsiScanResult[] results) : IRsiScannerService
    {
        public Task<List<RsiScanResult>> AnalyzeSymbolsAsync(IEnumerable<string> symbols, decimal oversoldThreshold = 30m,
            decimal overboughtThreshold = 75m, string logicMode = "Legacy", string? userId = null, CancellationToken ct = default)
        {
            var wanted = symbols.ToHashSet(StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(results.Where(r => wanted.Contains(r.Symbol)).ToList());
        }

        public Task<ScannerResponse> ScanAsync(IEnumerable<string>? extraSymbols = null, decimal oversoldThreshold = 30m,
            decimal overboughtThreshold = 75m, string logicMode = "Legacy", string? userId = null, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    /// <summary>RSI 75.7 uptrend: engine says "Hold / Trail Stop — No Add" for a Swing lot.</summary>
    private static RsiScanResult OverboughtScan(string symbol) => new()
    {
        Symbol = symbol, AnalysisTicker = symbol, AnalysisCurrency = "CAD", Rsi = 75.7m, CurrentPrice = 100m, ChangePercent = 0.5m,
        DayHigh = 101m, DayLow = 98m, OpenPrice = 99.5m, PreviousClose = 99.4m, VolumeRatio = 1.0m,
        RsiSignal = 74m, RsiSignalAvailable = true, MacdHistogram = 0.4m, MacdHistDelta = 0.1m,
        Ema9Price = 99m, Ema10Price = 98m, Ema20Price = 96m, Sma20Price = 95m, Sma50Price = 90m, Sma200 = 80m,
        Status = SignalStatus.Neutral, ScanType = ScanType.Neutral, Week52High = 101m, TradingDate = Expected,
    };

    private static PortfolioSummaryDto Holding(int id, string symbol, string role, string type = "OPEN", string account = "MARGIN",
        decimal shares = 100m, decimal price = 100m, string? currency = null) => new(
        new PortfolioItemDto(id, symbol, symbol, shares, 90m, "Tech", "", false, false, null, Stamp, type, account, Stamp.AddDays(-30), null, null,
            role, null, null, null, "Hold", "hold", "INFORMATIONAL", Stamp),
        new StockQuote { Symbol = symbol, CurrentPrice = price, Currency = currency },
        PriceStructureResult.None);

    private static PortfolioItem Lot(int id, string symbol, decimal shares = 100m, string role = "Swing", string account = "MARGIN") => new()
    {
        Id = id, Symbol = symbol, CompanyName = symbol, Shares = shares, AverageCostBasis = 90m, AccountType = account, TransactionType = "OPEN",
        OpenDate = new DateTime(2026, 9, 1), HoldingRole = role, UserId = "u",
    };

    private static StockQuote LiveQuote(string symbol, decimal price, bool provenTimestamp = true) => new()
    {
        Symbol = symbol, CurrentPrice = price, MarketState = "REGULAR", Currency = "CAD",
        Timestamp = new DateTimeOffset(ClockUtc.AddMinutes(-5)).ToUnixTimeSeconds(),
        SourcePriceTimestamp = provenTimestamp ? new DateTimeOffset(ClockUtc.AddMinutes(-10)).ToUnixTimeSeconds() : null,
    };

    private sealed record Setup(FinalActionService Service, AppDbContext Db, FixedQuoteReader Reader);

    private static async Task<Setup> SetUpAsync(
        IEnumerable<PortfolioSummaryDto> holdings, IEnumerable<PortfolioItem> lots, Dictionary<string, StockQuote> riskQuotes,
        IEnumerable<string> watch, RiskPlan? plan = null, IEnumerable<BrokerStopRecord>? stops = null, IEnumerable<CashItem>? cash = null,
        params RsiScanResult[] scans)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        foreach (var w in watch) db.WatchlistItems.Add(new WatchlistItem { UserId = "u", Symbol = w, Role = "Strategic" });
        foreach (var n in new[] { "MARGIN", "TFSA" }) db.AccountTypes.Add(new AccountType { Name = n, NormalizedName = n });
        db.PortfolioItems.AddRange(lots);
        if (plan is not null) db.RiskPlans.Add(plan);
        if (stops is not null) db.BrokerStopRecords.AddRange(stops);
        if (cash is not null) db.CashItems.AddRange(cash);
        db.PortfolioSnapshots.Add(new PortfolioSnapshot { UserId = "u", SnapshotJson = JsonSerializer.Serialize(holdings.ToList(), Json) });
        await db.SaveChangesAsync();
        var snap = new PortfolioSnapshotService(db, NullLogger<PortfolioSnapshotService>.Instance);
        var reader = new FixedQuoteReader(new OpenRiskReader(db, new ConfigurationBuilder().Build(), new FixedClock(ClockUtc)), riskQuotes);
        var service = new FinalActionService(db, new FakeScanner(scans), snap, new MemoryCache(new MemoryCacheOptions()),
            NullLogger<FinalActionService>.Instance, reader);
        return new Setup(service, db, reader);
    }

    private static RiskPlan Plan(string symbol, decimal stop) => new() { UserId = "u", AccountType = "MARGIN", Symbol = symbol, PlannedStopPrice = stop, EpisodeOpenDate = new DateTime(2026, 9, 1) };

    private static BrokerStopRecord Stop(string symbol, decimal trigger, decimal? covered, string verification = BrokerVerification.Unverified) => new()
    {
        UserId = "u", AccountType = "MARGIN", Symbol = symbol, TriggerPrice = trigger, CoveredQuantity = covered, Currency = "CAD",
        BrokerVerificationStatus = verification, EpisodeOpenDate = new DateTime(2026, 9, 1),
    };

    private static async Task<FinalActionResultDto> DisAsync(
        decimal observed, RiskPlan? plan, IEnumerable<BrokerStopRecord>? stops = null, bool provenTimestamp = true, decimal shares = 100m)
    {
        var s = await SetUpAsync([Holding(1, "DIS.TO", "Swing", shares: shares)], [Lot(1, "DIS.TO", shares)],
            new() { ["DIS.TO"] = LiveQuote("DIS.TO", observed, provenTimestamp) }, [], plan, stops, null, OverboughtScan("DIS.TO"));
        return Assert.Single((await s.Service.ComputeAsync("u")).Portfolio);
    }

    // â”€â”€ Stop-risk integration â”€â”€

    [Fact]
    public async Task Dis_PlannedStopClear_OverboughtSwing_StaysTrailStopAdvisory_WithClearEvidence()
    {
        var r = await DisAsync(100m, Plan("DIS.TO", 95m));
        Assert.Equal("Hold / Trail Stop — No Add", r.FinalAction);
        Assert.False(r.IsActionable);
        Assert.Equal("Clear", r.Risk!.PlannedStopState);
        Assert.False(r.Risk.PlannedBreached);
    }

    [Fact]
    public async Task PlannedStopBreach_EscalatesToReviewStopBreach_WithObservationEvidence_AndNeverClaimsAFill()
    {
        var r = await DisAsync(94m, Plan("DIS.TO", 95m));
        Assert.Equal("Review Stop Breach", r.FinalAction);
        Assert.Equal("REQUIRED", r.Priority);
        Assert.Equal(DecisionBucket.RiskReview, DecisionEligibility.Classify(r));
        Assert.True(r.IsActionable);
        Assert.Contains(GateReasons.PlannedStopBreached, r.ReasonCodes!);
        Assert.Equal("Breached", r.Risk!.PlannedStopState);
        Assert.Equal(94m, r.Risk.ObservedPrice);
        Assert.Equal(95m, r.Risk.PlannedStop);
        Assert.NotNull(r.Risk.ObservedAtUtc);
        Assert.Equal("Live", r.Risk.PriceBasis);
        Assert.Contains("not a confirmed brokerage execution", r.Reason);
        Assert.DoesNotContain("sold", r.Reason!.ToLowerInvariant().Replace("nothing has been sold", ""));
        Assert.Equal("Hold / Trail Stop — No Add", r.OriginalAction);   // the technical reading is preserved, not lost
    }

    [Fact]
    public async Task BrokerOnlyBreach_IsReportedSeparately_FromPlannedStop()
    {
        // Planned stop is below the price (clear), the recorded broker stop is above it (breached).
        var r = await DisAsync(94m, Plan("DIS.TO", 90m), [Stop("DIS.TO", 95m, 100m)]);
        Assert.Equal("Review Stop Breach", r.FinalAction);
        Assert.Contains(GateReasons.BrokerStopBreached, r.ReasonCodes!);
        Assert.DoesNotContain(GateReasons.PlannedStopBreached, r.ReasonCodes!);
        Assert.False(r.Risk!.PlannedBreached);
        Assert.True(r.Risk.BrokerBreached);
        Assert.Equal(95m, r.Risk.BrokerStop);
        Assert.Contains("recorded broker stop", r.Reason);
    }

    [Fact]
    public async Task StaleObservation_IsUnverified_NeitherClearNorBreached()
    {
        // The quote carries no provider timestamp, so it cannot prove either state.
        var r = await DisAsync(94m, Plan("DIS.TO", 95m), provenTimestamp: false);
        Assert.Equal("Hold / Trail Stop — No Add", r.FinalAction);
        Assert.Equal("Unverified", r.Risk!.PlannedStopState);
        Assert.False(r.Risk.PlannedBreached);
        Assert.Equal("Stale", r.Risk.PriceBasis);
        Assert.Contains(GateReasons.StopEvidenceUnverified, r.ReasonCodes!);
    }

    [Fact]
    public async Task BrokerRecordedButUnverified_IsNeverTreatedAsFilledOrCancelled()
    {
        var r = await DisAsync(100m, Plan("DIS.TO", 95m), [Stop("DIS.TO", 95m, 100m, BrokerVerification.Unverified)]);
        Assert.Equal(new[] { BrokerVerification.Unverified }, r.Risk!.BrokerVerification);
        Assert.Equal("Hold / Trail Stop — No Add", r.FinalAction);        // an unverified order changes nothing about the decision
        Assert.Equal(DecisionBucket.None, DecisionEligibility.Classify(r));
    }

    [Fact]
    public async Task PartialBrokerCoverage_IsReportedAccurately()
    {
        var r = await DisAsync(100m, Plan("DIS.TO", 95m), [Stop("DIS.TO", 95m, 40m)]);
        Assert.Equal(CoverageStates.Partial, r.Risk!.CoverageState);
        Assert.Contains(GateReasons.StopCoveragePartial, r.ReasonCodes!);
    }

    [Fact]
    public async Task UnknownCoverage_IsNotReportedAsProtected()
    {
        var r = await DisAsync(100m, Plan("DIS.TO", 95m), [Stop("DIS.TO", 95m, null)]);
        Assert.Equal(CoverageStates.Unknown, r.Risk!.CoverageState);
        Assert.Contains(GateReasons.StopCoverageUnknown, r.ReasonCodes!);
    }

    [Fact]
    public async Task VerifiedBreach_DoesNotWeakenAnIndependentlyJustifiedExit()
    {
        // Same lot, but the technical result is a confirmed Sell Trigger on a Swing position after entry: the exit stands.
        var scan = OverboughtScan("DIS.TO");
        scan.CurrentPrice = 98.5m; scan.MacdHistDelta = -0.2m; scan.VolumeRatio = 1.5m;
        var s = await SetUpAsync([Holding(1, "DIS.TO", "Swing", price: 98.5m)], [Lot(1, "DIS.TO")],
            new() { ["DIS.TO"] = LiveQuote("DIS.TO", 94m) }, [], Plan("DIS.TO", 95m), null, null, scan);
        var r = Assert.Single((await s.Service.ComputeAsync("u")).Portfolio);
        Assert.Equal("Exit / Take Profit", r.FinalAction);
        Assert.Contains(GateReasons.PlannedStopBreached, r.ReasonCodes!);
        Assert.Contains("not a confirmed brokerage execution", r.Reason);
    }

    [Fact]
    public async Task Risk_IsReadOnceForAllLots_AndNonSwingLotsCarryNoStopClaims()
    {
        var holdings = new[] { Holding(1, "DIS.TO", "Swing"), Holding(2, "DIS.TO", "Swing"), Holding(3, "BRK.TO", "Strategic") };
        var lots = new[] { Lot(1, "DIS.TO", 50), Lot(2, "DIS.TO", 50), Lot(3, "BRK.TO", 100, "Strategic") };
        var s = await SetUpAsync(holdings, lots, new() { ["DIS.TO"] = LiveQuote("DIS.TO", 100m) }, [], Plan("DIS.TO", 95m), null, null,
            OverboughtScan("DIS.TO"), OverboughtScan("BRK.TO"));
        var snap = await s.Service.ComputeAsync("u");
        Assert.Equal(1, s.Reader.Calls);
        Assert.Equal(3, snap.Portfolio.Count);
        Assert.All(snap.Portfolio.Where(p => p.Symbol == "DIS.TO"), p => Assert.Equal("Clear", p.Risk!.PlannedStopState));
        Assert.Null(snap.Portfolio.Single(p => p.Symbol == "BRK.TO").Risk);
    }

    [Fact]
    public async Task NoReader_OrReaderFailure_LeavesStopEvidenceAbsent_NeverClear()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.PortfolioSnapshots.Add(new PortfolioSnapshot { UserId = "u", SnapshotJson = JsonSerializer.Serialize(new[] { Holding(1, "DIS.TO", "Swing") }, Json) });
        await db.SaveChangesAsync();
        var service = new FinalActionService(db, new FakeScanner(OverboughtScan("DIS.TO")),
            new PortfolioSnapshotService(db, NullLogger<PortfolioSnapshotService>.Instance), new MemoryCache(new MemoryCacheOptions()),
            NullLogger<FinalActionService>.Instance, new ThrowingReader());
        var r = Assert.Single((await service.ComputeAsync("u")).Portfolio);
        Assert.Null(r.Risk);
        Assert.Equal("Hold / Trail Stop — No Add", r.FinalAction);
    }

    private sealed class ThrowingReader : IOpenRiskReader
    {
        public Task<OpenRiskSnapshot> ReadAsync(RiskScope scope, IReadOnlyDictionary<string, StockQuote> quotes, CancellationToken ct = default) =>
            throw new InvalidOperationException("boom");
    }

    [Fact]
    public async Task SwingTradingGrid_AndFinalAction_AgreeOnPlannedStopState()
    {
        var s = await SetUpAsync([Holding(1, "DIS.TO", "Swing")], [Lot(1, "DIS.TO")], new() { ["DIS.TO"] = LiveQuote("DIS.TO", 94m) }, [],
            Plan("DIS.TO", 95m), null, null, OverboughtScan("DIS.TO"));
        var final = Assert.Single((await s.Service.ComputeAsync("u")).Portfolio);
        var snap = await new OpenRiskReader(s.Db, new ConfigurationBuilder().Build(), new FixedClock(ClockUtc))
            .ReadAsync(new RiskScope("u", false), new Dictionary<string, StockQuote> { ["DIS.TO"] = LiveQuote("DIS.TO", 94m) });
        Assert.Equal(snap.Positions.Single().Evaluation.PlannedStopState, final.Risk!.PlannedStopState);
    }

    // â”€â”€ Gate-level: escalation precedence â”€â”€

    private static PortfolioEvidence Ev(StopRiskEvidence? risk, double? size = 3.5, double? limit = 10) =>
        new(5, new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 27), new DateTime(2026, 10, 27, 16, 0, 0, DateTimeKind.Utc), size, limit,
            false, false, "Rebalance", Risk: risk);

    private static StopRiskEvidence Breach() => new("Breached", 95m, null, true, false, 94m, new DateTime(2026, 10, 27, 15, 0, 0, DateTimeKind.Utc),
        "Live", "None", "No Stop", [], true, "MARGIN", null);

    private static FinalActionDecision Decision(string action) => new(action, "Constructive Extended", "", "Warning — Overbought Run", "",
        "Stand By", "No Gap", 0, 0, "", FinalActionEngine.FinalActionClass(action), "", "", null, null);

    [Theory]
    [InlineData("Add to Trade")]
    [InlineData("Hold / Trail Stop")]
    [InlineData("Hold Swing")]
    [InlineData("Hold / Trail Stop — No Add")]
    public void Breach_EscalatesEveryNonReductionOutcome(string action)
    {
        var o = PortfolioEvidenceGate.Apply(Decision(action), "Swing", Ev(Breach()));
        Assert.Equal("Review Stop Breach", o.Decision.FinalAction);
        Assert.Equal(action, o.OriginalAction);
    }

    [Theory]
    [InlineData("Exit / Cut Loss")]
    [InlineData("Cut Loss / Exit")]
    public void Breach_NeverDowngradesAJustifiedLossExit(string action)
    {
        var e = Ev(Breach()) with { UnrealizedGainPct = -4 };
        var o = PortfolioEvidenceGate.Apply(Decision(action), "Swing", e);
        Assert.Equal(action, o.Decision.FinalAction);
        Assert.Contains(GateReasons.PlannedStopBreached, o.ReasonCodes);
    }

    // â”€â”€ FX-safe allocation â”€â”€

    [Fact]
    public void ValuationClassification_SingleMixedAndCashMarkers()
    {
        var cad = FinalActionService.ClassifyValuation([("AAA.TO", null), ("BBB.TO", "CAD")], [("TFSA_L_TD", "CASH")], []);
        Assert.True(cad.Reliable);
        Assert.Equal(ValuationCheck.SingleCurrency, cad.Status);

        var mixed = FinalActionService.ClassifyValuation([("AAA.TO", null), ("MSFT", "USD")], [], []);
        Assert.False(mixed.Reliable);
        Assert.Equal(ValuationCheck.MixedCurrency, mixed.Status);

        var usdCash = FinalActionService.ClassifyValuation([("AAA.TO", null)], [("Margin_USD", "CASH")], []);
        Assert.False(usdCash.Reliable);

        var usdOption = FinalActionService.ClassifyValuation([("AAA.TO", null)], [], ["NVDA"]);
        Assert.False(usdOption.Reliable);
    }

    [Fact]
    public void UnreliableAllocation_NeverApprovesAnAdd_AndNeverCitesAConcentrationBreach()
    {
        var mixed = new ValuationCheck(ValuationCheck.MixedCurrency, "the portfolio total mixes CAD and USD with no verified conversion");
        var ev = new PortfolioEvidence(5, new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 27), DateTime.UtcNow, 3.5, 10, false, false, "Rebalance", Valuation: mixed);

        var add = PortfolioEvidenceGate.Apply(Decision("Add / Staged Entry"), "Strategic", ev);
        Assert.Equal("Add / Needs Review", add.Decision.FinalAction);
        Assert.Contains(GateReasons.AllocationUnavailable, add.ReasonCodes);
        Assert.Contains("Allocation cannot be verified", add.Decision.FinalActionReason);
        Assert.Equal(DecisionBucket.None, DecisionEligibility.Classify(add.Decision.FinalAction, "Portfolio", 10m));

        // A raw (invalid) over-limit figure must not justify a Trim when the denominator is unreliable.
        var trim = PortfolioEvidenceGate.Apply(Decision("Trim / Review"), "Strategic", ev with { PositionSizePct = 40 });
        Assert.Equal("Hold / Monitor", trim.Decision.FinalAction);
        Assert.DoesNotContain(GateReasons.AllocationBreach, trim.ReasonCodes);
    }

    [Fact]
    public void UnreliableAllocation_PreservesIndependentTechnicalAndStopEvidence()
    {
        var mixed = new ValuationCheck(ValuationCheck.MixedCurrency, "mixed");
        var ev = Ev(Breach()) with { Valuation = mixed, HardStructuralNegative = true };
        var o = PortfolioEvidenceGate.Apply(Decision("Trim / Review"), "Strategic", ev);
        Assert.Equal("Trim / Review", o.Decision.FinalAction);
        Assert.Contains(GateReasons.StructuralBreakdown, o.ReasonCodes);
    }

    [Fact]
    public async Task MixedCurrencyBook_NullsWeights_AndFlagsAllocationStatus()
    {
        var s = await SetUpAsync([Holding(1, "AAA.TO", "Strategic"), Holding(2, "MSFT", "Strategic", currency: "USD")],
            [], new(), [], null, null, null, OverboughtScan("AAA.TO"), OverboughtScan("MSFT"));
        var snap = await s.Service.ComputeAsync("u");
        Assert.All(snap.Portfolio, r =>
        {
            Assert.Equal(ValuationCheck.MixedCurrency, r.AllocationStatus);
            Assert.Null(r.Ownership!.ExposurePct);
        });
    }

    [Fact]
    public async Task SingleCurrencyBook_KeepsVerifiedWeights_AndConfiguredLimitsStillApply()
    {
        var s = await SetUpAsync([Holding(1, "AAA.TO", "Strategic"), Holding(2, "BBB.TO", "Strategic")], [], new(), [], null, null, null,
            OverboughtScan("AAA.TO"), OverboughtScan("BBB.TO"));
        var snap = await s.Service.ComputeAsync("u");
        Assert.All(snap.Portfolio, r =>
        {
            Assert.Equal(ValuationCheck.SingleCurrency, r.AllocationStatus);
            Assert.Equal(50.0, r.Ownership!.ExposurePct!.Value, 1);
        });
    }

    // â”€â”€ Ownership â”€â”€

    [Fact]
    public async Task MultiLotMultiAccount_IsOneOwnership_AndMixedRolesAreReported()
    {
        var holdings = new[]
        {
            Holding(1, "XYZ.TO", "Strategic", account: "TFSA", shares: 100m), Holding(2, "XYZ.TO", "Swing", account: "MARGIN", shares: 50m),
            Holding(3, "OTHER.TO", "Strategic", shares: 100m),
        };
        var s = await SetUpAsync(holdings, [], new(), [], null, null, null, OverboughtScan("XYZ.TO"), OverboughtScan("OTHER.TO"));
        var snap = await s.Service.ComputeAsync("u");
        foreach (var r in snap.Portfolio.Where(p => p.Symbol == "XYZ.TO"))
        {
            Assert.Equal(2, r.Ownership!.OpenLots);
            Assert.Equal(new[] { "MARGIN", "TFSA" }, r.Ownership.Accounts);
            Assert.True(r.Ownership.MixedRoles);
            Assert.Equal(60.0, r.Ownership.ExposurePct!.Value, 1);   // 150 of 250 shares at the same price
        }
    }

    [Fact]
    public async Task ClosedLots_AreNeverCurrentExposure_AndAClosedOnlyTickerIsNotOwned()
    {
        var holdings = new[]
        {
            Holding(1, "KEEP.TO", "Strategic"), Holding(2, "KEEP.TO", "Strategic", type: "CLOSE", shares: 500m),
            Holding(3, "GONE.TO", "Strategic", type: "CLOSE"),
        };
        var s = await SetUpAsync(holdings, [], new(), ["GONE.TO", "KEEP.TO"], null, null, null, OverboughtScan("KEEP.TO"), OverboughtScan("GONE.TO"));
        var snap = await s.Service.ComputeAsync("u");

        var keep = Assert.Single(snap.Portfolio);
        Assert.Equal(1, keep.Ownership!.OpenLots);
        Assert.Equal(100.0, keep.Ownership.ExposurePct!.Value, 1);   // the 500 closed shares are not exposure

        var gone = snap.Watchlist.Single(w => w.Symbol == "GONE.TO");
        Assert.False(gone.Ownership!.Owned);
        Assert.DoesNotContain("ALREADY_OWNED", gone.ReasonCodes!);

        var owned = snap.Watchlist.Single(w => w.Symbol == "KEEP.TO");
        Assert.True(owned.Ownership!.Owned);
        Assert.Contains("ALREADY_OWNED", owned.ReasonCodes!);
    }

    [Fact]
    public async Task SameTicker_PortfolioAndWatchlist_ShareOwnershipSemantics_AndSurviveTheCache()
    {
        var s = await SetUpAsync([Holding(1, "SAME.TO", "Strategic")], [], new(), ["SAME.TO"], null, null, null, OverboughtScan("SAME.TO"));
        var first = await s.Service.ComputeAsync("u");
        var cached = s.Service.TryGetCached("u")!;
        Assert.Same(first, cached);
        var (po, wo) = (first.Portfolio.Single().Ownership!, first.Watchlist.Single().Ownership!);
        Assert.Equal((po.Owned, po.OpenLots, po.MixedRoles, po.ExposurePct), (wo.Owned, wo.OpenLots, wo.MixedRoles, wo.ExposurePct));
        Assert.Equal(po.Accounts, wo.Accounts);
        Assert.Equal(first.Portfolio.Single().AllocationStatus, first.Watchlist.Single().AllocationStatus);
    }

    [Fact]
    public async Task WatchlistEntryForAnOwnedTicker_IsConditionedByTheSameAddRules()
    {
        // Two 50% holdings: with a 40% single-position limit configured for the role, an entry label must not read as an unrestricted buy.
        var s = await SetUpAsync([Holding(1, "AAA.TO", "Strategic"), Holding(2, "BBB.TO", "Strategic")], [], new(), ["AAA.TO"], null, null, null,
            Entry("AAA.TO"), Entry("BBB.TO"));
        s.Db.SinglePositionLimits.Add(new SinglePositionLimit { Role = "Strategic", TargetPct = 40m });
        await s.Db.SaveChangesAsync();
        var w = Assert.Single((await s.Service.ComputeAsync("u")).Watchlist);
        Assert.Equal(DecisionBucket.None, DecisionEligibility.Classify(w));
        Assert.NotEqual("ENTRY CANDIDATE", w.FinalAction);
        Assert.NotEqual("STARTER ENTRY", w.FinalAction);
    }

    private static RsiScanResult Entry(string symbol)
    {
        var s = OverboughtScan(symbol);
        s.Rsi = 50m; s.RsiSignal = 48m; s.MacdHistDelta = 0.3m; s.VolumeRatio = 1.5m;
        return s;
    }
}
