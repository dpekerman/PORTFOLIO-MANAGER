using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using PortfolioManager.Api.Services.Opportunities;

namespace PortfolioManager.Tests;

public sealed class SetupQualityTests
{
    private static readonly DateOnly Session = new(2026, 11, 13);
    private static readonly DateTime Now = new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);
    private static RsiScanResult Scan(decimal stop = 96m, decimal target = 110m) => new()
    {
        Symbol = "TEST", AnalysisTicker = "TEST", AnalysisMarket = "US", AnalysisCurrency = "USD", TradingDate = Session,
        ScannedAt = Now.AddHours(-1), CurrentPrice = 100m, DailyAtr = 2m,
        DynamicStopLoss = stop, Week52High = target
    };

    private static MarketDailyClose[] History()
    {
        var days = new List<DateOnly> { Session };
        while (days.Count < 12) days.Insert(0, TradingCalendar.PreviousTradingDay(days[0], MarketExchange.Nyse));
        decimal[] lows = [98, 97, 95, 97, 98, 99, 100, 100, 99, 98, 98, 99];
        decimal[] highs = [102, 102, 103, 103, 105, 108, 111, 108, 105, 103, 102, 102];
        return days.Select((day, index) => new MarketDailyClose(day, 100m, 100m, highs[index], lows[index], 1000, true, "USD")).ToArray();
    }

    [Theory]
    [InlineData(96, 110, "Caution", 2.5)] // Stop above confirmed support.
    [InlineData(95, 110, "Good", 2)]
    [InlineData(95, 120, "Caution", 2.2)] // First resistance caps an optimistic target.
    [InlineData(94, 110, "Caution", 1.6666666666666667)]
    [InlineData(92, 103, "Poor", 0.375)]
    public void ApprovedProposalUsesUnbrokenPivotsAndConservativeTarget(int stop, int target, string expected, double ratio)
    {
        var result = SetupQualityCalculator.Evaluate(Scan(stop, target), History(), Session, Now, true);
        Assert.Equal(expected, result.Status);
        Assert.Equal(95m, result.Support);
        Assert.Equal(111m, result.ProposedTarget > 111m ? result.CredibleTarget : 111m);
        Assert.Equal(ratio, (double)result.ConservativeRewardRisk!.Value, 8);
    }

    [Fact]
    public void DefaultIsInactiveAndDoesNotModifyScannerStopsOrMarketR()
    {
        var scan = Scan(95m);
        var before = TradeSetupResolver.Resolve(scan, Session);
        var evidence = SetupQualityCalculator.Evaluate(scan, History(), Session, Now);
        Assert.Equal("Unavailable", evidence.Status);
        Assert.Contains("SetupPolicyApprovalRequired", evidence.ReasonCodes);
        Assert.NotNull(evidence.ConservativeRewardRisk);
        Assert.Equal(before, TradeSetupResolver.Resolve(scan, Session));
        Assert.Equal(95m, scan.DynamicStopLoss);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(99.5)]
    [InlineData(90)]
    public void KnownInvalidStopIsPoorOnlyWhenProposalExplicitlyApproved(double stop)
    {
        var scan = Scan((decimal)stop);
        Assert.Equal("Poor", SetupQualityCalculator.Evaluate(scan, History(), Session, Now, true).Status);
        Assert.Equal("Unavailable", SetupQualityCalculator.Evaluate(scan, History(), Session, Now).Status);
    }

    [Theory]
    [InlineData(100, "Good", 2.2)]       // 5 below / 11 above, 2.5 ATR stop distance
    [InlineData(96.5, "Caution", 14.5 / 1.5)] // support closer than one ATR
    [InlineData(108, "Poor", 3.0 / 13)]  // little room to confirmed resistance
    public void StrategicSetupNeedsNoSignalStopAndGradesConfirmedStructure(double price, string expected, double ratio)
    {
        var scan = Scan(stop: 0m);
        scan.CurrentPrice = (decimal)price;
        Assert.Equal("Unavailable", SetupQualityCalculator.Evaluate(scan, History(), Session, Now, true).Status);

        var result = SetupQualityCalculator.EvaluateStrategic(scan, History(), Session, Now);

        Assert.Equal(expected, result.Status);
        Assert.True(result.PolicyApproved);
        Assert.Equal(SetupQualityCalculator.StrategicPolicyVersion, result.PolicyVersion);
        Assert.Equal(95m, result.ProposedStop);
        Assert.Equal(111m, result.ProposedTarget);
        Assert.Equal(ratio, (double)result.MarketRewardRisk!.Value, 8);
        Assert.DoesNotContain("NoValidStop", result.ReasonCodes);
        Assert.Equal(0m, scan.DynamicStopLoss);
    }

    [Fact]
    public void StrategicSetupIsUnavailableWithoutConfirmedLevelsOrVerifiedStructure()
    {
        var aboveEveryHigh = Scan(stop: 0m);
        aboveEveryHigh.CurrentPrice = 112m;
        var noResistance = SetupQualityCalculator.EvaluateStrategic(aboveEveryHigh, History(), Session, Now);
        Assert.Equal("Unavailable", noResistance.Status);
        Assert.Contains("ConfirmedResistanceUnavailable", noResistance.ReasonCodes);
        Assert.Null(noResistance.MarketRewardRisk);

        var gapped = SetupQualityCalculator.EvaluateStrategic(Scan(stop: 0m),
            History().Skip(1).Where((_, index) => index != 2).ToArray(), Session, Now);
        Assert.Equal("Unavailable", gapped.Status);
        Assert.Contains("StructuralSessionGap", gapped.ReasonCodes);
        Assert.Equal("Unavailable", SetupQualityCalculator.EvaluateStrategic(null, null, Session, Now).Status);
    }

    [Fact]
    public void MissingStaleFutureAndGappedStructuralEvidenceFailClosed()
    {
        Assert.Equal("Unavailable", SetupQualityCalculator.Evaluate(Scan(), null, Session, Now, true).Status);
        var scan = Scan();
        scan.ScannedAt = Now.AddSeconds(1);
        Assert.Equal("Unavailable", SetupQualityCalculator.Evaluate(scan, History(), Session, Now, true).Status);
        Assert.Equal("Unavailable", SetupQualityCalculator.Evaluate(Scan(), History(), Session.AddDays(1), Now, true).Status);
        Assert.Equal("Unavailable", SetupQualityCalculator.Evaluate(Scan(), History().Skip(1).Where((_, index) => index != 2).ToArray(), Session, Now, true).Status);
        Assert.Equal("Unavailable", SetupQualityCalculator.Evaluate(Scan(), History().Append(new(Session.AddDays(1), 100, 100, 101, 99, 1)).ToArray(), Session, Now, true).Status);
        var unknownCurrency = Scan(100m);
        unknownCurrency.AnalysisCurrency = "";
        Assert.Equal("Unavailable", SetupQualityCalculator.Evaluate(unknownCurrency, History(), Session, Now, true).Status);
        var reconstructed = History();
        reconstructed[0] = reconstructed[0] with { OriginalOhlcAvailable = false };
        Assert.Equal("Unavailable", SetupQualityCalculator.Evaluate(Scan(), reconstructed, Session, Now, true).Status);
        var mismatched = History();
        mismatched[0] = mismatched[0] with { PriceCurrency = "CAD" };
        Assert.Equal("Unavailable", SetupQualityCalculator.Evaluate(Scan(), mismatched, Session, Now, true).Status);
    }
}
