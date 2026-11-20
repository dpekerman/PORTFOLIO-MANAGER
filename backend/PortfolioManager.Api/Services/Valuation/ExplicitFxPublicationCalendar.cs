namespace PortfolioManager.Api.Services.Valuation;

public sealed class ExplicitFxPublicationCalendar : IFxPublicationCalendar
{
    private readonly DateOnly _coveredFrom;
    private readonly DateOnly _coveredThrough;
    private readonly IReadOnlyDictionary<DateOnly, Pm029FxPublicationDay> _days;

    public ExplicitFxPublicationCalendar(
        DateOnly coveredFrom,
        DateOnly coveredThrough,
        IEnumerable<Pm029FxPublicationDay> days)
    {
        if (coveredThrough < coveredFrom)
            throw new ArgumentOutOfRangeException(nameof(coveredThrough));

        _coveredFrom = coveredFrom;
        _coveredThrough = coveredThrough;
        var entries = days.ToDictionary(day => day.Date);
        var expectedCount = coveredThrough.DayNumber - coveredFrom.DayNumber + 1;
        if (entries.Count != expectedCount
            || Enumerable.Range(0, expectedCount).Any(offset => !entries.ContainsKey(coveredFrom.AddDays(offset))))
            throw new ArgumentException("The explicit FX calendar must classify every date in its coverage range.", nameof(days));

        foreach (var day in entries.Values)
        {
            if (day.IsPublicationSession)
            {
                var expectedCutoff = BankOfCanadaPublicationClock.ExpectedDailyCutoffUtc(day.Date);
                if (day.ExpectedPublicationAtUtc?.ToUniversalTime() != expectedCutoff)
                    throw new ArgumentException("Publication sessions must use the Bank of Canada 16:30 ET cutoff.", nameof(days));
            }
            else if (day.ExpectedPublicationAtUtc is not null)
            {
                throw new ArgumentException("A non-publication day cannot have a publication cutoff.", nameof(days));
            }
        }

        _days = entries;
    }

    public bool Covers(DateOnly fromInclusive, DateOnly toInclusive) =>
        fromInclusive >= _coveredFrom && toInclusive <= _coveredThrough && toInclusive >= fromInclusive;

    public IReadOnlyList<Pm029FxPublicationDay> GetDays(DateOnly fromInclusive, DateOnly toInclusive)
    {
        if (!Covers(fromInclusive, toInclusive))
            throw new ArgumentOutOfRangeException(nameof(fromInclusive), "The requested dates are outside explicit FX calendar coverage.");

        return Enumerable.Range(0, toInclusive.DayNumber - fromInclusive.DayNumber + 1)
            .Select(offset => _days[fromInclusive.AddDays(offset)])
            .ToArray();
    }
}

public static class BankOfCanadaPublicationClock
{
    public static DateTimeOffset ExpectedDailyCutoffUtc(DateOnly date)
    {
        var eastern = MarketHoursGate.GetEasternTimeZone()
            ?? throw new TimeZoneNotFoundException("Eastern Time is unavailable for the Bank of Canada publication cutoff.");
        var localCutoff = DateTime.SpecifyKind(date.ToDateTime(new TimeOnly(16, 30)), DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localCutoff, eastern), TimeSpan.Zero);
    }
}
