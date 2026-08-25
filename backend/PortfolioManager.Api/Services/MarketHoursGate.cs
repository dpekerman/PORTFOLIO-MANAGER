namespace PortfolioManager.Api.Services;

/// <summary>
/// Shared Eastern-time market-hours check used by background services so they skip DB/API
/// work overnight, on weekends, and holidays — this lets a serverless/auto-pause SQL database
/// actually pause instead of being kept alive 24/7 by continuous background pings.
/// </summary>
public static class MarketHoursGate
{
    private static readonly string[] EasternTzIds = ["Eastern Standard Time", "America/New_York"];
    private static TimeZoneInfo? _easternTz;

    public static readonly TimeSpan DefaultStart = new(9, 0, 0);
    public static readonly TimeSpan DefaultEnd = new(16, 30, 0);

    public static TimeZoneInfo? GetEasternTimeZone()
    {
        if (_easternTz is not null) return _easternTz;
        foreach (var id in EasternTzIds)
        {
            try { _easternTz = TimeZoneInfo.FindSystemTimeZoneById(id); return _easternTz; }
            catch { /* try next */ }
        }
        return null;
    }

    public static DateTime? GetEasternNow()
    {
        var tz = GetEasternTimeZone();
        return tz is null ? null : TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
    }

    /// <summary>True Mon-Fri between start and end (inclusive), evaluated in Eastern time.</summary>
    public static bool IsMarketHours(DateTime easternNow, TimeSpan? start = null, TimeSpan? end = null)
    {
        if (easternNow.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return false;
        var s = start ?? DefaultStart;
        var e = end ?? DefaultEnd;
        return easternNow.TimeOfDay >= s && easternNow.TimeOfDay <= e;
    }
}
