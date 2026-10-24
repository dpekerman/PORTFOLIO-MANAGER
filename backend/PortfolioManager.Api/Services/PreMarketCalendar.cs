namespace PortfolioManager.Api.Services;

public enum MarketExchange { Tsx, Us }

/// <summary>Regular-session holiday calendars for the TSX/TSXV and NYSE/Nasdaq, computed from rules (no data feed).</summary>
public static class TradingCalendar
{
    public static MarketExchange ExchangeOf(string symbol)
    {
        var s = symbol.ToUpperInvariant();
        return s.EndsWith(".TO") || s.EndsWith(".V") || s.EndsWith(".CN") || s.EndsWith(".NE") ? MarketExchange.Tsx : MarketExchange.Us;
    }

    public static bool IsOpen(MarketExchange ex, DateOnly d) =>
        d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !Holidays(ex, d.Year).Contains(d);

    public static IReadOnlySet<DateOnly> Holidays(MarketExchange ex, int y)
    {
        var h = new HashSet<DateOnly>();
        var goodFriday = Easter(y).AddDays(-2);
        h.Add(goodFriday);
        if (ex == MarketExchange.Tsx)
        {
            h.Add(Observed(new DateOnly(y, 1, 1), false));
            h.Add(NthWeekday(y, 2, DayOfWeek.Monday, 3));                 // Family Day
            h.Add(VictoriaDay(y));
            h.Add(Observed(new DateOnly(y, 7, 1), false));                // Canada Day (weekend -> Monday)
            h.Add(NthWeekday(y, 8, DayOfWeek.Monday, 1));                 // Civic Holiday
            h.Add(NthWeekday(y, 9, DayOfWeek.Monday, 1));                 // Labour Day
            h.Add(NthWeekday(y, 10, DayOfWeek.Monday, 2));                // Thanksgiving
            var (xmas, boxing) = CanadianChristmas(y);
            h.Add(xmas);
            h.Add(boxing);
        }
        else
        {
            h.Add(Observed(new DateOnly(y, 1, 1), false));
            h.Add(NthWeekday(y, 1, DayOfWeek.Monday, 3));                 // MLK
            h.Add(NthWeekday(y, 2, DayOfWeek.Monday, 3));                 // Presidents' Day
            h.Add(LastWeekday(y, 5, DayOfWeek.Monday));                   // Memorial Day
            h.Add(Observed(new DateOnly(y, 6, 19), true));                // Juneteenth
            h.Add(Observed(new DateOnly(y, 7, 4), true));                 // Independence Day
            h.Add(NthWeekday(y, 9, DayOfWeek.Monday, 1));                 // Labor Day
            h.Add(NthWeekday(y, 11, DayOfWeek.Thursday, 4));              // Thanksgiving
            h.Add(Observed(new DateOnly(y, 12, 25), true));
        }
        return h;
    }

    /// <summary>Saturday holidays move to Friday only when <paramref name="satToFri"/> is set; Sunday moves to Monday.</summary>
    private static DateOnly Observed(DateOnly d, bool satToFri) => d.DayOfWeek switch
    {
        DayOfWeek.Sunday => d.AddDays(1),
        DayOfWeek.Saturday when satToFri => d.AddDays(-1),
        _ => d,
    };

    private static (DateOnly Christmas, DateOnly Boxing) CanadianChristmas(int y)
    {
        var c = new DateOnly(y, 12, 25);
        return c.DayOfWeek switch
        {
            DayOfWeek.Saturday => (c.AddDays(2), c.AddDays(3)),   // Mon 27, Tue 28
            DayOfWeek.Sunday => (c.AddDays(1), c.AddDays(2)),     // Mon 26, Tue 27
            DayOfWeek.Friday => (c, c.AddDays(3)),                // Fri 25, Mon 28
            _ => (c, c.AddDays(1)),
        };
    }

    private static DateOnly NthWeekday(int y, int m, DayOfWeek dow, int n)
    {
        var d = new DateOnly(y, m, 1);
        while (d.DayOfWeek != dow) d = d.AddDays(1);
        return d.AddDays(7 * (n - 1));
    }

    private static DateOnly LastWeekday(int y, int m, DayOfWeek dow)
    {
        var d = new DateOnly(y, m, DateTime.DaysInMonth(y, m));
        while (d.DayOfWeek != dow) d = d.AddDays(-1);
        return d;
    }

    /// <summary>Victoria Day: the Monday before May 25.</summary>
    private static DateOnly VictoriaDay(int y)
    {
        var d = new DateOnly(y, 5, 24);
        while (d.DayOfWeek != DayOfWeek.Monday) d = d.AddDays(-1);
        return d;
    }

    private static DateOnly Easter(int y)
    {
        var a = y % 19; var b = y / 100; var c = y % 100; var d = b / 4; var e = b % 4;
        var f = (b + 8) / 25; var g = (b - f + 1) / 3; var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4; var k = c % 4; var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var month = (h + l - 7 * m + 114) / 31; var day = (h + l - 7 * m + 114) % 31 + 1;
        return new DateOnly(y, month, day);
    }
}
