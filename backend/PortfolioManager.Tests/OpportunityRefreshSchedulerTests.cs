using PortfolioManager.Api.Services.Opportunities;

namespace PortfolioManager.Tests;

public sealed class OpportunityRefreshSchedulerTests
{
    // 2026-11-16 is a Monday; November is EST (UTC-5).
    private static DateTime Et(string date, string time) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.Parse($"{date} {time}"), TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"));

    private static OpportunityScheduleConfig Config(bool enabled = true, string time = "18:05", params DayOfWeek[] days)
    {
        var cfg = new OpportunityScheduleConfig();
        cfg.Update(enabled, time, days.Length == 0
            ? [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday]
            : days);
        return cfg;
    }

    [Fact]
    public void Default_schedule_time_is_605_pm_eastern()
        => Assert.Equal("18:05", new OpportunityScheduleConfig().TimeEt);

    [Fact]
    public void Due_after_scheduled_time_on_a_configured_market_day()
        => Assert.True(OpportunityRefreshSchedulerService.IsDue(Config(), Et("2026-11-16", "18:06")));

    [Fact]
    public void Not_due_before_scheduled_time_or_when_disabled()
    {
        Assert.False(OpportunityRefreshSchedulerService.IsDue(Config(), Et("2026-11-16", "18:04")));
        Assert.False(OpportunityRefreshSchedulerService.IsDue(Config(enabled: false), Et("2026-11-16", "18:30")));
    }

    [Fact]
    public void Waits_until_the_session_counts_as_completed_even_if_time_is_earlier()
        => Assert.False(OpportunityRefreshSchedulerService.IsDue(Config(time: "16:30"), Et("2026-11-16", "16:45")));

    [Fact]
    public void Skips_unselected_days_weekends_and_holidays()
    {
        Assert.False(OpportunityRefreshSchedulerService.IsDue(Config(days: DayOfWeek.Tuesday), Et("2026-11-16", "19:00")));
        Assert.False(OpportunityRefreshSchedulerService.IsDue(Config(), Et("2026-11-14", "19:00")));
        Assert.False(OpportunityRefreshSchedulerService.IsDue(Config(), Et("2026-12-25", "19:00")));
    }

    [Fact]
    public void Runs_once_per_day()
    {
        var cfg = Config();
        cfg.RecordRun("2026-11-16", "Succeeded", null);
        Assert.False(OpportunityRefreshSchedulerService.IsDue(cfg, Et("2026-11-16", "19:00")));
        Assert.True(OpportunityRefreshSchedulerService.IsDue(cfg, Et("2026-11-17", "18:10")));
    }
}
