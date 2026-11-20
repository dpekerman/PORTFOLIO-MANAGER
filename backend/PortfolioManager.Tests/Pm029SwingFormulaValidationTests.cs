using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using PortfolioManager.Api.Services.Opportunities;

namespace PortfolioManager.Tests;

public sealed class Pm029SwingFormulaValidationTests
{
    private static readonly DateOnly ExpectedSession = new(2026, 11, 13);
    private static readonly DateTime NowUtc = new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);

    private sealed record CandidateScore(
        int? Score,
        ScoreStatus Status,
        decimal CompletenessPct,
        decimal Awarded,
        decimal Available);

    [Fact]
    public void AllTechnicalComponentsAvailable_BothFormulasAgree_WithoutAValidSetup()
    {
        var input = Input(Scan(s => s.DynamicStopLoss = 0m));
        var current = OpportunityScoringEngine.ScoreSwing(input);

        Assert.Equal(ScoreStatus.Verified, current.Status);
        Assert.Equal(SetupKind.Unavailable, input.Setup.Kind);
        Assert.DoesNotContain(current.Components, component => component.Code == "S6");

        var optionA = Simulate(current, denominator: "available", minimumCompletenessPct: 70m);
        var optionB = Simulate(current, denominator: "fixed", minimumCompletenessPct: 70m);

        Assert.Equal(ScoreStatus.Verified, optionA.Status);
        Assert.Equal(ScoreStatus.Verified, optionB.Status);
        Assert.Equal(100m, optionA.CompletenessPct);
        Assert.Equal(91, optionA.Score);
        Assert.Equal(optionA.Score, optionB.Score);
    }

    [Fact]
    public void MissingS2_OptionAInflatesScoreComparedWithOptionB()
    {
        var current = OpportunityScoringEngine.ScoreSwing(Input(sector: Sector(null, null)));

        var optionA = Simulate(current, denominator: "available", minimumCompletenessPct: 70m);
        var optionB = Simulate(current, denominator: "fixed", minimumCompletenessPct: 70m);

        Assert.Equal(75m, optionA.CompletenessPct);
        Assert.Equal(ScoreStatus.Provisional, optionA.Status);
        Assert.Equal(100, optionA.Score);
        Assert.Equal(75, optionB.Score);
        Assert.Equal(ScoreBand.Strong, OpportunityScoringEngine.BandFor(optionA.Score));
        Assert.Equal(ScoreBand.Attractive, OpportunityScoringEngine.BandFor(optionB.Score));
        Assert.Null(Simulate(current, denominator: "available", minimumCompletenessPct: 80m).Score);
    }

    [Theory]
    [InlineData(true, 70.0, 90, 79)]
    [InlineData(false, 70.0, 90, 79)]
    public void MissingS4OrS5_IsPartialEvidence_NotAnEssentialFailure(
        bool missingS4,
        double minimumCompletenessPct,
        int expectedOptionA,
        int expectedOptionB)
    {
        var scan = Scan(s =>
        {
            if (missingS4) s.VolumeRatio = 0m;
            else s.DailyAtr = 0m;
        });
        var current = OpportunityScoringEngine.ScoreSwing(Input(scan));

        var optionA = Simulate(current, denominator: "available", (decimal)minimumCompletenessPct);
        var optionB = Simulate(current, denominator: "fixed", (decimal)minimumCompletenessPct);

        Assert.Equal(87.5m, optionA.CompletenessPct);
        Assert.Equal(ScoreStatus.Provisional, optionA.Status);
        Assert.Equal(expectedOptionA, optionA.Score);
        Assert.Equal(expectedOptionB, optionB.Score);
    }

    [Fact]
    public void MissingS1OrS3_EssentialEvidencePreventsTechnicalScore()
    {
        var current = OpportunityScoringEngine.ScoreSwing(Input(Scan(s => s.Ema20Price = 0m)));
        var s1Missing = Simulate(current, denominator: "available", minimumCompletenessPct: 70m);
        Assert.Null(s1Missing.Score);
        Assert.Equal(ScoreStatus.Insufficient, s1Missing.Status);

        var complete = OpportunityScoringEngine.ScoreSwing(Input());
        var components = complete.Components
            .Select(component => component.Code == "S3"
                ? component with { State = ComponentState.Missing, Awarded = 0m, AvailableMax = 0m }
                : component)
            .ToArray();
        var s3Missing = Simulate(complete with { Components = components },
            denominator: "available", minimumCompletenessPct: 70m);

        Assert.Null(s3Missing.Score);
        Assert.Equal(ScoreStatus.Insufficient, s3Missing.Status);
    }

    [Theory]
    [InlineData(55.99, false)]
    [InlineData(56.00, true)]
    [InlineData(56.01, true)]
    public void CompletenessGate_IsInclusiveAtSeventyPercent(double availablePoints, bool expectedScorable)
    {
        var available = (decimal)availablePoints;
        var score = OpportunityScoringEngine.ScoreSwing(Input()) with
        {
            Components =
            [
                Component("S1", 25m, 25m, essential: true),
                Component("S2", 20m, available - 50m),
                Component("S3", 15m, 15m, essential: true),
                Component("S4", 10m, 10m),
                Component("S5", 10m, 0m),
            ],
        };

        var result = Simulate(score, denominator: "available", minimumCompletenessPct: 70m);

        Assert.Equal(expectedScorable, result.Score is not null);
        Assert.Equal(available / 80m * 100m, result.CompletenessPct);
    }

    [Fact]
    public void ValidSetupDoesNotRaiseAWeakTechnicalScore()
    {
        var scan = Scan(s =>
        {
            s.CurrentPrice = 100m;
            s.Ema20Price = 105m;
            s.Sma50Price = 110m;
            s.Sma200 = 115m;
            s.Rsi = 35m;
            s.RsiSignal = 40m;
            s.MacdHistogram = -0.5m;
            s.MacdHistDelta = -0.1m;
            s.ChangePercent = -1m;
            s.VolumeRatio = 2m;
            s.ChaseRisk = "Elevated";
            s.FibZone = "Trend Damage";
            s.DynamicStopLoss = 96m;
            s.PriceStructure = PriceStructureResult.None with { PrimaryPatternState = "BREAKDOWN" };
        });
        var current = OpportunityScoringEngine.ScoreSwing(Input(scan, Sector(-3m, -3m)));

        Assert.Equal(SetupKind.ValidCandidate, TradeSetupResolver.Resolve(scan, ExpectedSession).Kind);
        Assert.Equal(0m, current.Components.Single(component => component.Code == "S1").Awarded);
        Assert.Equal(1, Simulate(current, denominator: "available", minimumCompletenessPct: 70m).Score);
    }

    [Fact]
    public void ChangingSetupAssumptionsDoesNotChangeTechnicalCandidateScore()
    {
        var technicalInput = Input();
        var alternativeSetup = new TradeSetupResult(
            SetupKind.ValidCandidate, 100m, 99m, 120m, "Synthetic scenario", 20m, 1m, 20m, 2.5m, "CAD", []);
        var original = OpportunityScoringEngine.ScoreSwing(technicalInput);
        var alternative = OpportunityScoringEngine.ScoreSwing(technicalInput with { Setup = alternativeSetup });

        Assert.Equal(original.Score, alternative.Score);
        Assert.Equal(original.Status, alternative.Status);
        Assert.Equal(original.CompletenessPct, alternative.CompletenessPct);
        Assert.Equal(
            original.Components.Select(component => (component.Code, component.Awarded, component.AvailableMax)),
            alternative.Components.Select(component => (component.Code, component.Awarded, component.AvailableMax)));
    }

    [Fact]
    public void ZeroPriceOutOfRangeRsiAndStaleEvidenceFailClosed()
    {
        var zeroPrice = OpportunityScoringEngine.ScoreSwing(Input(Scan(s => s.CurrentPrice = 0m)));
        Assert.Null(zeroPrice.Score);
        Assert.Contains(OpportunityReasons.InvalidAnalysisPrice, zeroPrice.ReasonCodes);

        var invalidRsi = OpportunityScoringEngine.ScoreSwing(Input(Scan(s =>
        {
            s.Rsi = 101m;
        })));
        Assert.Null(invalidRsi.Score);
        Assert.Equal(ScoreStatus.Insufficient, invalidRsi.Status);
        Assert.Contains(OpportunityReasons.InvalidRsi,
            invalidRsi.Components.Single(component => component.Code == "S3").MissingReasons);

        var stale = OpportunityScoringEngine.ScoreSwing(Input(Scan(s =>
        {
            s.ScannedAt = NowUtc.AddYears(-10);
        })));
        Assert.Null(stale.Score);
        Assert.Contains(OpportunityReasons.ScanBeforeSessionClose, stale.ReasonCodes);
    }

    [Fact]
    public void ScanTimestampMustBeAtOrAfterTheActualSessionCloseAndNotInTheFuture()
    {
        var close = TradingCalendar.SessionCloseUtc(ExpectedSession, MarketExchange.Tsx);
        var beforeClose = OpportunityScoringEngine.ScoreSwing(Input(Scan(s => s.ScannedAt = close.AddTicks(-1))));
        Assert.Contains(OpportunityReasons.ScanBeforeSessionClose, beforeClose.ReasonCodes);

        var atClose = OpportunityScoringEngine.ScoreSwing(Input(Scan(s => s.ScannedAt = close)));
        Assert.Equal(ScoreStatus.Verified, atClose.Status);

        var future = OpportunityScoringEngine.ScoreSwing(Input(Scan(s => s.ScannedAt = NowUtc.AddTicks(1))));
        Assert.Contains(OpportunityReasons.ScanTimestampInFuture, future.ReasonCodes);

        var missing = OpportunityScoringEngine.ScoreSwing(Input(Scan(s => s.ScannedAt = default)));
        Assert.Contains(OpportunityReasons.InvalidScanTimestamp, missing.ReasonCodes);
    }

    [Fact]
    public void EarlyCloseIsExchangeAwareAndFridayEvidenceRemainsCurrentOnSaturday()
    {
        var friday = new DateOnly(2026, 11, 27);
        var saturday = new DateTime(2026, 11, 28, 17, 0, 0, DateTimeKind.Utc);
        var earlyClose = TradingCalendar.SessionCloseUtc(friday, MarketExchange.Nyse);
        var beforeCloseScan = Scan(s =>
        {
            s.AnalysisTicker = "SYNTH";
            s.AnalysisMarket = "NYSE";
            s.TradingDate = friday;
            s.ScannedAt = earlyClose.AddTicks(-1);
        });
        var beforeClose = OpportunityScoringEngine.ScoreSwing(new OpportunityScoringInput(
            beforeCloseScan, friday, Sector(2m, 1m), TradeSetupResolver.Resolve(beforeCloseScan, friday), null, saturday));
        Assert.Contains(OpportunityReasons.ScanBeforeSessionClose, beforeClose.ReasonCodes);

        beforeCloseScan.ScannedAt = earlyClose;
        var afterClose = OpportunityScoringEngine.ScoreSwing(new OpportunityScoringInput(
            beforeCloseScan, friday, Sector(2m, 1m), TradeSetupResolver.Resolve(beforeCloseScan, friday), null, saturday));
        Assert.Equal(ScoreStatus.Verified, afterClose.Status);
    }

    [Fact]
    public void InvalidTechnicalDomainsRemainMissingEvidenceRatherThanObservedWeakness()
    {
        var invalidRsi = OpportunityScoringEngine.ScoreSwing(Input(Scan(s => s.Rsi = -1m)));
        Assert.Contains(OpportunityReasons.InvalidRsi,
            invalidRsi.Components.Single(component => component.Code == "S3").MissingReasons);
        Assert.Equal(ScoreStatus.Insufficient, invalidRsi.Status);

        var invalidAverage = OpportunityScoringEngine.ScoreSwing(Input(Scan(s => s.Ema20Price = -1m)));
        Assert.Contains(OpportunityReasons.InvalidMovingAverage,
            invalidAverage.Components.Single(component => component.Code == "S1").MissingReasons);
        Assert.Equal(ScoreStatus.Insufficient, invalidAverage.Status);

        var zeroVolume = OpportunityScoringEngine.ScoreSwing(Input(Scan(s => s.Volume = 0)));
        Assert.Equal(ComponentState.Missing, zeroVolume.Components.Single(component => component.Code == "S4").State);
        Assert.Contains("VolumeUnavailable", zeroVolume.Components.Single(component => component.Code == "S4").MissingReasons);

        var invalidRatio = OpportunityScoringEngine.ScoreSwing(Input(Scan(s => s.VolumeRatio = -0.1m)));
        Assert.Contains(OpportunityReasons.InvalidVolumeRatio,
            invalidRatio.Components.Single(component => component.Code == "S4").MissingReasons);

        var invalidAtr = OpportunityScoringEngine.ScoreSwing(Input(Scan(s => s.DailyAtr = -0.1m)));
        Assert.Contains(OpportunityReasons.InvalidAtr,
            invalidAtr.Components.Single(component => component.Code == "S5").MissingReasons);
    }

    private static OpportunityScoringInput Input(RsiScanResult? scan = null, SectorRelativeResult? sector = null)
    {
        scan ??= Scan();
        return new(scan, ExpectedSession, sector ?? Sector(2.3m, 0.4m),
            TradeSetupResolver.Resolve(scan, ExpectedSession), null, NowUtc);
    }

    private static RsiScanResult Scan(Action<RsiScanResult>? tweak = null)
    {
        var scan = new RsiScanResult
        {
            Symbol = "SYNTH.TO",
            AnalysisCurrency = "CAD",
            AnalysisMarket = "TSX",
            TradingDate = ExpectedSession,
            ScannedAt = NowUtc,
            CurrentPrice = 100m,
            Ema20Price = 98m,
            Sma50Price = 95m,
            Sma200 = 90m,
            Has200Dma = true,
            Rsi = 58m,
            RsiSignal = 55m,
            RsiSignalAvailable = true,
            MacdHistogram = 0.5m,
            MacdHistDelta = 0.1m,
            ChangePercent = 1.2m,
            Volume = 1_000_000,
            VolumeRatio = 1.6m,
            VolumeSignal = "Validated",
            DailyAtr = 2m,
            DynamicStopLoss = 96m,
            ChannelDirection = "ASCENDING",
            UpperRailToday = 108m,
            Week52High = 110m,
            ChaseRisk = "",
            FibZone = "Normal Pullback",
            PriceStructure = PriceStructureResult.None,
        };
        tweak?.Invoke(scan);
        return scan;
    }

    private static SectorRelativeResult Sector(decimal? d20, decimal? d60)
    {
        var d20Window = Window(20, d20);
        var d60Window = Window(60, d60);
        var status = d20 is null && d60 is null
            ? SectorHeatStatus.Unmapped
            : SectorHeatStatus.Matched;
        return new(status, "SYNTH.BENCH", "Synthetic benchmark", ExpectedSession,
            d20Window, d60Window, OpportunityScoringConfig.SectorMethodology);
    }

    private static SectorWindowResult Window(int sessions, decimal? diff)
    {
        var available = diff.HasValue;
        return new(sessions,
            available ? SectorHeatStatus.Matched : SectorHeatStatus.Unmapped,
            available ? ExpectedSession.AddDays(-sessions) : null,
            available ? ExpectedSession : null,
            available ? 1m : null,
            available ? 1m : null,
            diff,
            diff,
            available ? SectorHeatDirection.Positive : SectorHeatDirection.None,
            available ? 1 : 0,
            available ? sessions + 1 : 0,
            available ? sessions + 1 : 0,
            sessions + 1);
    }

    private static ScoreComponent Component(string code, decimal max, decimal availableMax, bool essential = false) =>
        new(code, code, max, essential,
            availableMax == 0m ? ComponentState.Missing : availableMax == max ? ComponentState.Observed : ComponentState.Partial,
            availableMax, availableMax, [], [], null);

    private static CandidateScore Simulate(ScoreResult score, string denominator, decimal minimumCompletenessPct)
    {
        if (score.Status == ScoreStatus.Unavailable) return new(null, ScoreStatus.Unavailable, 0m, 0m, 0m);

        var technical = score.Components.Where(component => component.Code is "S1" or "S2" or "S3" or "S4" or "S5").ToArray();
        if (technical.Length != 5) return new(null, ScoreStatus.Unavailable, 0m, 0m, 0m);

        var awarded = technical.Sum(component => component.Awarded);
        var available = technical.Sum(component => component.AvailableMax);
        var completeness = available / 80m * 100m;
        var essentialsPresent = technical.Where(component => component.Essential)
            .All(component => component.State != ComponentState.Missing);
        if (!essentialsPresent || available == 0m || completeness < minimumCompletenessPct)
            return new(null, ScoreStatus.Insufficient, completeness, awarded, available);

        var denominatorMax = denominator == "available" ? available : 80m;
        var result = (int)Math.Round(awarded / denominatorMax * 100m, 0, MidpointRounding.AwayFromZero);
        var status = completeness >= 100m ? ScoreStatus.Verified : ScoreStatus.Provisional;
        return new(result, status, completeness, awarded, available);
    }
}
