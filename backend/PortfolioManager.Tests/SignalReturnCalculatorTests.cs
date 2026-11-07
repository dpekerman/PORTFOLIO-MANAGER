using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Tests;

public sealed class SignalReturnCalculatorTests
{
    // Mon 2026-03-02 .. business days only (no holidays) — 30 sessions.
    private static List<MarketDailyClose> Sessions(DateOnly start, int count, Func<int, decimal> price, params DateOnly[] holidays)
    {
        var list = new List<MarketDailyClose>();
        var d = start;
        while (list.Count < count)
        {
            if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !holidays.Contains(d))
                list.Add(new MarketDailyClose(d, price(list.Count)));
            d = d.AddDays(1);
        }
        return list;
    }

    private static readonly DateOnly SignalDate = new(2026, 3, 6); // Friday

    [Fact]
    public void Compute_5D_And_20D_UseSessionsStrictlyAfterSignalDate()
    {
        // index 0 = signal day close (100). Session n after signal has price 100 + n.
        var closes = Sessions(SignalDate, 30, i => 100m + i);
        var r = SignalReturnCalculator.Compute(SignalDate, 100m, closes);

        Assert.Equal(SignalReturnCalculator.Complete, r.Return5D.Status);
        Assert.Equal(5m, r.Return5D.Pct);
        Assert.Equal("2026-03-13", r.Return5D.EvalDate);
        Assert.Equal(20m, r.Return20D.Pct);
        Assert.Equal("2026-04-03", r.Return20D.EvalDate);
    }

    [Fact]
    public void Compute_ExchangeHolidayIsNotCountedAsASession()
    {
        var holiday = new DateOnly(2026, 3, 11);
        var closes = Sessions(SignalDate, 30, i => 100m + i, holiday);
        var r = SignalReturnCalculator.Compute(SignalDate, 100m, closes);

        Assert.Equal("2026-03-16", r.Return5D.EvalDate); // 9,10,12,13,16
    }

    [Fact]
    public void Compute_OneMonth_UsesFirstSessionOnOrAfterAnniversary()
    {
        // Signal Fri 2026-01-30 -> anniversary 2026-02-28 (Saturday) -> first session is Mon 2026-03-02.
        var signal = new DateOnly(2026, 1, 30);
        var closes = Sessions(signal, 40, i => 200m + i);
        var r = SignalReturnCalculator.Compute(signal, 200m, closes);

        Assert.Equal("2026-03-02", r.Return1M.EvalDate);
        Assert.Equal(SignalReturnCalculator.Complete, r.Return1M.Status);
    }

    [Fact]
    public void Compute_IncompleteHorizons_ArePendingNeverZero()
    {
        var closes = Sessions(SignalDate, 4, i => 100m + i); // only 3 sessions after the signal
        var r = SignalReturnCalculator.Compute(SignalDate, 100m, closes);

        Assert.Equal(SignalReturnCalculator.Pending, r.Return5D.Status);
        Assert.Null(r.Return5D.Pct);
        Assert.Equal(SignalReturnCalculator.Pending, r.Return20D.Status);
        Assert.Equal(SignalReturnCalculator.Pending, r.Return1M.Status);
        Assert.Equal(SignalReturnCalculator.Complete, r.Today.Status);
        Assert.Equal(3m, r.Today.Pct);
        Assert.Equal("2026-03-11", r.Today.EvalDate);
    }

    [Fact]
    public void Compute_MissingOrUncoveredPrices_AreNotAvailable()
    {
        Assert.Equal(SignalReturnCalculator.NotAvailable, SignalReturnCalculator.Compute(SignalDate, 100m, null).Today.Status);
        Assert.Equal(SignalReturnCalculator.NotAvailable, SignalReturnCalculator.Compute(SignalDate, 0m, Sessions(SignalDate, 5, _ => 1m)).Return5D.Status);

        var laterHistory = Sessions(SignalDate.AddYears(1), 30, _ => 100m);
        Assert.Equal(SignalReturnCalculator.NotAvailable, SignalReturnCalculator.Compute(SignalDate, 100m, laterHistory).Return20D.Status);
    }

    [Fact]
    public void Compute_ReturnSignIsRawNotDirectionAdjusted()
    {
        var closes = Sessions(SignalDate, 30, i => 100m - i);
        var r = SignalReturnCalculator.Compute(SignalDate, 100m, closes);
        Assert.Equal(-5m, r.Return5D.Pct);
    }
}
