using PortfolioManager.Api.Services;
using PortfolioManager.Api.Services.Valuation;

namespace PortfolioManager.Tests;

public sealed class BankOfCanadaFxProviderTests
{
    [Theory]
    [InlineData("boc-fxusdcad-2025-11-26_2025-12-03.json", "2025-11-26", "2025-12-03", "2025-11-26", 1.4059)]
    [InlineData("boc-fxusdcad-2025-11-26_2025-12-03.json", "2025-11-26", "2025-12-03", "2025-11-27", 1.4034)]
    [InlineData("boc-fxusdcad-2026-11-09_2026-11-13.json", "2026-11-09", "2026-11-13", "2026-11-10", 1.4181)]
    [InlineData("boc-fxusdcad-2026-11-09_2026-11-13.json", "2026-11-09", "2026-11-13", "2026-11-12", 1.4255)]
    public async Task ParsesOfficialUsdCadObservations(
        string fixtureName,
        string startDate,
        string endDate,
        string observationDate,
        decimal expectedRate)
    {
        var retrievedAt = new DateTimeOffset(2026, 11, 15, 15, 0, 0, TimeSpan.Zero);
        var handler = new FixtureHandler(await ReadFixtureAsync(fixtureName));
        using var http = new HttpClient(handler);
        var provider = new BankOfCanadaFxProvider(http, new FixedTimeProvider(retrievedAt));

        var observations = await provider.GetUsdCadObservationsAsync(
            DateOnly.ParseExact(startDate, "yyyy-MM-dd"),
            DateOnly.ParseExact(endDate, "yyyy-MM-dd"));

        var observation = Assert.Single(observations, item =>
            item.ObservationDate == DateOnly.ParseExact(observationDate, "yyyy-MM-dd"));
        Assert.Equal("Bank of Canada", observation.Source);
        Assert.Equal("FXUSDCAD", observation.Series);
        Assert.Equal("USD", observation.BaseCurrency);
        Assert.Equal("CAD", observation.QuoteCurrency);
        Assert.Equal(expectedRate, observation.Rate);
        Assert.Equal(retrievedAt, observation.RetrievedAtUtc);
        Assert.Contains($"start_date={startDate}", handler.RequestUri!.Query);
        Assert.Contains($"end_date={endDate}", handler.RequestUri.Query);
    }

    [Fact]
    public async Task OfficialSeriesIncludesUsThanksgivingEvenThoughNyseIsClosed()
    {
        var handler = new FixtureHandler(await ReadFixtureAsync("boc-fxusdcad-2025-11-26_2025-12-03.json"));
        using var http = new HttpClient(handler);
        var provider = new BankOfCanadaFxProvider(http,
            new FixedTimeProvider(new DateTimeOffset(2025, 12, 4, 12, 0, 0, TimeSpan.Zero)));

        var observations = await provider.GetUsdCadObservationsAsync(
            new DateOnly(2025, 11, 26), new DateOnly(2025, 12, 3));

        var thanksgivingObservation = Assert.Single(observations, observation =>
            observation.ObservationDate == new DateOnly(2025, 11, 27));
        Assert.Equal(new DateOnly(2025, 11, 27), thanksgivingObservation.ObservationDate);
        Assert.Equal(1.4034m, thanksgivingObservation.Rate);
        Assert.False(TradingCalendar.IsTradingDay(new DateOnly(2025, 11, 27), MarketExchange.Nyse));
    }

    [Fact]
    public async Task OfficialSeriesOmitsTheBankOfCanadaHolidayAndCalendarMarksItClosed()
    {
        var handler = new FixtureHandler(await ReadFixtureAsync("boc-fxusdcad-2026-11-09_2026-11-13.json"));
        using var http = new HttpClient(handler);
        var provider = new BankOfCanadaFxProvider(http,
            new FixedTimeProvider(new DateTimeOffset(2026, 11, 15, 15, 0, 0, TimeSpan.Zero)));
        var observations = await provider.GetUsdCadObservationsAsync(
            new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 13));
        var holiday = new DateOnly(2026, 11, 11);
        var calendar = new ExplicitFxPublicationCalendar(
            new DateOnly(2026, 11, 9),
            new DateOnly(2026, 11, 13),
            new[]
            {
                new Pm029FxPublicationDay(new DateOnly(2026, 11, 9), true,
                    BankOfCanadaPublicationClock.ExpectedDailyCutoffUtc(new DateOnly(2026, 11, 9))),
                new Pm029FxPublicationDay(new DateOnly(2026, 11, 10), true,
                    BankOfCanadaPublicationClock.ExpectedDailyCutoffUtc(new DateOnly(2026, 11, 10))),
                new Pm029FxPublicationDay(holiday, false, null),
                new Pm029FxPublicationDay(new DateOnly(2026, 11, 12), true,
                    BankOfCanadaPublicationClock.ExpectedDailyCutoffUtc(new DateOnly(2026, 11, 12))),
                new Pm029FxPublicationDay(new DateOnly(2026, 11, 13), true,
                    BankOfCanadaPublicationClock.ExpectedDailyCutoffUtc(new DateOnly(2026, 11, 13)))
            });

        Assert.DoesNotContain(observations, observation => observation.ObservationDate == holiday);
        Assert.False(calendar.GetDays(holiday, holiday).Single().IsPublicationSession);
    }

    [Fact]
    public async Task ProviderSurfacesHttpFailures()
    {
        using var http = new HttpClient(new FixtureHandler("", System.Net.HttpStatusCode.ServiceUnavailable));
        var provider = new BankOfCanadaFxProvider(http,
            new FixedTimeProvider(new DateTimeOffset(2026, 11, 15, 15, 0, 0, TimeSpan.Zero)));

        await Assert.ThrowsAsync<HttpRequestException>(() => provider.GetUsdCadObservationsAsync(
            new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 13)));
    }

    private static Task<string> ReadFixtureAsync(string fixtureName) =>
        File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureName));

    private sealed class FixtureHandler(string body, System.Net.HttpStatusCode status = System.Net.HttpStatusCode.OK)
        : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body)
            });
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
