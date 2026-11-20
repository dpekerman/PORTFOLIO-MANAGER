using System.Text.Json;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using PortfolioManager.Api.Services.Opportunities;

namespace PortfolioManager.Tests;

/// <summary>PM-029 rule-branch tests for Swing-v2 and the unchanged Strategic-v1 calculators.</summary>
public class Pm029CalculatorTests
{
    private static readonly DateOnly E = new(2026, 11, 13);
    private static readonly DateTime Now = new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);

    // ── Fixtures (values are test inputs, not prototype figures) ─────────────

    private static RsiScanResult Scan(Action<RsiScanResult>? tweak = null)
    {
        var s = new RsiScanResult
        {
            Symbol = "TST.TO", AnalysisCurrency = "CAD", AnalysisMarket = "TSX", TradingDate = E, ScannedAt = Now,
            CurrentPrice = 100m, Ema20Price = 98m, Sma50Price = 95m, Sma200 = 90m, Has200Dma = true, Dma200Deviation = 11.1m,
            Rsi = 58m, RsiSignal = 55m, RsiSignalAvailable = true, MacdHistogram = 0.5m, MacdHistDelta = 0.1m,
            ChangePercent = 1.2m, Volume = 1_000_000, VolumeRatio = 1.6m, VolumeSignal = "Validated",
            DailyAtr = 2m, ChaseRisk = "", FibZone = "Normal Pullback",
            DynamicStopLoss = 96m, ChannelDirection = "ASCENDING", UpperRailToday = 108m, Week52High = 110m,
            MomentumState = "Positive", PriceStructure = PriceStructureResult.None,
        };
        tweak?.Invoke(s);
        return s;
    }

    private static SectorWindowResult Win(int sessions, decimal? display)
    {
        if (display is null)
            return new(sessions, SectorHeatStatus.InsufficientObservations, null, null, null, null, null, null, SectorHeatDirection.None, 0, 0, 0, sessions + 1);
        var (dir, intensity) = SectorRelativeCalculator.Classify(display.Value);
        return new(sessions, SectorHeatStatus.Matched, E.AddDays(-30), E, 1m, 1m, display, display, dir, intensity, sessions + 1, sessions + 1, sessions + 1);
    }

    private static SectorRelativeResult Sector(decimal? d20, decimal? d60) =>
        new(d20 is null && d60 is null ? SectorHeatStatus.InsufficientObservations : SectorHeatStatus.Matched, "XFN.TO", "Fin", E,
            Win(20, d20), Win(60, d60), OpportunityScoringConfig.SectorMethodology);

    private static ValueScreenerResult Value(Action<ValueScreenerResult>? tweak = null)
    {
        var v = new ValueScreenerResult
        {
            Symbol = "TST.TO", Sector = "Financial Services", AnalyzedAt = Now.AddDays(-1),
            ScoreEarningsYield = 3m, ScoreFcfYield = 1m, ScorePriceToBook = 1m, ScorePiotroski = 2m, ScoreRoic = 1m,
            F1Available = true, F2Available = true, F3Available = true, F4Available = true, F5Available = true,
            CapitalEfficiencyBasis = "ROIC", PiotroskiSignalsAvailable = 9, MarketCapFallbackUsed = false, FundamentalsDataAvailable = true,
        };
        tweak?.Invoke(v);
        return v;
    }

    private static OpportunityScoringInput Input(RsiScanResult? scan = null, SectorRelativeResult? sector = null, ValueScreenerResult? value = null)
    {
        scan ??= Scan();
        return new OpportunityScoringInput(scan, E, sector ?? Sector(2.3m, 0.4m), TradeSetupResolver.Resolve(scan, E), value ?? Value(), Now);
    }

    // ── Swing ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("ETF")]
    [InlineData("MUTUALFUND")]
    [InlineData("CLOSEDENDFUND")]
    public void ConfirmedFundsAreNotEvaluatedInEitherPerspective(string instrument)
    {
        var input = Input() with { InstrumentType = instrument };
        foreach (var score in new[] { OpportunityScoringEngine.ScoreSwing(input), OpportunityScoringEngine.ScoreStrategic(input) })
        {
            Assert.Equal(ScoreStatus.NotEvaluated, score.Status);
            Assert.Null(score.Score);
            Assert.Empty(score.Components);
            Assert.Equal([OpportunityReasons.EtfScoringDeferred], score.ReasonCodes);
        }
    }

    [Fact]
    public void UsProxyBenchmark_IsConvertedToCad_OnTheStocksOwnSessions()
    {
        var monday = new DateOnly(2026, 11, 9);
        List<MarketDailyClose> Series(params (int Day, decimal Close)[] points) =>
            points.Select(p => new MarketDailyClose(monday.AddDays(p.Day), p.Close)).ToList();
        var stock = Series((0, 100m), (1, 100m), (2, 100m), (3, 100m), (4, 100m));
        var etf = Series((0, 50m), (1, 50m), (3, 55m));
        var fx = Series((0, 1.4m), (1, 1.4m), (2, 1.4m), (3, 1.5m));

        var cad = SectorRelativeCalculator.ConvertUsBenchmarkToCad(etf, fx, stock)!;

        Assert.Equal([70m, 70m, 70m, 82.5m, 82.5m], cad.Select(b => b.Close));
        Assert.Equal(stock.Select(b => b.Date), cad.Select(b => b.Date));
    }

    [Fact]
    public void UsProxyBenchmark_OmitsSessionsWithoutRecentCloses_AndNeedsAllInputs()
    {
        var monday = new DateOnly(2026, 11, 9);
        var stock = new[] { new MarketDailyClose(monday, 10m), new MarketDailyClose(monday.AddDays(7), 10m) };
        var etf = new[] { new MarketDailyClose(monday, 50m) };
        var fx = new[] { new MarketDailyClose(monday, 1.4m), new MarketDailyClose(monday.AddDays(7), 1.4m) };

        var cad = SectorRelativeCalculator.ConvertUsBenchmarkToCad(etf, fx, stock)!;

        Assert.Equal([monday], cad.Select(b => b.Date));
        Assert.Null(SectorRelativeCalculator.ConvertUsBenchmarkToCad(etf, null, stock));
        Assert.Null(SectorRelativeCalculator.ConvertUsBenchmarkToCad(null, fx, stock));
    }

    [Theory]
    [InlineData(true, "EQUITY", "ETF", "EQUITY")]
    [InlineData(true, null, "ETF", null)]
    [InlineData(false, "EQUITY", "ETF", "ETF")]
    [InlineData(false, "EQUITY", null, "EQUITY")]
    public void InstrumentType_UsesTheUnderlyingOnlyWhenTheUnderlyingIsAnalysed(
        bool usesUnderlying, string? scanType, string? listingType, string? expected)
    {
        var scan = Scan(s => { s.UsesUnderlyingSecurity = usesUnderlying; s.InstrumentType = scanType; });
        Assert.Equal(expected, OpportunityService.ResolveInstrumentType(scan, listingType));
    }

    [Fact]
    public void UnknownInstrumentAndAnEtfUnderlyingDoNotMisclassifyTheListing()
    {
        var input = Input(Scan(scan => { scan.InstrumentType = "ETF"; scan.UsesUnderlyingSecurity = true; }));
        Assert.Equal(ScoreStatus.Verified, OpportunityScoringEngine.ScoreSwing(input).Status);
        Assert.Equal(ScoreStatus.Verified, OpportunityScoringEngine.ScoreStrategic(input).Status);
        Assert.Equal(ScoreStatus.Unavailable, OpportunityScoringEngine.ScoreSwing(input with
            { IdentityResolved = false, InstrumentType = "ETF" }).Status);
    }

    [Fact]
    public void NpiQuoteMarketCapRestoresObservedF2WithoutInventingF1()
    {
        var quote = new StockQuote { Symbol = "NPI.TO", CurrentPrice = 20m, MarketCap = 1_000_000_000L, PriceToBook = 1m };
        var snapshot = new FundamentalsSnapshot { FreeCashFlow = 100_000_000L, PriceToBook = 1m, DataAvailable = true };
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<ValueScreenerService>.Instance;
        var value = ValueScreenerService.BuildResult("NPI.TO", ValueOrigin.Watchlist, quote, snapshot, Scan(), logger);
        value.AnalyzedAt = Now.AddHours(-1);
        Assert.False(value.MarketCapFallbackUsed);
        Assert.True(value.F2Available);
        Assert.False(value.F1Available);
        var strategic = OpportunityScoringEngine.ScoreStrategic(Input(value: value));
        Assert.NotNull(strategic.Score);
        Assert.DoesNotContain(OpportunityReasons.FundamentalsInsufficientFactors, strategic.ReasonCodes);

        quote.MarketCap = 0L;
        var estimated = ValueScreenerService.BuildResult("NPI.TO", ValueOrigin.Watchlist, quote, snapshot, Scan(), logger);
        estimated.AnalyzedAt = Now.AddHours(-1);
        Assert.True(estimated.MarketCapFallbackUsed);
        Assert.Null(OpportunityScoringEngine.ScoreStrategic(Input(value: estimated)).Score);
    }

    [Fact]
    public void Swing_AllEvidence_IsVerifiedAndReconciles()
    {
        var r = OpportunityScoringEngine.ScoreSwing(Input());
        Assert.Equal(ScoreStatus.Verified, r.Status);
        Assert.Equal(100m, r.CompletenessPct);
        // S1 25 + S2 (11+2) + S3 15 + S4 10 + S5 10 = 73; 73 / 80 × 100 = 91.25 → 91.
        Assert.Equal(new[] { 25m, 13m, 15m, 10m, 10m }, r.Components.Select(c => c.Awarded));
        Assert.Equal(80m, r.ScoreDenominatorMax);
        Assert.Equal(91, r.Score);
        Assert.Equal(91, r.NormalizedScore);
        Assert.Equal(ScoreBand.Strong, r.Band);
        Assert.Equal("pm029-v2", r.ScoringVersion);
    }

    [Fact]
    public void Swing_MissingSubItem_IsProvisionalWithReducedCompleteness()
    {
        var r = OpportunityScoringEngine.ScoreSwing(Input(Scan(s => s.RsiSignalAvailable = false)));
        Assert.Equal(ScoreStatus.Provisional, r.Status);
        Assert.Equal(96.25m, r.CompletenessPct);
        Assert.Equal(88, r.Score); // 70 / 80 = 87.5 → 88
        Assert.Equal(ComponentState.Partial, r.Components.Single(c => c.Code == "S3").State);
    }

    [Fact]
    public void Swing_NoValidStop_DoesNotAffectTechnicalScore_AndStrategicStillScores()
    {
        var input = Input(Scan(s => s.DynamicStopLoss = 0m));
        var swing = OpportunityScoringEngine.ScoreSwing(input);
        Assert.Equal(ScoreStatus.Verified, swing.Status);
        Assert.Equal(91, swing.Score);
        Assert.DoesNotContain(swing.Components, c => c.Code == "S6");
        Assert.DoesNotContain(swing.ReasonCodes, c => c == "MissingEssential:S6");
        Assert.Equal(SetupKind.Unavailable, input.Setup.Kind);
        Assert.Contains(OpportunityReasons.NoValidStop, input.Setup.ReasonCodes);

        var strategic = OpportunityScoringEngine.ScoreStrategic(input);
        Assert.NotNull(strategic.Score);
        Assert.Equal("pm029-v1", strategic.ScoringVersion);
    }

    [Fact]
    public void Swing_LowCompleteness_IsInsufficient()
    {
        var scan = Scan(s => { s.VolumeRatio = 0m; s.DailyAtr = 0m; });
        var r = OpportunityScoringEngine.ScoreSwing(new OpportunityScoringInput(
            scan, E, Sector(null, null), TradeSetupResolver.Resolve(scan, E), null, Now));
        Assert.Equal(50m, r.CompletenessPct);
        Assert.Equal(ScoreStatus.Insufficient, r.Status);
        Assert.Contains(OpportunityReasons.LowCompleteness, r.ReasonCodes);
    }

    [Fact]
    public void Swing_HardStructuralNegative_ZeroesTrend()
    {
        var r = OpportunityScoringEngine.ScoreSwing(Input(Scan(s => s.PriceStructure = PriceStructureResult.None with { PrimaryPatternState = "BREAKDOWN" })));
        var s1 = r.Components.Single(c => c.Code == "S1");
        Assert.Equal(0m, s1.Awarded);
        Assert.Equal(25m, s1.AvailableMax);
    }

    [Theory]
    [InlineData(5.0, 14)] [InlineData(4.9, 11)] [InlineData(2.0, 11)] [InlineData(0.5, 8)] [InlineData(0.4, 5)]
    [InlineData(-0.4, 5)] [InlineData(-0.5, 2)] [InlineData(-2.0, 2)] [InlineData(-2.1, 0)]
    public void Swing_Rs20Tiers(double diff, int expected) =>
        Assert.Equal((decimal)expected, OpportunityScoringEngine.Tier((decimal)diff, 14m, 11m, 8m, 5m, 2m));

    [Theory]
    [InlineData(57, 6)] [InlineData(47, 4)] [InlineData(68, 4)] [InlineData(42, 2)] [InlineData(75, 1)] [InlineData(35, 0)]
    public void Swing_RsiZones(int rsi, int expected)
    {
        var r = OpportunityScoringEngine.ScoreSwing(Input(Scan(s => s.Rsi = rsi)));
        Assert.Equal((decimal)expected, r.Components.Single(c => c.Code == "S3").Items.Single(i => i.Code == "RsiZone").Points);
    }

    [Theory]
    [InlineData(1.0, 1.6, 8)] [InlineData(1.0, 0.8, 4)] [InlineData(-1.0, 1.6, 0)] [InlineData(-1.0, 1.2, 2)] [InlineData(-1.0, 0.5, 4)] [InlineData(0.0, 2.0, 4)]
    public void Swing_VolumeDirection(double change, double ratio, int expected)
    {
        var r = OpportunityScoringEngine.ScoreSwing(Input(Scan(s => { s.ChangePercent = (decimal)change; s.VolumeRatio = (decimal)ratio; })));
        Assert.Equal((decimal)expected, r.Components.Single(c => c.Code == "S4").Items.Single(i => i.Code == "VolumeDirection").Points);
    }

    [Fact]
    public void Score_StaleOrMissingScan_IsUnavailable()
    {
        var stale = OpportunityScoringEngine.ScoreSwing(Input(Scan(s => s.TradingDate = E.AddDays(-1))));
        Assert.Equal(ScoreStatus.Unavailable, stale.Status);
        Assert.Equal([OpportunityReasons.StaleScan], stale.ReasonCodes);

        var none = OpportunityScoringEngine.ScoreStrategic(new OpportunityScoringInput(null, E, null, TradeSetupResolver.Resolve(null, E), null, Now));
        Assert.Equal([OpportunityReasons.NoScan], none.ReasonCodes);

        var unresolved = OpportunityScoringEngine.ScoreSwing(Input() with { IdentityResolved = false });
        Assert.Equal([OpportunityReasons.IdentityUnresolved], unresolved.ReasonCodes);
    }

    [Fact]
    public void Score_IsDeterministic()
    {
        var a = JsonSerializer.Serialize(new[] { OpportunityScoringEngine.ScoreSwing(Input()), OpportunityScoringEngine.ScoreStrategic(Input()) });
        var b = JsonSerializer.Serialize(new[] { OpportunityScoringEngine.ScoreSwing(Input()), OpportunityScoringEngine.ScoreStrategic(Input()) });
        Assert.Equal(a, b);
    }

    [Fact]
    public void LegacySwingV1Json_DeserializesWithoutTheAdditiveDenominatorField()
    {
        const string legacy = """
            {"perspective":"Swing","scoringVersion":"pm029-v1","score":null,"normalizedScore":null,
             "awardedTotal":0,"availableMax":0,"completenessPct":0,"status":"Insufficient","band":"None",
             "components":[],"reasonCodes":[]}
            """;
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());

        var result = JsonSerializer.Deserialize<ScoreResult>(legacy, options);

        Assert.NotNull(result);
        Assert.Null(result.ScoreDenominatorMax);
        Assert.Equal(OpportunityScoringConfig.LegacyDisclaimer,
            OpportunityScoringConfig.DisclaimerFor(result.ScoringVersion));
    }

    [Theory]
    [InlineData(null, ScoreBand.None)] [InlineData(85, ScoreBand.Strong)] [InlineData(84, ScoreBand.Attractive)] [InlineData(70, ScoreBand.Attractive)]
    [InlineData(69, ScoreBand.Moderate)] [InlineData(60, ScoreBand.Moderate)] [InlineData(59, ScoreBand.Caution)] [InlineData(50, ScoreBand.Caution)] [InlineData(49, ScoreBand.Weak)]
    public void Bands(int? score, ScoreBand band) => Assert.Equal(band, OpportunityScoringEngine.BandFor(score));

    // ── Strategic ────────────────────────────────────────────────────────────

    [Fact]
    public void Strategic_AllEvidence_IsVerifiedAndReconciles()
    {
        var r = OpportunityScoringEngine.ScoreStrategic(Input());
        Assert.Equal(ScoreStatus.Verified, r.Status);
        // T1 20 + T2 4 + T3 5 + T4 5 + T5 10 + T6 8 + T7 5/6×25 + T8 3/4×15 = 84.083 → 84
        Assert.Equal(84, r.Score);
        Assert.Equal(ScoreBand.Attractive, r.Band);
        Assert.Equal(r.AwardedTotal, r.Components.Sum(c => c.Awarded));
    }

    [Fact]
    public void Strategic_F4Label_DisclosesProxyIndicatorsWithoutChangingTheScore()
    {
        var r = OpportunityScoringEngine.ScoreStrategic(Input());
        var t8 = r.Components.Single(c => c.Code == "T8");
        var f4 = t8.Items.Single(i => i.Code == "F4");

        Assert.Equal(84, r.Score);
        Assert.Contains("proxy-based", f4.Label, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Piotroski", f4.Label, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2m, f4.Points);
    }

    [Theory]
    [InlineData(8, OpportunityReasons.FundamentalsStale)]
    public void Strategic_StaleFundamentals_FailClosed(int ageDays, string reason)
    {
        var r = OpportunityScoringEngine.ScoreStrategic(Input(value: Value(v => v.AnalyzedAt = Now.AddDays(-ageDays))));
        Assert.Equal(ScoreStatus.Insufficient, r.Status);
        Assert.Contains("MissingEssential:T7", r.ReasonCodes);
        Assert.Contains(reason, r.ReasonCodes);
    }

    [Fact]
    public void Strategic_LegacyOrMissingFundamentals_FailClosed()
    {
        var legacy = OpportunityScoringEngine.ScoreStrategic(Input(value: Value(v => { v.F1Available = null; v.F4Available = null; })));
        Assert.Contains(OpportunityReasons.FundamentalsLegacy, legacy.ReasonCodes);
        var missing = OpportunityScoringEngine.ScoreStrategic(new OpportunityScoringInput(Scan(), E, Sector(1m, 1m), TradeSetupResolver.Resolve(Scan(), E), null, Now));
        Assert.Contains(OpportunityReasons.FundamentalsMissing, missing.ReasonCodes);
        Assert.Equal(ScoreStatus.Insufficient, missing.Status);
        // Swing never depends on fundamentals.
        Assert.Equal(ScoreStatus.Verified, OpportunityScoringEngine.ScoreSwing(Input(value: Value(v => v.F1Available = null))).Status);
    }

    [Fact]
    public void Strategic_MarketCapFallback_DropsFcfFactorAndRescales()
    {
        var r = OpportunityScoringEngine.ScoreStrategic(Input(value: Value(v => v.MarketCapFallbackUsed = true)));
        var t7 = r.Components.Single(c => c.Code == "T7");
        Assert.Equal(ComponentState.Partial, t7.State);
        Assert.False(t7.Items.Single(i => i.Code == "F2").Available);
        Assert.Equal(25m, t7.Awarded); // (3 + 1) / (3 + 1) × 25
    }

    [Fact]
    public void Strategic_FewerThanTwoValuationFactors_IsMissing()
    {
        var r = OpportunityScoringEngine.ScoreStrategic(Input(value: Value(v => { v.F2Available = false; v.F3Available = false; })));
        Assert.Equal(ComponentState.Missing, r.Components.Single(c => c.Code == "T7").State);
        Assert.Equal(ScoreStatus.Insufficient, r.Status);
    }

    [Fact]
    public void Strategic_ProxyCapitalEfficiency_NotAccepted()
    {
        var r = OpportunityScoringEngine.ScoreStrategic(Input(value: Value(v => v.CapitalEfficiencyBasis = "Proxy")));
        var t8 = r.Components.Single(c => c.Code == "T8");
        Assert.False(t8.Items.Single(i => i.Code == "F5").Available);
        Assert.Equal(10m, t8.Awarded); // 2/3 × 15
    }

    [Fact]
    public void Strategic_NoSma200_MissingEssential()
    {
        var r = OpportunityScoringEngine.ScoreStrategic(Input(Scan(s => s.Has200Dma = false)));
        Assert.Contains("MissingEssential:T1", r.ReasonCodes);
        Assert.Equal(ScoreStatus.Insufficient, r.Status);
    }

    // ── Trade setup ──────────────────────────────────────────────────────────

    [Fact]
    public void Setup_ValidCandidate_UsesNearestOverheadLevel()
    {
        var r = TradeSetupResolver.Resolve(Scan(), E);
        Assert.Equal(SetupKind.ValidCandidate, r.Kind);
        Assert.Equal(108m, r.Target);
        Assert.Equal("Upper channel rail", r.TargetSource);
        Assert.Equal(2m, r.RewardRisk);
        Assert.Equal(4m, r.RiskPct);
        Assert.Equal("CAD", r.Currency);
    }

    [Fact]
    public void Setup_LowRewardRisk_IsNotActionableButScorable()
    {
        var r = TradeSetupResolver.Resolve(Scan(s => { s.UpperRailToday = 104m; s.Week52High = 0m; }), E);
        Assert.Equal(SetupKind.NotActionable, r.Kind);
        Assert.True(r.IsScorable);
        Assert.Equal(1m, r.RewardRisk);
    }

    [Theory]
    [InlineData(0, OpportunityReasons.NoValidStop, OpportunityReasons.DynamicStopMissing)]
    [InlineData(100, OpportunityReasons.NoValidStop, OpportunityReasons.StopAtOrAboveEntry)]
    [InlineData(101, OpportunityReasons.NoValidStop, OpportunityReasons.StopAtOrAboveEntry)]
    [InlineData(99.5, OpportunityReasons.StopOutsideVolatilityBand, null)]
    [InlineData(91.5, OpportunityReasons.StopOutsideVolatilityBand, null)]
    public void Setup_InvalidStop_IsUnavailableAndNeverFabricated(double stop, string reason, string? detailReason)
    {
        var r = TradeSetupResolver.Resolve(Scan(s => s.DynamicStopLoss = (decimal)stop), E);
        Assert.Equal(SetupKind.Unavailable, r.Kind);
        Assert.Equal(detailReason is null ? [reason] : [reason, detailReason], r.ReasonCodes);
        Assert.Null(r.Stop);
        Assert.Null(r.Target);
        Assert.Null(r.RewardRisk);
    }

    [Theory]
    [InlineData(85, SetupKind.NotActionable)]
    [InlineData(84.9, SetupKind.Unavailable)]
    public void Setup_RiskPercentBoundary_IsInclusiveAtFifteenPercent(double stop, SetupKind expected)
    {
        var r = TradeSetupResolver.Resolve(Scan(s =>
        {
            s.DynamicStopLoss = (decimal)stop;
            s.DailyAtr = 4m;
        }), E);

        Assert.Equal(expected, r.Kind);
        if (stop == 85)
            Assert.Equal(15m, r.RiskPct);
        if (expected == SetupKind.Unavailable)
            Assert.Equal([OpportunityReasons.StopOutsideVolatilityBand], r.ReasonCodes);
    }

    [Fact]
    public void Setup_NoTargetAboveFloor_IsUnavailable()
    {
        // Floor = 100 + 0.5 × 2 = 101: a level at 101 is not "above" it; channel NONE ignores the rail.
        var r = TradeSetupResolver.Resolve(Scan(s => { s.ChannelDirection = "NONE"; s.Week52High = 101m; s.NearestOpenGapAbove = 100.5m; }), E);
        Assert.Equal([OpportunityReasons.NoStructuralTarget], r.ReasonCodes);
    }

    [Fact]
    public void Setup_StaleScanOrZeroAtr_IsUnavailable()
    {
        Assert.Equal([OpportunityReasons.StaleScan], TradeSetupResolver.Resolve(Scan(s => s.TradingDate = E.AddDays(-1)), E).ReasonCodes);
        Assert.Equal([OpportunityReasons.StopOutsideVolatilityBand], TradeSetupResolver.Resolve(Scan(s => s.DailyAtr = 0m), E).ReasonCodes);
    }

    [Fact]
    public void Risk_WorseOfRiskAndAtr_HardNegativeIsHigh()
    {
        var scan = Scan();
        Assert.Equal(RiskCategory.Low, TradeSetupResolver.AssessRisk(scan, TradeSetupResolver.Resolve(scan, E), E).Category);
        var volatile_ = Scan(s => s.DailyAtr = 6m); // risk-to-stop 4 % is Low, ATR 6 % is High ⇒ worse = High
        Assert.Equal(RiskCategory.High, TradeSetupResolver.AssessRisk(volatile_, TradeSetupResolver.Resolve(volatile_, E), E).Category);
        var hard = Scan(s => s.PriceStructure = PriceStructureResult.None with { KeyLevelState = "SUPPORT_BROKEN" });
        Assert.Equal(RiskCategory.High, TradeSetupResolver.AssessRisk(hard, TradeSetupResolver.Resolve(hard, E), E).Category);
        Assert.Equal(RiskCategory.Unavailable, TradeSetupResolver.AssessRisk(null, TradeSetupResolver.Resolve(null, E), E).Category);
    }

    // ── Sector Heat ──────────────────────────────────────────────────────────

    private static List<MarketDailyClose> Bars(DateOnly end, MarketExchange ex, int sessions, Func<int, decimal> closeAtAge, params DateOnly[] skip)
    {
        var list = new List<MarketDailyClose>();
        var d = end;
        for (var age = 0; age <= sessions; age++)
        {
            if (!skip.Contains(d)) list.Add(new MarketDailyClose(d, closeAtAge(age)));
            d = TradingCalendar.PreviousTradingDay(d, ex);
        }
        list.Reverse();
        return list;
    }

    private static readonly SectorBenchmarkInfo Xfn = new("XFN.TO", "iShares Financials", "CAD");

    [Fact]
    public void SectorHeat_ComputesExactReturnsOverCompletedSessions()
    {
        // Stock: 100 at age 20 → 110 at E (+10 %); 90 at age 60. Benchmark: 200 → 210 (+5 %); 200 at age 60.
        var stock = Bars(E, MarketExchange.Tsx, 80, a => a == 0 ? 110m : a == 20 ? 100m : a == 60 ? 90m : 100m);
        var bench = Bars(E, MarketExchange.Tsx, 80, a => a == 0 ? 210m : 200m);
        var r = SectorRelativeCalculator.Compute(stock, bench, E, MarketExchange.Tsx, "CAD", Xfn);
        Assert.Equal(SectorHeatStatus.Matched, r.Status);
        Assert.Equal(10m, r.D20.StockReturnPct);
        Assert.Equal(5m, r.D20.BenchmarkReturnPct);
        Assert.Equal(5.0m, r.D20.DisplayDiffPp);
        Assert.Equal((SectorHeatDirection.Positive, 3), (r.D20.Direction, r.D20.Intensity));
        Assert.Equal(SectorRelativeCalculator.WindowStart(E, 20, MarketExchange.Tsx), r.D20.StartSession);
        Assert.Equal(21, r.D20.ExpectedObservations);
        Assert.Equal(Math.Round((110m / 90m - 1m) * 100m - 5m, 1, MidpointRounding.AwayFromZero), r.D60.DisplayDiffPp);
    }

    [Theory]
    [InlineData(0.449, 0.4, SectorHeatDirection.Neutral, 0)]
    [InlineData(0.45, 0.5, SectorHeatDirection.Positive, 1)]
    [InlineData(-0.45, -0.5, SectorHeatDirection.Negative, 1)]
    [InlineData(-0.44, -0.4, SectorHeatDirection.Neutral, 0)]
    [InlineData(1.99, 2.0, SectorHeatDirection.Positive, 2)]
    [InlineData(-4.96, -5.0, SectorHeatDirection.Negative, 3)]
    public void SectorHeat_NeutralBandOnDisplayedValue(double raw, double display, SectorHeatDirection dir, int intensity)
    {
        var shown = SectorRelativeCalculator.Display((decimal)raw);
        Assert.Equal((decimal)display, shown);
        Assert.Equal((dir, intensity), SectorRelativeCalculator.Classify(shown));
    }

    [Fact]
    public void SectorHeat_StaleWhenLastBarIsNotExpectedSession_AndTrimsBarsAfterE()
    {
        var prior = TradingCalendar.PreviousTradingDay(E, MarketExchange.Tsx);
        var stale = SectorRelativeCalculator.Compute(Bars(prior, MarketExchange.Tsx, 80, _ => 100m), Bars(E, MarketExchange.Tsx, 80, _ => 100m),
            E, MarketExchange.Tsx, "CAD", Xfn);
        Assert.Equal(SectorHeatStatus.Stale, stale.Status);

        var stock = Bars(E, MarketExchange.Tsx, 80, a => a == 0 ? 110m : 100m);
        var withIntraday = stock.Append(new MarketDailyClose(E.AddDays(3), 999m)).ToList();
        var bench = Bars(E, MarketExchange.Tsx, 80, _ => 100m);
        Assert.Equal(
            SectorRelativeCalculator.Compute(stock, bench, E, MarketExchange.Tsx, "CAD", Xfn).D20.DiffPp,
            SectorRelativeCalculator.Compute(withIntraday, bench, E, MarketExchange.Tsx, "CAD", Xfn).D20.DiffPp);
    }

    [Fact]
    public void SectorHeat_ObservationCoverage()
    {
        var bench = Bars(E, MarketExchange.Tsx, 80, _ => 100m);
        DateOnly Age(int n) { var d = E; for (var i = 0; i < n; i++) d = TradingCalendar.PreviousTradingDay(d, MarketExchange.Tsx); return d; }
        var oneMissing = Bars(E, MarketExchange.Tsx, 80, _ => 100m, Age(5));
        Assert.Equal(SectorHeatStatus.Matched, SectorRelativeCalculator.Compute(oneMissing, bench, E, MarketExchange.Tsx, "CAD", Xfn).D20.Status);
        var twoMissing = Bars(E, MarketExchange.Tsx, 80, _ => 100m, Age(5), Age(6));
        Assert.Equal(SectorHeatStatus.InsufficientObservations, SectorRelativeCalculator.Compute(twoMissing, bench, E, MarketExchange.Tsx, "CAD", Xfn).D20.Status);
        var noStart = Bars(E, MarketExchange.Tsx, 80, _ => 100m, Age(20));
        Assert.Equal(SectorHeatStatus.InsufficientObservations, SectorRelativeCalculator.Compute(noStart, bench, E, MarketExchange.Tsx, "CAD", Xfn).D20.Status);
        var shortHistory = Bars(E, MarketExchange.Tsx, 30, _ => 100m);
        var r = SectorRelativeCalculator.Compute(shortHistory, bench, E, MarketExchange.Tsx, "CAD", Xfn);
        Assert.Equal(SectorHeatStatus.Matched, r.D20.Status);
        Assert.Equal(SectorHeatStatus.InsufficientObservations, r.D60.Status);
    }

    [Fact]
    public void SectorHeat_CorporateActionCurrencyAndMapping()
    {
        var bench = Bars(E, MarketExchange.Tsx, 80, _ => 100m);
        var split = Bars(E, MarketExchange.Tsx, 80, a => a < 10 ? 50m : 100m);
        Assert.Equal(SectorHeatStatus.PossibleCorporateAction, SectorRelativeCalculator.Compute(split, bench, E, MarketExchange.Tsx, "CAD", Xfn).D20.Status);
        Assert.Equal(SectorHeatStatus.CurrencyMismatch, SectorRelativeCalculator.Compute(bench, bench, E, MarketExchange.Tsx, "USD", Xfn).Status);
        Assert.Equal(SectorHeatStatus.Unmapped, SectorRelativeCalculator.Compute(bench, bench, E, MarketExchange.Tsx, "CAD", null).Status);
        Assert.Equal(SectorHeatStatus.Unclassified, SectorRelativeCalculator.Compute(bench, bench, E, MarketExchange.Tsx, "CAD", Xfn, sectorClassified: false).Status);
    }

    [Fact]
    public void SectorHeat_UsesEachExchangeHolidayCalendar()
    {
        // 2026: TSX closed Jul 1 (Canada Day); NYSE closed Jun 19 (Juneteenth) and Jul 3 (Independence Day observed).
        var end = new DateOnly(2026, 7, 10);
        Assert.Equal(new DateOnly(2026, 6, 11), SectorRelativeCalculator.WindowStart(end, 20, MarketExchange.Tsx));
        Assert.Equal(new DateOnly(2026, 6, 10), SectorRelativeCalculator.WindowStart(end, 20, MarketExchange.Nyse));
    }

    // ── Portfolio Fit ────────────────────────────────────────────────────────

    private static FitInput Fit(bool owned = true, double? size = 4, double? limit = 10, double? sector = 20, double? target = 20,
        bool reliable = true, bool ambiguous = false, bool sectorKnown = true) =>
        new(owned, reliable, size, limit, ambiguous, sectorKnown, sector, target);

    [Fact]
    public void Fit_Branches()
    {
        Assert.Equal(FitStatus.Eligible, PortfolioFitCalculator.Evaluate(Fit()).Status);
        Assert.Equal(FitStatus.Unavailable, PortfolioFitCalculator.Evaluate(Fit(reliable: false)).Status);
        Assert.Equal(FitStatus.Unavailable, PortfolioFitCalculator.Evaluate(Fit(size: null)).Status);
        Assert.Equal(FitStatus.NoAdd, PortfolioFitCalculator.Evaluate(Fit(size: 10)).Status);
        Assert.Equal(FitStatus.Review, PortfolioFitCalculator.Evaluate(Fit(size: 9.5)).Status);
        Assert.Equal(FitStatus.Review, PortfolioFitCalculator.Evaluate(Fit(limit: null)).Status);
        Assert.Equal(FitStatus.Review, PortfolioFitCalculator.Evaluate(Fit(ambiguous: true, limit: null)).Status);
        Assert.Equal(FitStatus.Review, PortfolioFitCalculator.Evaluate(Fit(sector: 21.5)).Status);
        Assert.Equal(FitStatus.Eligible, PortfolioFitCalculator.Evaluate(Fit(sector: 20)).Status);
        Assert.Equal(FitStatus.Review, PortfolioFitCalculator.Evaluate(Fit(sector: 22)).Status);   // exactly +2 pp is within tolerance
        Assert.Equal(FitStatus.NoAdd, PortfolioFitCalculator.Evaluate(Fit(sector: 22.1)).Status);
        Assert.Equal(FitStatus.Review, PortfolioFitCalculator.Evaluate(Fit(sectorKnown: false)).Status);
        var sectorUnavailable = PortfolioFitCalculator.Evaluate(Fit(sector: null));
        Assert.Equal(FitStatus.Review, sectorUnavailable.Status);
        Assert.Contains(FitReasons.SectorExposureUnavailable, sectorUnavailable.ReasonCodes);
        var noTarget = PortfolioFitCalculator.Evaluate(Fit(target: null));
        Assert.Equal(FitStatus.Eligible, noTarget.Status);
        Assert.Contains(FitReasons.SectorNoTarget, noTarget.ReasonCodes);
        var notOwned = PortfolioFitCalculator.Evaluate(Fit(owned: false, size: null));
        Assert.Equal((FitStatus.Eligible, 0d, 10d), (notOwned.Status, notOwned.WeightPct!.Value, notOwned.PositionHeadroomPct!.Value));
    }

    [Fact]
    public void Fit_ExternalDeposit_RecalculatesWeightAndHeadroom_ButNeverAScore()
    {
        // Same holding value; a deposit raises the GrandTotal denominator, so exposure falls below the limit (C1).
        const double holding = 11_000, before = 100_000, after = 140_000;
        var pre = PortfolioFitCalculator.Evaluate(Fit(size: holding / before * 100));
        var post = PortfolioFitCalculator.Evaluate(Fit(size: holding / after * 100));
        Assert.Equal(FitStatus.NoAdd, pre.Status);
        Assert.Equal(FitStatus.Eligible, post.Status);
        Assert.True(post.PositionHeadroomPct > pre.PositionHeadroomPct);

        // Scoring input has no portfolio field at all: the same market evidence scores identically before/after.
        Assert.Equal(JsonSerializer.Serialize(OpportunityScoringEngine.ScoreSwing(Input())), JsonSerializer.Serialize(OpportunityScoringEngine.ScoreSwing(Input())));
    }

    // ── Sector classification ────────────────────────────────────────────────

    [Fact]
    public void Classification_PrecedenceAndBenchmarkAliases()
    {
        var lots = new[]
        {
            new PortfolioItem { Symbol = "A", Sector = "Technology" },
            new PortfolioItem { Symbol = "A", Sector = "Industrials", SectorIsOverridden = true },
        };
        Assert.Equal(("Industrials", SectorClassificationResolver.SourceOverride),
            (SectorClassificationResolver.Resolve(lots, "Energy", null).Sector, SectorClassificationResolver.Resolve(lots, "Energy", null).Source));
        Assert.Equal(SectorClassificationResolver.SourcePortfolio, SectorClassificationResolver.Resolve([lots[0]], "Energy", null).Source);
        Assert.Equal(SectorClassificationResolver.SourceProvider, SectorClassificationResolver.Resolve(Array.Empty<PortfolioItem>(), "Energy", null).Source);
        Assert.False(SectorClassificationResolver.Resolve(Array.Empty<PortfolioItem>(), "ETFs & Funds", null).IsClassified);

        var maps = new[]
        {
            new SectorBenchmarkMapping { Id = 1, Sector = "Information Technology", Market = "Tsx", BenchmarkSymbol = "XIT.TO", Currency = "CAD", IsActive = true, ProviderSectorAliasesJson = "[\"Technology\"]" },
            new SectorBenchmarkMapping { Id = 2, Sector = "Information Technology", Market = "Nyse", BenchmarkSymbol = "XLK", Currency = "USD", IsActive = true, ProviderSectorAliasesJson = "[\"Technology\"]" },
            new SectorBenchmarkMapping { Id = 3, Sector = "Energy", Market = "Tsx", BenchmarkSymbol = "OLD.TO", Currency = "CAD", IsActive = false },
        };
        Assert.Equal("XIT.TO", SectorClassificationResolver.FindBenchmark(maps, "Technology", MarketExchange.Tsx)?.BenchmarkSymbol);
        Assert.Equal("XLK", SectorClassificationResolver.FindBenchmark(maps, "technology", MarketExchange.Nyse)?.BenchmarkSymbol);
        Assert.Null(SectorClassificationResolver.FindBenchmark(maps, "Energy", MarketExchange.Tsx));
    }
}
