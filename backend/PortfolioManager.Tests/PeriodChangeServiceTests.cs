using PortfolioManager.Api.Services;

namespace PortfolioManager.Tests;

public sealed class PeriodChangeServiceTests
{
    private static List<MarketDailyClose> Series(DateOnly start, int days, Func<int, decimal> price) =>
        Enumerable.Range(0, days)
            .Select(i => new MarketDailyClose(start.AddDays(i), price(i)))
            .ToList();

    [Fact]
    public void Compute_FiveDay_UsesCloseFiveSessionsBack()
    {
        var closes = Series(new DateOnly(2026, 1, 1), 10, i => 100m + i); // last=109, 5 back=104
        var result = PeriodChangeService.Compute("AAA", closes);
        Assert.Equal(Math.Round((109m / 104m - 1m) * 100m, 2), result.Change5DPct);
    }

    [Fact]
    public void Compute_OneMonth_UsesLastCloseOnOrBeforeAnniversary()
    {
        var closes = new List<MarketDailyClose>
        {
            new(new DateOnly(2026, 3, 13), 80m),
            new(new DateOnly(2026, 3, 14), 90m), // Saturday-style gap: no 3/15 close
            new(new DateOnly(2026, 4, 1), 95m),
            new(new DateOnly(2026, 4, 15), 99m),
        };
        var result = PeriodChangeService.Compute("AAA", closes);
        Assert.Equal(10m, result.Change1MPct); // 99 vs 90 (3/14 is last close <= 3/15)
    }

    [Fact]
    public void Compute_InsufficientHistory_ReturnsNulls_NotZero()
    {
        var closes = Series(new DateOnly(2026, 1, 1), 3, i => 100m + i);
        var result = PeriodChangeService.Compute("AAA", closes);
        Assert.Null(result.Change5DPct);
        Assert.Null(result.Change1MPct);
        Assert.Null(PeriodChangeService.Compute("AAA", null).Change5DPct);
    }
}
