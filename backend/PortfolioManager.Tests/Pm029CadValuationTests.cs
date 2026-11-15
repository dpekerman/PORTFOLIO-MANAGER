using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using PortfolioManager.Api.Services.Valuation;

namespace PortfolioManager.Tests;

public sealed class Pm029CadValuationTests
{
    private static readonly DateOnly FxStart = new(2025, 11, 26);
    private static readonly DateOnly FxEnd = new(2025, 12, 1);
    private static readonly DateTimeOffset FxCutoff = new(2025, 11, 26, 21, 30, 0, TimeSpan.Zero);

    [Fact]
    public void ConvertsWithOfficialRateAndPreservesFullPrecision()
    {
        var valuation = Evaluate(
            [Security("stock:1", "USD", 2m, 10m, FxStart)],
            [Fx(FxStart, 1.4059m, FxCutoff)],
            FxStart,
            FxCutoff.AddMinutes(10),
            Calendar(FxStart, FxStart, FxStart));

        Assert.True(valuation.Complete);
        Assert.Equal(28.118m, valuation.DenominatorCad);
        Assert.Equal(28.118m, valuation.StockOnlyCad);
        var component = Assert.Single(valuation.Components);
        Assert.Equal(1.4059m, component.FxRateCadPerUsd);
        Assert.Equal(FxStart, component.FxObservationDate);
        Assert.Equal("Verified listing or account evidence",
            Assert.IsType<Pm029SecurityValuationInput>(component.Input).ListingCurrency!.Source);
    }

    [Fact]
    public void CanadianListingCurrencyDoesNotRequireFxForUnderlyingAnalysisCurrency()
    {
        var valuation = Evaluate(
            [Security("stock:cdr", "CAD", 2m, 25m, FxStart, symbol: "example-CDR.TO")],
            [],
            FxStart,
            FxCutoff.AddMinutes(10),
            Calendar(FxStart, FxStart));

        Assert.True(valuation.Complete);
        Assert.Equal(50m, valuation.DenominatorCad);
        Assert.Null(Assert.Single(valuation.Components).FxRateCadPerUsd);
    }

    [Fact]
    public void FridayFxObservationCanBeUsedOnSaturdayWithoutAWeekendPublicationSession()
    {
        var friday = new DateOnly(2025, 11, 28);
        var saturday = friday.AddDays(1);
        var cutoff = BankOfCanadaPublicationClock.ExpectedDailyCutoffUtc(friday);
        var calendar = Calendar(friday, saturday, friday);

        var valuation = Evaluate(
            [Security("stock:1", "USD", 1m, 10m, friday)],
            [Fx(friday, 1.3979m, cutoff)],
            friday,
            new DateTimeOffset(2025, 11, 29, 15, 0, 0, TimeSpan.Zero),
            calendar);

        Assert.True(valuation.Complete);
        Assert.Equal(friday, valuation.Components[0].FxObservationDate);
        Assert.False(calendar.GetDays(friday, saturday).Single(day => day.Date == saturday).IsPublicationSession);
    }

    [Fact]
    public void MissingMarketQuoteFailsClosedWithoutCostBasisFallback()
    {
        var security = Security("stock:missing", "CAD", 4m, null, FxStart);
        var valuation = Evaluate(
            [security],
            [],
            FxStart,
            FxCutoff.AddMinutes(10),
            Calendar(FxStart, FxStart));

        Assert.False(valuation.Complete);
        Assert.Null(valuation.DenominatorCad);
        Assert.Null(valuation.Components[0].NativeValue);
        Assert.Contains(Pm029ValuationIssueCodes.QuoteProvenanceMissing, valuation.IssueCodes);
    }

    [Fact]
    public void MismatchedAndFutureQuotesFailClosed()
    {
        var session = FxStart;
        var expectedClose = MarketCloseUtc(session);
        var wrongSession = Security("stock:wrong-session", "CAD", 1m, 10m, session) with
        {
            QuoteSession = session.AddDays(-1),
            ExpectedSessionCloseUtc = MarketCloseUtc(session.AddDays(-1))
        };
        var futureQuote = Security("stock:future", "CAD", 1m, 10m, session) with
        {
            QuoteAsOfUtc = expectedClose.AddHours(1)
        };
        var asOf = expectedClose.AddMinutes(10);
        var calendar = Calendar(session, session);

        var mismatched = Evaluate([wrongSession], [], session, asOf, calendar);
        var future = Evaluate([futureQuote], [], session, asOf, calendar);

        Assert.False(mismatched.Complete);
        Assert.Contains(Pm029ValuationIssueCodes.QuoteSessionMismatch, mismatched.IssueCodes);
        Assert.False(future.Complete);
        Assert.Contains(Pm029ValuationIssueCodes.QuoteNotAvailableAsOf, future.IssueCodes);
    }

    [Fact]
    public void UnverifiedOptionTermsFailClosed()
    {
        var session = FxStart;
        var unverified = Option("option:unverified", 1, 1m, 100m, session) with
        {
            TermsVerified = false,
            TermsSource = null,
            ContractMultiplier = null
        };

        var valuation = Evaluate(
            [Security("stock:1", "CAD", 1m, 100m, session), unverified],
            [],
            session,
            FxCutoff.AddMinutes(10),
            Calendar(session, session));

        Assert.False(valuation.Complete);
        Assert.Null(valuation.DenominatorCad);
        Assert.Contains(Pm029ValuationIssueCodes.OptionTermsUnverified, valuation.IssueCodes);
    }

    [Fact]
    public void MultipleInvestmentRolesDoNotSelectAHiddenPositionLimit()
    {
        var session = FxStart;
        var valuation = Evaluate(
            [
                Security("stock:swing", "CAD", 1m, 100m, session, role: "Swing", symbol: "DUAL"),
                Security("stock:strategic", "CAD", 1m, 100m, session, role: "Strategic", symbol: "DUAL")
            ],
            [],
            session,
            FxCutoff.AddMinutes(10),
            Calendar(session, session));

        var evidence = Pm029PortfolioFitEvidenceCalculator.Evaluate(
            valuation, "DUAL", 10m, "Technology", 50m, 2m);

        Assert.True(evidence.Reliable);
        Assert.True(evidence.RolePolicyAmbiguous);
        Assert.Equal(100m, evidence.PositionPct);
        Assert.Null(evidence.PositionLimitExceeded);
    }

    [Fact]
    public void ThreePublicationSessionAgeIsAnExplicitProvisionalInput()
    {
        var calendar = Calendar(
            FxStart,
            FxEnd,
            new DateOnly(2025, 11, 26),
            new DateOnly(2025, 11, 27),
            new DateOnly(2025, 11, 28),
            new DateOnly(2025, 12, 1));
        var security = Security("stock:1", "USD", 1m, 10m, FxEnd);
        var oldObservation = Fx(FxStart, 1.4059m, FxCutoff);

        var ageThree = Evaluate([security], [oldObservation], FxEnd,
            new DateTimeOffset(2025, 12, 1, 22, 0, 0, TimeSpan.Zero), calendar, maximumAge: 3);
        var ageThreeExceeded = Evaluate([security], [oldObservation], FxEnd,
            new DateTimeOffset(2025, 12, 1, 22, 0, 0, TimeSpan.Zero), calendar, maximumAge: 2);

        Assert.True(ageThree.Complete);
        Assert.False(ageThreeExceeded.Complete);
        Assert.Contains(Pm029ValuationIssueCodes.FxStale, ageThreeExceeded.IssueCodes);
        Assert.Equal(3, ageThree.ProvisionalMaximumFxPublicationSessions);
    }

    [Fact]
    public void DelayedObservationIsNotUsedBeforeItsFirstObservedTime()
    {
        var session = new DateOnly(2025, 11, 28);
        var asOf = new DateTimeOffset(2025, 11, 28, 21, 45, 0, TimeSpan.Zero);
        var latestObservation = Fx(session, 1.3979m,
            new DateTimeOffset(2025, 11, 28, 22, 0, 0, TimeSpan.Zero));
        var priorObservation = Fx(new DateOnly(2025, 11, 27), 1.4034m,
            new DateTimeOffset(2025, 11, 27, 21, 30, 0, TimeSpan.Zero));
        var calendar = Calendar(new DateOnly(2025, 11, 27), session,
            new DateOnly(2025, 11, 27), session);

        var valuation = Evaluate(
            [Security("stock:1", "USD", 1m, 10m, session)],
            [priorObservation, latestObservation],
            session,
            asOf,
            calendar);

        Assert.True(valuation.Complete);
        Assert.Equal(new DateOnly(2025, 11, 27), valuation.Components[0].FxObservationDate);
        Assert.Equal(1.4034m, valuation.Components[0].FxRateCadPerUsd);
    }

    [Fact]
    public void ConflictingRatesForOneObservationDateFailClosed()
    {
        var session = FxStart;
        var cutoff = BankOfCanadaPublicationClock.ExpectedDailyCutoffUtc(session);
        var valuation = Evaluate(
            [Security("stock:1", "USD", 1m, 10m, session)],
            [Fx(session, 1.4059m, cutoff), Fx(session, 1.4060m, cutoff.AddMinutes(1))],
            session,
            cutoff.AddMinutes(10),
            Calendar(session, session, session));

        Assert.False(valuation.Complete);
        Assert.Null(valuation.DenominatorCad);
        Assert.Contains(Pm029ValuationIssueCodes.FxObservationDuplicated, valuation.IssueCodes);
    }

    [Fact]
    public void BankOfCanadaHolidayDoesNotAgeThePriorFxRate()
    {
        var start = new DateOnly(2026, 11, 10);
        var holiday = new DateOnly(2026, 11, 11);
        var end = new DateOnly(2026, 11, 12);
        var cutoffNov10 = BankOfCanadaPublicationClock.ExpectedDailyCutoffUtc(start);
        var asOf = new DateTimeOffset(2026, 11, 12, 22, 0, 0, TimeSpan.Zero);
        var calendar = Calendar(start, end, start, end);
        var fxNov10 = Fx(start, 1.4181m, cutoffNov10);
        var fxNov12Delayed = Fx(end, 1.4255m,
            new DateTimeOffset(2026, 11, 12, 23, 0, 0, TimeSpan.Zero));

        var valuation = Evaluate(
            [Security("stock:1", "USD", 1m, 10m, end)],
            [fxNov10, fxNov12Delayed],
            end,
            asOf,
            calendar);

        Assert.True(valuation.Complete);
        Assert.Equal(start, valuation.Components[0].FxObservationDate);
        Assert.Equal(1.4181m, valuation.Components[0].FxRateCadPerUsd);
        Assert.DoesNotContain(calendar.GetDays(start, end), day => day.Date == holiday && day.IsPublicationSession);
    }

    [Fact]
    public void PublicationCutoffUsesEasternDaylightAndStandardTime()
    {
        Assert.Equal(
            new DateTimeOffset(2025, 11, 27, 21, 30, 0, TimeSpan.Zero),
            BankOfCanadaPublicationClock.ExpectedDailyCutoffUtc(new DateOnly(2025, 11, 27)));
        Assert.Equal(
            new DateTimeOffset(2025, 7, 7, 20, 30, 0, TimeSpan.Zero),
            BankOfCanadaPublicationClock.ExpectedDailyCutoffUtc(new DateOnly(2025, 7, 7)));
    }

    [Fact]
    public void SignedOptionsUseTheirVerifiedIndividualMultipliers()
    {
        var session = FxStart;
        var valuation = Evaluate(
            [
                Security("stock:1", "CAD", 1m, 500m, session, sector: "Technology"),
                Cash("cash:1", 100m, "CAD", session),
                Option("option:long", 2, 1.5m, 10m, session),
                Option("option:short", -1, 2m, 100m, session)
            ],
            [],
            session,
            FxCutoff.AddMinutes(10),
            Calendar(session, session));

        Assert.True(valuation.Complete);
        Assert.Equal(430m, valuation.DenominatorCad);
        Assert.Equal(500m, valuation.StockOnlyCad);
        Assert.Equal(30m, valuation.Components.Single(component => component.Id == "option:long").ValueCad);
        Assert.Equal(-200m, valuation.Components.Single(component => component.Id == "option:short").ValueCad);
        Assert.Equal("Verified contract terms",
            Assert.IsType<Pm029OptionValuationInput>(
                valuation.Components.Single(component => component.Id == "option:long").Input).TermsSource);
        Assert.Equal("Complete cash ledger",
            Assert.IsType<Pm029CashBalanceValuationInput>(
                valuation.Components.Single(component => component.Id == "cash:1").Input).LedgerSource);
    }

    [Fact]
    public void OwnershipAndAccountScopeIncludeSharedComponentsOnlyOnce()
    {
        var session = FxStart;
        var components = new Pm029ValuationComponentInput[]
        {
            Security("stock:a", "CAD", 1m, 100m, session, account: "Account A", owner: "user-a"),
            Cash("cash:shared", 50m, "CAD", session, account: "Account A", owner: null),
            Security("stock:b", "CAD", 1m, 300m, session, account: "Account B", owner: "user-a"),
            Security("stock:other", "CAD", 1m, 1000m, session, account: "Account A", owner: "user-b")
        };
        var calendar = Calendar(session, session);

        var consolidated = Evaluate(components, [], session, FxCutoff.AddMinutes(10), calendar);
        var accountA = Evaluate(components, [], session, FxCutoff.AddMinutes(10), calendar, accountScope: "Account A");
        var accountB = Evaluate(components, [], session, FxCutoff.AddMinutes(10), calendar, accountScope: "Account B");

        Assert.Equal(450m, consolidated.DenominatorCad);
        Assert.Equal(150m, accountA.DenominatorCad);
        Assert.Equal(300m, accountB.DenominatorCad);
    }

    [Fact]
    public void DuplicateLinkedCashEvidenceFailsClosed()
    {
        var session = FxStart;
        var components = new Pm029ValuationComponentInput[]
        {
            Security("stock:1", "CAD", 1m, 100m, session),
            Cash("cash:a", 50m, "CAD", session, linkedTradeKeys: ["PortfolioOpen:7"]),
            Cash("cash:b", 25m, "CAD", session, linkedTradeKeys: ["portfolioopen:7"])
        };

        var valuation = Evaluate(components, [], session, FxCutoff.AddMinutes(10), Calendar(session, session));

        Assert.False(valuation.Complete);
        Assert.Null(valuation.DenominatorCad);
        Assert.Contains(Pm029ValuationIssueCodes.CashTradeLinkDuplicated, valuation.IssueCodes);
    }

    [Fact]
    public void PositionWeightUsesConsolidatedDenominatorAndSectorWeightUsesStocksOnly()
    {
        var session = FxStart;
        var valuation = Evaluate(
            [
                Security("stock:tech", "CAD", 1m, 100m, session, "Technology", "Swing", symbol: "TECH"),
                Security("stock:health", "CAD", 1m, 100m, session, "Health", "Strategic", symbol: "HEALTH"),
                Cash("cash:1", 700m, "CAD", session),
                Option("option:1", 1, 1m, 100m, session)
            ],
            [],
            session,
            FxCutoff.AddMinutes(10),
            Calendar(session, session));

        var evidence = Pm029PortfolioFitEvidenceCalculator.Evaluate(
            valuation, "TECH", 9m, "Technology", 45m, 2m);

        Assert.True(evidence.Reliable);
        Assert.Equal(10m, evidence.PositionPct);
        Assert.True(evidence.PositionLimitExceeded);
        Assert.Equal(50m, evidence.SectorPct);
        Assert.True(evidence.SectorOverTarget);
    }

    [Fact]
    public void IncompleteValuationCannotProduceAllocationEvidence()
    {
        var valuation = Evaluate(
            [Security("stock:missing", "CAD", 1m, null, FxStart)],
            [],
            FxStart,
            FxCutoff.AddMinutes(10),
            Calendar(FxStart, FxStart));

        var evidence = Pm029PortfolioFitEvidenceCalculator.Evaluate(
            valuation, "stock:missing", 10m, "Technology", 30m, 2m);

        Assert.False(evidence.Reliable);
        Assert.Null(evidence.PositionPct);
        Assert.Null(evidence.SectorPct);
    }

    [Fact]
    public void DuplicateComponentIdentifiersAreRejected()
    {
        var session = FxStart;
        var components = new Pm029ValuationComponentInput[]
        {
            Security("same-id", "CAD", 1m, 100m, session),
            Cash("same-id", 50m, "CAD", session)
        };
        var calculator = Calculator(Calendar(session, session));

        Assert.Throws<ArgumentException>(() => calculator.Evaluate(Request(
            components, [], session, FxCutoff.AddMinutes(10))));
    }

    private static Pm029CadValuationResult Evaluate(
        IReadOnlyList<Pm029ValuationComponentInput> components,
        IReadOnlyList<Pm029FxObservation> observations,
        DateOnly valuationSession,
        DateTimeOffset asOfUtc,
        IFxPublicationCalendar calendar,
        int maximumAge = 3,
        string accountScope = Pm029ValuationScopes.Consolidated) =>
        Calculator(calendar).Evaluate(Request(components, observations, valuationSession, asOfUtc, maximumAge, accountScope));

    private static Pm029ValuationRequest Request(
        IReadOnlyList<Pm029ValuationComponentInput> components,
        IReadOnlyList<Pm029FxObservation> observations,
        DateOnly valuationSession,
        DateTimeOffset asOfUtc,
        int maximumAge = 3,
        string accountScope = Pm029ValuationScopes.Consolidated) =>
        new(asOfUtc, valuationSession, "user-a", accountScope, maximumAge, components, observations);

    private static Pm029CadValuationCalculator Calculator(IFxPublicationCalendar calendar) =>
        new(calendar);

    private static IFxPublicationCalendar Calendar(
        DateOnly from,
        DateOnly through,
        params DateOnly[] publicationDates)
    {
        var publicationSet = publicationDates.ToHashSet();
        var days = Enumerable.Range(0, through.DayNumber - from.DayNumber + 1)
            .Select(offset =>
            {
                var date = from.AddDays(offset);
                return publicationSet.Contains(date)
                    ? new Pm029FxPublicationDay(date, true, BankOfCanadaPublicationClock.ExpectedDailyCutoffUtc(date))
                    : new Pm029FxPublicationDay(date, false, null);
            });
        return new ExplicitFxPublicationCalendar(from, through, days);
    }

    private static Pm029SecurityValuationInput Security(
        string id,
        string currency,
        decimal? quantity,
        decimal? price,
        DateOnly session,
        string? sector = null,
        string? role = "Strategic",
        string? account = "Account A",
        string? owner = "user-a",
        string? symbol = "TEST",
        string? quoteCurrency = null)
    {
        var close = MarketCloseUtc(session);
        return new Pm029SecurityValuationInput(
            id, owner, account, symbol, sector, role, quantity,
            Currency(currency), quoteCurrency ?? currency, price, "Official market quote",
            close.AddMinutes(5), session, session, close);
    }

    private static Pm029CashBalanceValuationInput Cash(
        string id,
        decimal balance,
        string currency,
        DateOnly session,
        string? account = "Account A",
        string? owner = "user-a",
        IReadOnlyList<string>? linkedTradeKeys = null) =>
        new(id, owner, account, balance, Currency(currency),
            new DateTimeOffset(session.ToDateTime(new TimeOnly(21, 0)), TimeSpan.Zero),
            session, "Complete cash ledger", linkedTradeKeys);

    private static Pm029OptionValuationInput Option(
        string id,
        int signedContracts,
        decimal price,
        decimal multiplier,
        DateOnly session,
        string currency = "CAD")
    {
        var close = MarketCloseUtc(session);
        return new Pm029OptionValuationInput(
            id, "user-a", "Account A", id, "Strategic", Currency(currency), currency, price,
            "Verified option quote", close.AddMinutes(5), session, session, close,
            signedContracts, multiplier, true, "Verified contract terms", FxCutoff.AddDays(-1));
    }

    private static CurrencyEvidence Currency(string currency) =>
        new(currency, "Verified listing or account evidence", true, FxCutoff.AddDays(-1));

    private static Pm029FxObservation Fx(DateOnly date, decimal rate, DateTimeOffset retrievedAtUtc) =>
        new("Bank of Canada", "FXUSDCAD", "USD", "CAD", date, rate, retrievedAtUtc);

    private static DateTimeOffset MarketCloseUtc(DateOnly date)
    {
        var eastern = MarketHoursGate.GetEasternTimeZone()!;
        var close = DateTime.SpecifyKind(date.ToDateTime(new TimeOnly(16, 0)), DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(close, eastern), TimeSpan.Zero);
    }
}
