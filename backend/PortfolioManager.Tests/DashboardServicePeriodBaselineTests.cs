using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Tests;

/// <summary>
/// Regression tests for DashboardService.ResolvePeriodBaselines — guards against the
/// "This Week == This Month" bug caused by falling back to an in-period snapshot when
/// no snapshot exists strictly before the period start (see docs/dashboard-week-month-
/// identical-values-root-cause-2026-09-04.md).
/// </summary>
public sealed class DashboardServicePeriodBaselineTests
{
    // Week of Mon 2026-08-31 .. Sun 2026-09-06. Month starts Tue 2026-09-01.
    private static readonly DateOnly WeekStart = new(2026, 8, 31);
    private static readonly DateOnly MonthFirstDay = new(2026, 9, 1);

    [Fact]
    public void WeekBaseline_UsesLastRowStrictlyBeforeWeekStart_NotOnOrAfter()
    {
        var values = new List<PortfolioValueHistory>
        {
            Row("2026-08-27", 100m), // Thursday before the week — expected baseline
            Row("2026-08-28", 110m), // Friday before the week — should win (latest before start)
            Row("2026-08-31", 999m), // Monday, ON the week start — must NOT be picked as week baseline
            Row("2026-09-01", 998m), // inside the week — must NOT be picked
        };

        var (weekBase, _) = DashboardService.ResolvePeriodBaselines(values, WeekStart, MonthFirstDay);

        Assert.NotNull(weekBase);
        Assert.Equal("2026-08-28", weekBase!.RecordedDate);
        Assert.Equal(110m, weekBase.TotalValue);
    }

    [Fact]
    public void MonthBaseline_UsesLastRowStrictlyBeforeMonthStart_NotOnOrAfter()
    {
        var values = new List<PortfolioValueHistory>
        {
            Row("2026-08-28", 200m), // Friday, last close in prior month — expected baseline
            Row("2026-08-31", 210m), // Monday, still before Sep 1 — should win over Aug 28
            Row("2026-09-01", 999m), // ON month start — must NOT be picked
            Row("2026-09-02", 998m), // inside the month — must NOT be picked
        };

        var (_, monthBase) = DashboardService.ResolvePeriodBaselines(values, WeekStart, MonthFirstDay);

        Assert.NotNull(monthBase);
        Assert.Equal("2026-08-31", monthBase!.RecordedDate);
        Assert.Equal(210m, monthBase.TotalValue);
    }

    [Fact]
    public void WeekAndMonthBaselines_ResolveToDifferentRows_WhenUnderlyingDataDiffers()
    {
        // Regression guard for the exact bug found in production: week and month must be able
        // to resolve to different snapshots (and therefore different values) when real data exists
        // for both. If this ever collapses to the same row again, the identical-values bug is back.
        var values = new List<PortfolioValueHistory>
        {
            Row("2026-08-28", 809969.3774m), // last close before week start (Aug 31)
            Row("2026-08-31", 760502.4277m), // last close before month start (Sep 1)
        };

        var (weekBase, monthBase) = DashboardService.ResolvePeriodBaselines(values, WeekStart, MonthFirstDay);

        Assert.NotNull(weekBase);
        Assert.NotNull(monthBase);
        Assert.NotEqual(weekBase!.RecordedDate, monthBase!.RecordedDate);
        Assert.NotEqual(weekBase.TotalValue, monthBase.TotalValue);
    }

    [Fact]
    public void Baselines_AreNull_WhenNoSnapshotExistsBeforePeriodStart()
    {
        // e.g. app was only ever run inside the current week/month — no prior history at all.
        var values = new List<PortfolioValueHistory>
        {
            Row("2026-09-02", 500m),
            Row("2026-09-03", 505m),
        };

        var (weekBase, monthBase) = DashboardService.ResolvePeriodBaselines(values, WeekStart, MonthFirstDay);

        Assert.Null(weekBase);
        Assert.Null(monthBase);
    }

    [Fact]
    public void WeekBaseline_SkipsGapAcrossHolidayWeekend_UsesLastRealTradingDayBeforeStart()
    {
        // Simulates a market holiday on the Monday before this week's Monday, plus the
        // weekend — only a real prior Friday close should ever be selected as baseline.
        var values = new List<PortfolioValueHistory>
        {
            Row("2026-08-21", 300m), // Friday, two weeks prior
            Row("2026-08-24", 305m), // Monday, prior week — nothing between here and week start
            Row("2026-08-28", 310m), // Friday immediately before week start — expected baseline
        };

        var (weekBase, _) = DashboardService.ResolvePeriodBaselines(values, WeekStart, MonthFirstDay);

        Assert.NotNull(weekBase);
        Assert.Equal("2026-08-28", weekBase!.RecordedDate);
        Assert.Equal(310m, weekBase.TotalValue);
    }

    private static PortfolioValueHistory Row(string recordedDate, decimal totalValue) => new()
    {
        RecordedAt = DateTime.SpecifyKind(DateTime.Parse(recordedDate).AddHours(20).AddMinutes(30), DateTimeKind.Utc),
        RecordedDate = recordedDate,
        TotalValue = totalValue,
        StocksValue = totalValue,
        CashValue = 0m,
        OptionsValue = 0m,
    };
}
