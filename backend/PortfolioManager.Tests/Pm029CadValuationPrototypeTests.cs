namespace PortfolioManager.Tests;

public sealed class Pm029CadValuationPrototypeTests
{
    private static readonly DateOnly Friday = new(2026, 11, 13);
    private static readonly DateOnly Monday = new(2026, 11, 16);
    private static readonly DateTimeOffset SaturdayAsOf = Utc(2026, 11, 14, 22, 0);

    [Fact]
    public void ConvertsCadAndUsdWithoutRoundingTheFxRate()
    {
        var result = Evaluate(
            new[]
            {
                Security("cad", "CAD", 1m, 100m),
                Security("usd", "USD", 3m, 10m)
            },
            fx: [Fx(Friday, 1.35275m, Utc(2026, 11, 13, 21, 30))]);

        Assert.True(result.Complete);
        Assert.Equal(140.58250m, result.DenominatorCad);
        var usdComponent = Assert.Single(result.Components, c => c.Id == "usd");
        Assert.Equal(30m, usdComponent.NativeMarketValue);
        Assert.Equal(1.35275m, usdComponent.FxRateCadPerUsd);
    }

    [Fact]
    public void CadOnlyDoesNotRequireFxEvidence()
    {
        var result = Evaluate(Security("cad", "CAD", 2m, 50m));

        Assert.True(result.Complete);
        Assert.Equal(100m, result.DenominatorCad);
        Assert.Null(Assert.Single(result.Components).FxRateCadPerUsd);
    }

    [Fact]
    public void MissingFxPreservesNativeEvidenceButSuppressesTheDenominator()
    {
        var result = Evaluate(Security("usd", "USD", 3m, 10m));
        var component = Assert.Single(result.Components);

        Assert.False(result.Complete);
        Assert.Null(result.DenominatorCad);
        Assert.Equal(30m, component.NativeMarketValue);
        Assert.Null(component.MarketValueCad);
        Assert.Equal(Pm029ValuationEvidenceQuality.NativeOnly, component.EvidenceQuality);
        Assert.Contains(Pm029ValuationIssue.FxUnavailable, result.ReasonCodes);
    }

    [Fact]
    public void FridayObservationCanBeReusedOverTheWeekend()
    {
        var result = Evaluate(
            Security("usd", "USD", 1m, 10m),
            asOf: Utc(2026, 11, 14, 17, 0),
            valuationSession: Friday,
            fx: [Fx(Friday, 1.35m, Utc(2026, 11, 13, 21, 30))],
            publicationSessions: [Publication(Friday, 21, 30)]);

        Assert.True(result.Complete);
        Assert.Equal(Friday, Assert.Single(result.Components).FxObservationDate);
    }

    [Fact]
    public void SameDayRateIsUnavailableBeforeItsPublicationTime()
    {
        var result = Evaluate(
            Security("usd", "USD", 1m, 10m, session: Monday),
            asOf: Utc(2026, 11, 16, 21, 29),
            valuationSession: Monday,
            fx: [Fx(Monday, 1.36m, Utc(2026, 11, 16, 21, 30))],
            publicationSessions: [Publication(Monday, 21, 30)]);

        Assert.False(result.Complete);
        Assert.Null(result.DenominatorCad);
        Assert.Contains(Pm029ValuationIssue.FxNotPublishedAsOf, result.ReasonCodes);
    }

    [Fact]
    public void CallerSuppliedNoPublicationDayDoesNotAgeTheLastRate()
    {
        var result = Evaluate(
            Security("usd", "USD", 1m, 10m, session: Monday),
            asOf: Utc(2026, 11, 16, 22, 0),
            valuationSession: Monday,
            fx: [Fx(Friday, 1.35m, Utc(2026, 11, 13, 21, 30))],
            publicationSessions: [Publication(Friday, 21, 30)],
            maximumFxPublicationDays: 0);

        Assert.True(result.Complete);
        Assert.Equal(Friday, Assert.Single(result.Components).FxObservationDate);
    }

    [Fact]
    public void FxObservationAfterAUsListingQuoteSessionIsNotSelected()
    {
        var result = Evaluate(
            Security("usd", "USD", 1m, 10m, session: Friday),
            asOf: Utc(2026, 11, 16, 22, 0),
            valuationSession: Monday,
            fx:
            [
                Fx(Friday, 1.30m, Utc(2026, 11, 13, 21, 30)),
                Fx(Monday, 1.40m, Utc(2026, 11, 16, 21, 30))
            ],
            publicationSessions: [Publication(Friday, 21, 30), Publication(Monday, 21, 30)],
            maximumFxPublicationDays: 1);

        Assert.True(result.Complete);
        Assert.Equal(Friday, Assert.Single(result.Components).FxObservationDate);
        Assert.Equal(1.30m, result.Components[0].FxRateCadPerUsd);
    }

    [Fact]
    public void RecentlyRetrievedCachedRateStillFailsWhenObservationIsStale()
    {
        var sessions = new[]
        {
            Publication(new DateOnly(2026, 11, 16), 21, 30),
            Publication(new DateOnly(2026, 11, 17), 21, 30),
            Publication(new DateOnly(2026, 11, 18), 21, 30),
            Publication(new DateOnly(2026, 11, 19), 21, 30)
        };
        var result = Evaluate(
            Security("usd", "USD", 1m, 10m, session: new DateOnly(2026, 11, 19)),
            asOf: Utc(2026, 11, 19, 22, 0),
            valuationSession: new DateOnly(2026, 11, 19),
            fx: [Fx(Friday, 1.35m, Utc(2026, 11, 13, 21, 30), Utc(2026, 11, 19, 22, 0))],
            publicationSessions: sessions,
            maximumFxPublicationDays: 3);

        Assert.False(result.Complete);
        Assert.Contains(Pm029ValuationIssue.FxStale, result.ReasonCodes);
    }

    [Fact]
    public void HistoricalAsOfDoesNotUseLaterPublishedOrFutureDatedFx()
    {
        var result = Evaluate(
            Security("usd", "USD", 1m, 10m),
            asOf: Utc(2026, 11, 13, 22, 0),
            valuationSession: Friday,
            fx:
            [
                Fx(Friday, 1.35m, Utc(2026, 11, 13, 23, 0)),
                Fx(Monday, 1.40m, Utc(2026, 11, 16, 21, 30))
            ],
            publicationSessions: [Publication(Friday, 21, 30), Publication(Monday, 21, 30)]);

        Assert.False(result.Complete);
        Assert.Contains(Pm029ValuationIssue.FxNotPublishedAsOf, result.ReasonCodes);
    }

    [Fact]
    public void UnsupportedCurrencyIsUnavailableRatherThanInferred()
    {
        var result = Evaluate(Security("eur", "EUR", 1m, 10m));

        Assert.False(result.Complete);
        Assert.Null(result.DenominatorCad);
        Assert.Contains(Pm029ValuationIssue.UnsupportedCurrency, result.ReasonCodes);
    }

    [Fact]
    public void MissingQuoteCannotFallBackToAverageCost()
    {
        var result = Evaluate(Security("missing", "CAD", 1m, null));

        Assert.False(result.Complete);
        Assert.Null(result.DenominatorCad);
        Assert.Null(Assert.Single(result.Components).NativeMarketValue);
        Assert.Contains(Pm029ValuationIssue.QuoteProvenanceMissing, result.ReasonCodes);
    }

    [Fact]
    public void QuoteFromTheWrongMarketSessionIsRejected()
    {
        var result = Evaluate(Security("stale", "CAD", 1m, 10m, session: Friday,
            quoteSession: Friday.AddDays(-1)));

        Assert.False(result.Complete);
        Assert.Contains(Pm029ValuationIssue.QuoteSessionMismatch, result.ReasonCodes);
    }

    [Fact]
    public void SharedAndAccountOwnedComponentsAreIncludedOnlyInTheirRequestedScope()
    {
        var sharedCash = Cash("shared-cash", 100m, "CAD", "Account A", isShared: true);
        var accountAStock = Security("a-stock", "CAD", 2m, 50m, accountType: "Account A");
        var accountBStock = Security("b-stock", "CAD", 3m, 100m, accountType: "Account B");
        var anotherUserStock = Security("other-user-stock", "CAD", 100m, 100m,
            accountType: "Account A", ownerId: "another-user");

        var consolidated = Evaluate(sharedCash, accountAStock, accountBStock, anotherUserStock);
        var accountA = Evaluate(new[] { sharedCash, accountAStock, accountBStock, anotherUserStock }, accountScope: "Account A");
        var accountB = Evaluate(new[] { sharedCash, accountAStock, accountBStock, anotherUserStock }, accountScope: "Account B");

        Assert.Equal(500m, consolidated.DenominatorCad);
        Assert.Equal(200m, accountA.DenominatorCad);
        Assert.Equal(300m, accountB.DenominatorCad);
        Assert.Equal(1, consolidated.Components.Count(c => c.Id == "shared-cash"));
    }

    [Fact]
    public void OptionsUseExplicitSignedContractsAndVerifiedMultiplier()
    {
        var result = Evaluate(
            new[]
            {
                Security("stock", "CAD", 5m, 100m),
                Option("long", "USD", 2m, 1.5m, 50m),
                Option("short", "CAD", -1m, 2m, 25m)
            },
            fx: [Fx(Friday, 1.35m, Utc(2026, 11, 13, 21, 30))]);

        Assert.True(result.Complete);
        Assert.Equal(652.50m, result.DenominatorCad);
        Assert.Equal(150m, result.Components.Single(c => c.Id == "long").NativeMarketValue);
        Assert.Equal(-50m, result.Components.Single(c => c.Id == "short").NativeMarketValue);
    }

    [Fact]
    public void OptionWithoutVerifiedContractTermsIsUnavailable()
    {
        var option = Option("option", "CAD", 1m, 2m, 100m) with { OptionTermsVerified = false };
        var result = Evaluate(option);

        Assert.False(result.Complete);
        Assert.Contains(Pm029ValuationIssue.OptionTermsUnverified, result.ReasonCodes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void NonPositiveDenominatorIsUnavailable(decimal balance)
    {
        var result = Evaluate(Cash("cash", balance, "CAD", "Account A"));

        Assert.False(result.Complete);
        Assert.Null(result.DenominatorCad);
        Assert.Contains(Pm029ValuationIssue.NonPositiveDenominator, result.ReasonCodes);
    }

    [Fact]
    public void PositionAndSectorEvidencePreserveTheirSeparateDenominatorsAndLimits()
    {
        var result = Evaluate(
            Security("target", "CAD", 1m, 100m, symbol: "TARGET", sector: "Technology"),
            Security("other", "CAD", 1m, 100m, symbol: "OTHER", sector: "Financials"),
            Cash("cash", 200m, "CAD", "Account A"));

        var atLimit = Pm029CadValuationPrototype.AllocationEvidence(
            result, "TARGET", 25m, "Technology", 48m, 2m);
        var overSectorTolerance = Pm029CadValuationPrototype.AllocationEvidence(
            result, "TARGET", 25m, "Technology", 47.9m, 2m);

        Assert.Equal(25m, atLimit.PositionPct);
        Assert.False(atLimit.PositionLimitExceeded);
        Assert.Equal(50m, atLimit.SectorPct);
        Assert.False(atLimit.SectorOverTarget);
        Assert.True(overSectorTolerance.SectorOverTarget);
    }

    [Fact]
    public void SameSecurityAcrossRolesAggregatesExposureButDoesNotChooseARoleLimit()
    {
        var result = Evaluate(
            Security("strategic", "CAD", 1m, 100m, symbol: "MULTI", sector: "Technology", role: "Strategic"),
            Security("swing", "CAD", 1m, 50m, symbol: "MULTI", sector: "Technology", role: "Swing"),
            Security("other", "CAD", 1m, 150m, symbol: "OTHER", sector: "Financials"));

        var evidence = Pm029CadValuationPrototype.AllocationEvidence(
            result, "MULTI", 40m, "Technology", 48m, 2m);

        Assert.Equal(50m, evidence.PositionPct);
        Assert.True(evidence.RolePolicyAmbiguous);
        Assert.Null(evidence.PositionLimitExceeded);
        Assert.Equal(50m, evidence.SectorPct);
        Assert.False(evidence.SectorOverTarget);
    }

    private static Pm029CadValuationResult Evaluate(
        params Pm029ValuationComponentInput[] components) =>
        Evaluate(components, null, null, null, null, null, null);

    private static Pm029CadValuationResult Evaluate(
        Pm029ValuationComponentInput component,
        DateTimeOffset? asOf = null,
        DateOnly? valuationSession = null,
        IReadOnlyList<Pm029FxObservation>? fx = null,
        IReadOnlyList<Pm029FxPublicationSession>? publicationSessions = null,
        int maximumFxPublicationDays = 3,
        string? accountScope = null) =>
        Evaluate([component], asOf, valuationSession, fx, publicationSessions, maximumFxPublicationDays, accountScope);

    private static Pm029CadValuationResult Evaluate(
        IReadOnlyList<Pm029ValuationComponentInput> components,
        DateTimeOffset? asOf = null,
        DateOnly? valuationSession = null,
        IReadOnlyList<Pm029FxObservation>? fx = null,
        IReadOnlyList<Pm029FxPublicationSession>? publicationSessions = null,
        int? maximumFxPublicationDays = null,
        string? accountScope = null)
    {
        var request = new Pm029CadValuationRequest(
            asOf ?? SaturdayAsOf,
            valuationSession ?? Friday,
            "CAD",
            "current-user",
            accountScope ?? "Consolidated",
            maximumFxPublicationDays ?? 3,
            components,
            fx ?? [],
            publicationSessions ?? [Publication(Friday, 21, 30)]);
        return Pm029CadValuationPrototype.Evaluate(request);
    }

    private static Pm029ValuationComponentInput Security(
        string id,
        string currency,
        decimal shares,
        decimal? price,
        string? symbol = null,
        string? sector = "Technology",
        string? role = "Strategic",
        string? accountType = "Account A",
        bool isShared = false,
        DateOnly? session = null,
        DateOnly? quoteSession = null,
        string? ownerId = "current-user")
    {
        var quoteDate = quoteSession ?? session ?? Friday;
        var close = Utc(quoteDate.Year, quoteDate.Month, quoteDate.Day, 21, 0);
        return new Pm029ValuationComponentInput(
            id, isShared ? null : ownerId, Pm029ValuationComponentKind.Security,
            symbol ?? id, sector, role, accountType,
            currency, true, shares, null, null, price, null, false,
            quoteDate, session ?? Friday, close.AddMinutes(1), close, "Market data provider", null, null);
    }

    private static Pm029ValuationComponentInput Cash(
        string id,
        decimal balance,
        string currency,
        string accountType,
        bool isShared = false) =>
        new(id, isShared ? null : "current-user", Pm029ValuationComponentKind.Cash, null, null, null, accountType,
            currency, true, null, balance, null, null, null, false,
            null, null, null, null, null, SaturdayAsOf.AddMinutes(-1), "Cash ledger");

    private static Pm029ValuationComponentInput Option(
        string id,
        string currency,
        decimal signedContracts,
        decimal price,
        decimal multiplier) =>
        new(id, "current-user", Pm029ValuationComponentKind.Option, "UNDERLYING", null, "Options", "Account A",
            currency, true, null, null, signedContracts, price, multiplier, true,
            Friday, Friday, Utc(2026, 11, 13, 21, 1), Utc(2026, 11, 13, 21, 0),
            "Option quote provider", null, null);

    private static Pm029FxObservation Fx(
        DateOnly date,
        decimal rate,
        DateTimeOffset publishedAt,
        DateTimeOffset? retrievedAt = null) =>
        new("Bank of Canada", "FXUSDCAD", "USD", "CAD", date, publishedAt, rate, retrievedAt);

    private static Pm029FxPublicationSession Publication(DateOnly date, int hour, int minute) =>
        new(date, Utc(date.Year, date.Month, date.Day, hour, minute));

    private static DateTimeOffset Utc(int year, int month, int day, int hour, int minute) =>
        new(year, month, day, hour, minute, 0, TimeSpan.Zero);
}
