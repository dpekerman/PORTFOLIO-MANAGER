namespace PortfolioManager.Api.Services;

/// <summary>
/// Determines whether "today" (Eastern Time) is a real trading day by checking whether the latest
/// available daily bar for a reference symbol actually dates to today, rather than assuming every
/// weekday is a trading day. This closes a pre-existing gap: PortfolioValueEodBackgroundService only
/// checked DayOfWeek, so on a weekday market holiday it could record a snapshot mislabeled with
/// today's date using stale (last-traded) quotes. RSI/EOD signals were already safe by construction
/// (TradingDate is derived from the actual last completed bar, not from DateTime.Now).
/// </summary>
public interface ITradingSessionGuard
{
    Task<bool> IsTodayATradingDayAsync(CancellationToken ct = default);

    /// <summary>Returns the actual date of the latest completed daily bar (Eastern Time), regardless
    /// of what "today" currently is — safe to call from any timezone/time-of-day.</summary>
    Task<DateOnly?> GetLatestTradingDateAsync(CancellationToken ct = default);
}

public sealed class TradingSessionGuard(
    IMarketDataProvider marketData,
    ILogger<TradingSessionGuard> logger) : ITradingSessionGuard
{
    /// <summary>S&P 500 index — already used elsewhere in this codebase (Dashboard/Scanner/Performance
    /// Summary market indices) as an always-available, already-warm Yahoo symbol.</summary>
    private const string ReferenceSymbol = "^GSPC";

    public async Task<bool> IsTodayATradingDayAsync(CancellationToken ct = default)
    {
        var tz = MarketHoursGate.GetEasternTimeZone();
        if (tz is null)
        {
            logger.LogWarning("[TradingSessionGuard] Eastern time zone unavailable; failing open (treating as a trading day).");
            return true;
        }
        var todayEt = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz));

        var latestBarDate = await GetLatestTradingDateAsync(ct);
        return latestBarDate is null || latestBarDate == todayEt;
    }

    public async Task<DateOnly?> GetLatestTradingDateAsync(CancellationToken ct = default)
    {
        var closes = await marketData.GetDailyClosesAsync(ReferenceSymbol, ct);
        if (closes is null || closes.Count == 0)
        {
            logger.LogWarning("[TradingSessionGuard] No daily closes returned for {Symbol}; cannot determine latest trading date.", ReferenceSymbol);
            return null;
        }
        return closes[^1].Date;
    }
}
