using System.Text.Json;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Tests;

/// <summary>PM-034: forward dividend semantics validated against live Yahoo /v7/finance/quote responses.</summary>
public sealed class ForwardDividendCalculatorTests
{
    private static readonly DateTime Now = new(2026, 11, 3, 18, 0, 0, DateTimeKind.Utc);
    private static long Unix(DateTime utc) => new DateTimeOffset(utc).ToUnixTimeSeconds();

    [Fact]
    public void ValidRate_AndMatchingProviderPercentYield_ProduceForwardDividendAndYield()
    {
        // ENB.TO sample: rate 3.88 CAD, price 68.69, provider yield 5.64 (a percentage on /v7).
        var r = ForwardDividendCalculator.Evaluate(3.88m, 5.64m, 68.69m, Unix(Now.AddDays(60)), Now, "CAD");

        Assert.Equal(DividendStatuses.Positive, r.Status);
        Assert.Equal(3.88m, r.PerShare);
        Assert.InRange(r.YieldPct!.Value, 5.648m, 5.649m);
        Assert.NotNull(r.DividendDateUtc);
    }

    [Fact]
    public void ProviderYieldInFractionScale_IsNotSilentlyAccepted()
    {
        // quoteSummary reports 0.0564; comparing it unscaled to 5.65% must be treated as unreconciled, never as a 0.06% yield.
        var r = ForwardDividendCalculator.Evaluate(3.88m, 0.0564m, 68.69m, Unix(Now.AddDays(60)), Now, "CAD");

        Assert.Equal(DividendStatuses.Inconsistent, r.Status);
        Assert.Null(r.YieldPct);
        Assert.Null(r.PerShare);
    }

    [Fact]
    public void SmallDifferenceFromDifferentQuoteTimes_StaysWithinTolerance()
    {
        // AAPL sample: rate 1.08, price 331.82 -> 0.3255%; provider reported 0.32.
        var r = ForwardDividendCalculator.Evaluate(1.08m, 0.32m, 331.82m, Unix(Now.AddDays(90)), Now, "USD");
        Assert.Equal(DividendStatuses.Positive, r.Status);
        Assert.Equal(0.3255m, r.YieldPct);
    }

    [Fact]
    public void LargeDisagreement_IsUnavailableNotCorrected()
    {
        var r = ForwardDividendCalculator.Evaluate(3.26m, 9.5m, 57.5m, Unix(Now.AddDays(30)), Now, "USD");
        Assert.Equal(DividendStatuses.Inconsistent, r.Status);
        Assert.Null(r.YieldPct);
    }

    [Fact]
    public void MissingProviderRate_IsMissing_NotZero()
    {
        // ETFs and funds (VTI, XIU.TO) and non-payers with no rate field.
        var etf = ForwardDividendCalculator.Evaluate(null, 1.06m, 371.54m, null, Now, "USD");
        Assert.Equal(DividendStatuses.Missing, etf.Status);
        Assert.Null(etf.PerShare);
        Assert.Null(etf.YieldPct);
    }

    [Fact]
    public void ExplicitZeroRate_IsConfirmedZero()
    {
        var r = ForwardDividendCalculator.Evaluate(0m, 0m, 100m, null, Now, "USD");
        Assert.Equal(DividendStatuses.Zero, r.Status);
        Assert.Equal(0m, r.PerShare);
        Assert.Equal(0m, r.YieldPct);
    }

    [Fact]
    public void NegativeRate_IsTreatedAsMissing()
    {
        Assert.Equal(DividendStatuses.Missing, ForwardDividendCalculator.Evaluate(-1m, null, 50m, null, Now).Status);
    }

    [Fact]
    public void OldDividendDate_IsStale_AndShowsNoValues()
    {
        var r = ForwardDividendCalculator.Evaluate(1.2m, 2.4m, 50m, Unix(Now.AddDays(-500)), Now, "CAD");
        Assert.Equal(DividendStatuses.Stale, r.Status);
        Assert.Null(r.PerShare);
        Assert.Null(r.YieldPct);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void InvalidOrZeroPrice_KeepsTheRateButHasNoYield(int price)
    {
        var r = ForwardDividendCalculator.Evaluate(1.5m, 3m, price, null, Now, "USD");
        Assert.Equal(DividendStatuses.Positive, r.Status);
        Assert.Equal(1.5m, r.PerShare);
        Assert.Null(r.YieldPct);
    }

    [Fact]
    public void DividendCurrencyDifferentFromQuoteCurrency_IsUnavailable_WithoutAnyConversion()
    {
        var r = ForwardDividendCalculator.Evaluate(2m, null, 40m, null, Now, quoteCurrency: "CAD", dividendCurrency: "USD");
        Assert.Equal(DividendStatuses.Inconsistent, r.Status);
        Assert.Null(r.PerShare);
        Assert.Null(r.YieldPct);
    }

    [Fact]
    public void NoProviderYield_StillUsesRatePerPrice()
    {
        var r = ForwardDividendCalculator.Evaluate(1.16m, null, 21.15m, Unix(Now.AddDays(10)), Now, "CAD");
        Assert.Equal(DividendStatuses.Positive, r.Status);
        Assert.Equal(5.4846m, r.YieldPct);
    }

    [Fact]
    public void YahooQuoteItem_ReadsRateAndPercentYield_AndLeavesAbsentFieldsNull()
    {
        const string json = """
        {"symbol":"ENB.TO","regularMarketPrice":68.69,"currency":"CAD","dividendRate":3.88,"dividendYield":5.64,"dividendDate":1788220800,"trailingAnnualDividendYield":0.055652425}
        """;
        var item = JsonSerializer.Deserialize<YahooQuoteItem>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        Assert.Equal(3.88m, item.DividendRate);
        Assert.Equal(5.64m, item.DividendYieldPct);
        Assert.Equal(1788220800L, item.DividendDate);
        // The trailing yield is a fraction and remains a separate field used by the Value Screener.
        Assert.Equal(0.055652425m, item.TrailingAnnualDividendYield);

        var etf = JsonSerializer.Deserialize<YahooQuoteItem>("""{"symbol":"VTI","regularMarketPrice":371.54,"dividendYield":1.06}""",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        Assert.Null(etf.DividendRate);
        Assert.Null(etf.DividendDate);
    }
}
