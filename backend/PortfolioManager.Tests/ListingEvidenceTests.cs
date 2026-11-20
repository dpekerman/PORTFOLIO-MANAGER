using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using PortfolioManager.Api.Services.FinalAction;
using PortfolioManager.Api.Services.Risk;

namespace PortfolioManager.Tests;

public sealed class ListingEvidenceTests
{
    [Theory]
    [InlineData("NVDA.TO", "NVDA")]
    [InlineData("DIS.TO", "DIS")]
    public void MappedSignal_UsesMatchingListingCloseOrReturnsUnavailable(string listing, string underlying)
    {
        var date = new DateOnly(2026, 11, 12);
        var signal = new SignalAuditEvent
        {
            Symbol = listing, AnalysisSymbol = underlying, Currency = "USD", SignalDate = date,
            SignalPrice = 150m, BasisClose = 151m,
        };
        MarketDailyClose[] history = [new(date, 25m, PriceCurrency: "CAD"), new(date.AddDays(1), 26m, PriceCurrency: "CAD")];
        var (price, basis, currency) = SignalsAuditService.ResolvePerformanceBasis(signal, history);
        Assert.Equal((25m, SignalPriceBasis.ListingClose, "CAD"), (price, basis, currency));
        Assert.Equal(4m, SignalReturnCalculator.Compute(date, price, history).Today.Pct);
        Assert.Equal(150m, signal.SignalPrice);
        foreach (var unavailable in new IReadOnlyList<MarketDailyClose>?[]
            { null, [history[1]], [history[0] with { PriceCurrency = "USD" }, history[1]] })
        {
            var result = SignalsAuditService.ResolvePerformanceBasis(signal, unavailable);
            Assert.Equal(SignalPriceBasis.Unavailable, result.Basis);
            Assert.Equal(SignalReturnCalculator.NotAvailable, SignalReturnCalculator.Compute(date, result.Price, unavailable).Today.Status);
        }
    }

    [Theory]
    [InlineData("NVDA", "USD", false)]
    [InlineData("NVDA.TO", "USD", false)]
    [InlineData("NVDA.TO", "CAD", true)]
    public void TradingQuote_RequiresListingIdentityAndCurrency(string quoteSymbol, string currency, bool valid)
    {
        var quote = new StockQuote { Symbol = quoteSymbol, Currency = currency, CurrentPrice = 25m };
        Assert.Equal(valid, TradingCurrency.IsListingQuote("NVDA.TO", quote));
    }

    [Fact]
    public void SameTechnicalEvidence_ProducesAnAddWhenTheSafeguardIsOffAndAWaitWhenItIsOn()
    {
        var input = new FinalActionInput
        {
            Rsi = 22, RsiSignal = 26, RsiSignalAvailable = true, CurrentPrice = 80, Sma50 = 100,
            DayLow = 74, DayHigh = 82, OpenPrice = 79, PreviousClose = 80, VolumeRatio = 1.4,
        };
        var watch = FinalActionEngine.TranslateForWatchlist(input, "Strategic");
        var held = FinalActionEngine.TranslateForPortfolio(input, "Strategic");
        Assert.Equal("Waterfall / Falling Knife", held.TrendSetup);
        Assert.Equal("Active Buy Trigger", held.MomentumShift);
        Assert.Equal("REVERSAL WATCH", watch.FinalAction);
        Assert.Equal("Add / Staged Entry", held.FinalAction);   // the unchanged role rule

        var evidence = new PortfolioEvidence(null, null, new DateOnly(2026, 11, 12), DateTime.UnixEpoch, 4.3, 5, false, false, null);
        var legacy = PortfolioEvidenceGate.Apply(held, "Strategic", evidence);        // no role or reversal facts: the pre-policy gate
        Assert.Equal("Add / Staged Entry", legacy.Decision.FinalAction);
        Assert.Contains(FinalActionExplainer.AddEligible, FinalActionExplainer.ExplainPortfolio(held, legacy, evidence, "Strategic").Codes);

        var unavailable = PortfolioEvidenceGate.Apply(held, "Strategic", evidence with
        {
            Valuation = new ValuationCheck(ValuationCheck.UnverifiedPrices, "mismatched quote"),
        });
        Assert.Equal("Add / Needs Review", unavailable.Decision.FinalAction);
        Assert.Contains(GateReasons.AllocationUnavailable, unavailable.ReasonCodes);
    }
}
