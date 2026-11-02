namespace PortfolioManager.Api.Services;

public enum MarketExchange { Tsx, Nyse }

/// <summary>
/// Rule-based exchange calendar (no network, no stored table): holidays, early closes and "last completed session"
/// for the TSX and the NYSE/NASDAQ. All clock logic is in America/Toronto and takes an injectable UTC instant, so
/// DST changes and year boundaries are deterministic and testable.
/// </summary>
public static class TradingCalendar
{
    public static readonly TimeOnly RegularClose = new(16, 0);
    public static readonly TimeOnly EarlyClose = new(13, 0);

    /// <summary>
    /// End-of-day data is produced by the EOD pipeline after the close, so a session counts as "completed" for
    /// freshness purposes only this long after its close. Before that, the previous session is still the latest evidence.
    /// </summary>
    public static readonly TimeSpan EvidenceGrace = TimeSpan.FromHours(2);

    public enum ExchangeBasis { Metadata, Suffix, Assumed }

    /// <summary>
    /// Exchange for a symbol: the security's own market metadata (<c>AnalysisMarket</c> / Yahoo exchange name) first, then the
    /// ticker suffix, then an explicit NYSE assumption. The basis is returned so an assumed exchange is never silent.
    /// </summary>
    public static (MarketExchange Exchange, ExchangeBasis Basis) Resolve(string? symbol, string? analysisMarket = null)
    {
        var m = (analysisMarket ?? string.Empty).Trim().ToUpperInvariant();
        if (m.Length > 0)
        {
            if (m.Contains("TSX", StringComparison.Ordinal) || m.Contains("TORONTO", StringComparison.Ordinal) || m.Contains("CANADA", StringComparison.Ordinal)
                || m is "CNQ" or "NEO" or "TOR")
                return (MarketExchange.Tsx, ExchangeBasis.Metadata);
            if (m.Contains("NYSE", StringComparison.Ordinal) || m.Contains("NASDAQ", StringComparison.Ordinal) || m.Contains("NYQ", StringComparison.Ordinal)
                || m.Contains("ARCA", StringComparison.Ordinal) || m.Contains("AMEX", StringComparison.Ordinal) || m.StartsWith("NMS", StringComparison.Ordinal)
                || m.StartsWith("NGM", StringComparison.Ordinal) || m.StartsWith("NCM", StringComparison.Ordinal) || m is "US" or "USA")
                return (MarketExchange.Nyse, ExchangeBasis.Metadata);
        }
        var s = (symbol ?? string.Empty).Trim().ToUpperInvariant();
        if (s.EndsWith(".TO", StringComparison.Ordinal) || s.EndsWith(".V", StringComparison.Ordinal) || s.EndsWith(".CN", StringComparison.Ordinal))
            return (MarketExchange.Tsx, ExchangeBasis.Suffix);
        return (MarketExchange.Nyse, ExchangeBasis.Assumed);
    }

    public static MarketExchange ExchangeFor(string? symbol, string? analysisMarket = null) => Resolve(symbol, analysisMarket).Exchange;

    // ── Clock ────────────────────────────────────────────────────────────────

    public static TimeZoneInfo TorontoZone() =>
        MarketHoursGate.GetEasternTimeZone() ?? TimeZoneInfo.Utc;

    public static DateTime TorontoNow(DateTime utcNow) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), TorontoZone());

    public static DateOnly TorontoDate(DateTime utcNow) => DateOnly.FromDateTime(TorontoNow(utcNow));

    // ── Sessions ─────────────────────────────────────────────────────────────

    public static bool IsTradingDay(DateOnly d, MarketExchange ex) =>
        d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !IsHoliday(d, ex);

    public static TimeOnly CloseTime(DateOnly d, MarketExchange ex) => IsEarlyClose(d, ex) ? EarlyClose : RegularClose;

    public static bool IsEarlyClose(DateOnly d, MarketExchange ex)
    {
        if (!IsTradingDay(d, ex)) return false;
        if (d.Month == 12 && d.Day == 24) return true;                       // Christmas Eve, both exchanges
        if (ex == MarketExchange.Nyse)
        {
            if (d.Month == 7 && d.Day == 3) return true;                      // day before Independence Day
            if (d.Month == 11 && d.DayOfWeek == DayOfWeek.Friday && d.Day >= 23 && d.Day <= 29) return true;   // after Thanksgiving
        }
        return false;
    }

    public static DateOnly PreviousTradingDay(DateOnly d, MarketExchange ex)
    {
        var p = d.AddDays(-1);
        while (!IsTradingDay(p, ex)) p = p.AddDays(-1);
        return p;
    }

    public static DateOnly NextTradingDay(DateOnly d, MarketExchange ex)
    {
        var n = d.AddDays(1);
        while (!IsTradingDay(n, ex)) n = n.AddDays(1);
        return n;
    }

    /// <summary>
    /// The latest session whose end-of-day data should exist at <paramref name="utcNow"/>: today's once the close plus
    /// <see cref="EvidenceGrace"/> has passed, otherwise the previous trading day. Never the raw calendar date.
    /// </summary>
    public static DateOnly LastCompletedSession(DateTime utcNow, MarketExchange ex)
    {
        var et = TorontoNow(utcNow);
        var date = DateOnly.FromDateTime(et);
        if (IsTradingDay(date, ex) && TimeOnly.FromDateTime(et) >= CloseTime(date, ex).Add(EvidenceGrace)) return date;
        return PreviousTradingDay(date, ex);
    }

    /// <summary>Trading days after <paramref name="from"/> up to and including <paramref name="to"/>.</summary>
    public static int TradingDaysBetween(DateOnly from, DateOnly to, MarketExchange ex)
    {
        if (to <= from) return 0;
        var n = 0;
        for (var d = from.AddDays(1); d <= to; d = d.AddDays(1))
            if (IsTradingDay(d, ex)) n++;
        return n;
    }

    // ── Holidays ─────────────────────────────────────────────────────────────

    public static bool HasHolidayTable(int year) => year is >= 2000 and <= 2100;

    public static bool IsHoliday(DateOnly d, MarketExchange ex) => Holidays(d.Year, ex).Contains(d);

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(int, MarketExchange), IReadOnlySet<DateOnly>> Cache = new();

    public static IReadOnlySet<DateOnly> Holidays(int year, MarketExchange ex) =>
        Cache.GetOrAdd((year, ex), k => BuildHolidays(k.Item1, k.Item2));

    private static IReadOnlySet<DateOnly> BuildHolidays(int year, MarketExchange ex)
    {
        var set = new HashSet<DateOnly>();
        var easter = EasterSunday(year);
        var goodFriday = easter.AddDays(-2);

        if (ex == MarketExchange.Tsx)
        {
            set.Add(NextMondayIfWeekend(new DateOnly(year, 1, 1)));
            set.Add(NthWeekday(year, 2, DayOfWeek.Monday, 3));                // Family Day
            set.Add(goodFriday);
            set.Add(MondayOnOrBefore(new DateOnly(year, 5, 24)));            // Victoria Day
            set.Add(NextMondayIfWeekend(new DateOnly(year, 7, 1)));          // Canada Day
            set.Add(NthWeekday(year, 8, DayOfWeek.Monday, 1));                // Civic Holiday
            set.Add(NthWeekday(year, 9, DayOfWeek.Monday, 1));                // Labour Day
            set.Add(NthWeekday(year, 10, DayOfWeek.Monday, 2));               // Thanksgiving
            var christmas = Dec25Observed(year);
            set.Add(christmas);
            var boxing = new DateOnly(year, 12, 26);
            while (boxing <= christmas || boxing.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) boxing = boxing.AddDays(1);
            set.Add(boxing);
        }
        else
        {
            var newYear = new DateOnly(year, 1, 1);
            if (newYear.DayOfWeek == DayOfWeek.Sunday) set.Add(newYear.AddDays(1));
            else if (newYear.DayOfWeek != DayOfWeek.Saturday) set.Add(newYear);   // a Saturday New Year is not observed
            set.Add(NthWeekday(year, 1, DayOfWeek.Monday, 3));                // MLK
            set.Add(NthWeekday(year, 2, DayOfWeek.Monday, 3));                // Presidents
            set.Add(goodFriday);
            set.Add(LastWeekday(year, 5, DayOfWeek.Monday));                  // Memorial
            if (year >= 2022) set.Add(ObservedFixed(new DateOnly(year, 6, 19)));   // Juneteenth
            set.Add(ObservedFixed(new DateOnly(year, 7, 4)));
            set.Add(NthWeekday(year, 9, DayOfWeek.Monday, 1));                // Labor Day
            set.Add(NthWeekday(year, 11, DayOfWeek.Thursday, 4));             // Thanksgiving
            set.Add(Dec25ObservedUs(year));
        }
        return set;
    }

    private static DateOnly NextMondayIfWeekend(DateOnly d) => d.DayOfWeek switch
    {
        DayOfWeek.Saturday => d.AddDays(2),
        DayOfWeek.Sunday => d.AddDays(1),
        _ => d,
    };

    private static DateOnly ObservedFixed(DateOnly d) => d.DayOfWeek switch
    {
        DayOfWeek.Saturday => d.AddDays(-1),
        DayOfWeek.Sunday => d.AddDays(1),
        _ => d,
    };

    private static DateOnly Dec25Observed(int year) => NextMondayIfWeekend(new DateOnly(year, 12, 25));
    private static DateOnly Dec25ObservedUs(int year) => ObservedFixed(new DateOnly(year, 12, 25));

    private static DateOnly NthWeekday(int year, int month, DayOfWeek day, int n)
    {
        var d = new DateOnly(year, month, 1);
        while (d.DayOfWeek != day) d = d.AddDays(1);
        return d.AddDays(7 * (n - 1));
    }

    private static DateOnly LastWeekday(int year, int month, DayOfWeek day)
    {
        var d = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        while (d.DayOfWeek != day) d = d.AddDays(-1);
        return d;
    }

    private static DateOnly MondayOnOrBefore(DateOnly d)
    {
        while (d.DayOfWeek != DayOfWeek.Monday) d = d.AddDays(-1);
        return d;
    }

    /// <summary>Anonymous Gregorian algorithm (Meeus/Jones/Butcher).</summary>
    private static DateOnly EasterSunday(int year)
    {
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var month = (h + l - 7 * m + 114) / 31;
        var day = (h + l - 7 * m + 114) % 31 + 1;
        return new DateOnly(year, month, day);
    }
}
