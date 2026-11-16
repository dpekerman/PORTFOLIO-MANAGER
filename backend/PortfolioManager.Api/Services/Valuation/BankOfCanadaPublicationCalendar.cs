namespace PortfolioManager.Api.Services.Valuation;

/// <summary>
/// Production publication calendar for the Bank of Canada daily USD/CAD rate: weekdays except federal bank holidays,
/// published at the existing 16:30 ET cutoff. A holiday missing here is absorbed by the rate source's one-session tolerance.
/// </summary>
public sealed class BankOfCanadaPublicationCalendar : IFxPublicationCalendar
{
    private const int FirstYear = 2000;
    private const int LastYear = 2100;

    public bool Covers(DateOnly fromInclusive, DateOnly toInclusive) =>
        fromInclusive <= toInclusive && fromInclusive.Year >= FirstYear && toInclusive.Year <= LastYear;

    public IReadOnlyList<Pm029FxPublicationDay> GetDays(DateOnly fromInclusive, DateOnly toInclusive)
    {
        if (!Covers(fromInclusive, toInclusive))
            throw new ArgumentOutOfRangeException(nameof(fromInclusive), "The Bank of Canada calendar covers 2000-2100.");
        var days = new List<Pm029FxPublicationDay>();
        for (var d = fromInclusive; d <= toInclusive; d = d.AddDays(1))
        {
            var publishes = d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !Holidays(d.Year).Contains(d);
            days.Add(new(d, publishes, publishes ? BankOfCanadaPublicationClock.ExpectedDailyCutoffUtc(d) : null));
        }
        return days;
    }

    internal static HashSet<DateOnly> Holidays(int year)
    {
        var easter = EasterSunday(year);
        var set = new HashSet<DateOnly>
        {
            Observed(new(year, 1, 1)), Observed(new(year, 7, 1)), Observed(new(year, 11, 11)),
            Observed(new(year, 12, 25)), Observed(new(year, 12, 26)),
            easter.AddDays(-2),
            NthMonday(year, 8, 1), NthMonday(year, 9, 1), NthMonday(year, 10, 2),
        };
        var victoria = new DateOnly(year, 5, 24);
        while (victoria.DayOfWeek != DayOfWeek.Monday) victoria = victoria.AddDays(-1);
        set.Add(victoria);
        if (year >= 2021) set.Add(Observed(new(year, 9, 30)));
        return set;
    }

    private static DateOnly Observed(DateOnly d) => d.DayOfWeek == DayOfWeek.Sunday ? d.AddDays(1) : d;

    private static DateOnly NthMonday(int year, int month, int n)
    {
        var d = new DateOnly(year, month, 1);
        while (d.DayOfWeek != DayOfWeek.Monday) d = d.AddDays(1);
        return d.AddDays(7 * (n - 1));
    }

    private static DateOnly EasterSunday(int y)
    {
        int a = y % 19, b = y / 100, c = y % 100, d = b / 4, e = b % 4, f = (b + 8) / 25, g = (b - f + 1) / 3;
        int h = (19 * a + b - d - g + 15) % 30, i = c / 4, k = c % 4, l = (32 + 2 * e + 2 * i - h - k) % 7;
        int m = (a + 11 * h + 22 * l) / 451, month = (h + l - 7 * m + 114) / 31, day = (h + l - 7 * m + 114) % 31 + 1;
        return new DateOnly(y, month, day);
    }
}
