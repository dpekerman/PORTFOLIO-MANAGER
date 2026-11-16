using Microsoft.Extensions.Caching.Memory;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services.Valuation;

namespace PortfolioManager.Tests;

public sealed class Pm029Gate2FxTests
{
    private sealed class FakeProvider(params (string Date, decimal Rate)[] rows) : IBankOfCanadaFxProvider
    {
        public bool Throw { get; init; }
        public Task<IReadOnlyList<Pm029FxObservation>> GetUsdCadObservationsAsync(DateOnly startDate, DateOnly endDate, CancellationToken ct = default)
        {
            if (Throw) throw new HttpRequestException("offline");
            var asOf = DateTimeOffset.UtcNow;
            return Task.FromResult<IReadOnlyList<Pm029FxObservation>>(rows
                .Select(r => new Pm029FxObservation("Bank of Canada", "FXUSDCAD", "USD", "CAD", DateOnly.Parse(r.Date), r.Rate, asOf))
                .Where(o => o.ObservationDate >= startDate && o.ObservationDate <= endDate).ToList());
        }
    }

    private static UsdCadRateSource Source(FakeProvider provider) =>
        new(provider, new BankOfCanadaPublicationCalendar(), new MemoryCache(new MemoryCacheOptions()));

    // Wednesday 2026-11-18: 16:30 ET is 21:30 UTC.
    private static readonly DateTimeOffset AfterCutoff = new(2026, 11, 18, 22, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset BeforeCutoff = new(2026, 11, 18, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task UsesTheLatestPublishedObservation_CadPerUsd()
    {
        var rate = await Source(new FakeProvider(("2026-11-17", 1.41m), ("2026-11-18", 1.42m))).GetAsync(AfterCutoff);
        Assert.True(rate.IsAvailable);
        Assert.Equal(1.42m, rate.Rate);
        Assert.Equal(new DateOnly(2026, 11, 18), rate.ObservationDate);
    }

    [Fact]
    public async Task NeverUsesAnObservationNotYetPublishedAtTheEvaluationTime()
    {
        var rate = await Source(new FakeProvider(("2026-11-17", 1.41m), ("2026-11-18", 1.42m))).GetAsync(BeforeCutoff);
        Assert.Equal(1.41m, rate.Rate);   // 11-18 is not published until 16:30 ET, so its observation is look-ahead and ignored
        Assert.Equal(new DateOnly(2026, 11, 17), rate.ObservationDate);
    }

    [Fact]
    public async Task ToleratesOneMissedPublicationButNotMore()
    {
        var lagging = await Source(new FakeProvider(("2026-11-17", 1.41m))).GetAsync(AfterCutoff);
        Assert.True(lagging.IsAvailable);

        var stale = await Source(new FakeProvider(("2026-11-13", 1.4257m))).GetAsync(AfterCutoff);
        Assert.False(stale.IsAvailable);
        Assert.Null(stale.Rate);
        Assert.Equal(Pm029ValuationIssueCodes.FxStale, stale.IssueCode);
    }

    [Fact]
    public async Task MissingOrFailedRateIsUnavailable_NeverOne()
    {
        var none = await Source(new FakeProvider()).GetAsync(AfterCutoff);
        Assert.Equal(Pm029ValuationIssueCodes.FxUnavailable, none.IssueCode);
        Assert.Null(none.Rate);

        var offline = await Source(new FakeProvider { Throw = true }).GetAsync(AfterCutoff);
        Assert.Equal(Pm029ValuationIssueCodes.FxUnavailable, offline.IssueCode);
        Assert.Null(offline.Rate);
    }

    [Fact]
    public void CalendarSkipsWeekendsAndFederalHolidays()
    {
        var days = new BankOfCanadaPublicationCalendar().GetDays(new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 6))
            .ToDictionary(d => d.Date, d => d.IsPublicationSession);
        Assert.True(days[new DateOnly(2026, 4, 2)]);
        Assert.False(days[new DateOnly(2026, 4, 3)]);   // Good Friday
        Assert.False(days[new DateOnly(2026, 4, 4)]);   // Saturday
        Assert.True(days[new DateOnly(2026, 4, 6)]);
        Assert.Contains(new DateOnly(2026, 11, 11), BankOfCanadaPublicationCalendar.Holidays(2026));
        Assert.Contains(new DateOnly(2026, 5, 18), BankOfCanadaPublicationCalendar.Holidays(2026));   // Victoria Day
    }
}
