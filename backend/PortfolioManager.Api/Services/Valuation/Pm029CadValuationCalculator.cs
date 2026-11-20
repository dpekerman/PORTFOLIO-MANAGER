using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services.Valuation;

public sealed class Pm029CadValuationCalculator(IFxPublicationCalendar publicationCalendar) : IPm029CadValuationCalculator
{
    public Pm029CadValuationResult Evaluate(Pm029ValuationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Components);
        ArgumentNullException.ThrowIfNull(request.FxObservations);
        if (request.AsOfUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("AsOfUtc must be expressed in UTC.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.RequestingUserId))
            throw new ArgumentException("RequestingUserId is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.AccountScope))
            throw new ArgumentException("AccountScope is required.", nameof(request));
        if (request.MaximumFxPublicationSessions < 0)
            throw new ArgumentOutOfRangeException(nameof(request), "The provisional FX age cannot be negative.");

        var visible = request.Components
            .Where(component => component.OwnerId is null
                || string.Equals(component.OwnerId, request.RequestingUserId, StringComparison.Ordinal))
            .ToList();
        var selected = string.Equals(request.AccountScope, Pm029ValuationScopes.Consolidated, StringComparison.OrdinalIgnoreCase)
            ? visible
            : visible.Where(component => string.Equals(
                component.AccountType, request.AccountScope, StringComparison.OrdinalIgnoreCase)).ToList();

        if (selected.Any(component => string.IsNullOrWhiteSpace(component.Id)))
            throw new ArgumentException("Every selected valuation component requires a stable identifier.", nameof(request));
        if (selected.Select(component => component.Id).Distinct(StringComparer.Ordinal).Count() != selected.Count)
            throw new ArgumentException("Component identifiers must be unique within the requested scope.", nameof(request));
        if (selected.Count == 0)
            return Incomplete(request, [], [Pm029ValuationIssueCodes.NoComponents]);

        var duplicateTradeLinks = selected.OfType<Pm029CashBalanceValuationInput>()
            .SelectMany(cash => (cash.LinkedTradeKeys ?? []).Where(key => !string.IsNullOrWhiteSpace(key))
                .Select(key => key.Trim()))
            .GroupBy(key => key, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var results = selected.Select(component => ValueComponent(
            request, component, duplicateTradeLinks)).ToList();
        var issues = results.Where(component => component.IssueCode is not null)
            .Select(component => component.IssueCode!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (issues.Count > 0)
            return Incomplete(request, results, issues);

        var denominator = results.Sum(component => component.ValueCad!.Value);
        if (denominator <= 0m)
            return Incomplete(request, results, [Pm029ValuationIssueCodes.NonPositiveDenominator]);

        var stockTotal = results.Where(component => component.Kind == "Security")
            .Sum(component => component.ValueCad!.Value);
        return new Pm029CadValuationResult(
            request.AsOfUtc,
            request.ValuationSessionDate,
            request.AccountScope,
            request.MaximumFxPublicationSessions,
            true,
            denominator,
            stockTotal,
            results,
            []);
    }

    private Pm029ValuationComponentResult ValueComponent(
        Pm029ValuationRequest request,
        Pm029ValuationComponentInput input,
        IReadOnlySet<string> duplicateTradeLinks)
    {
        var asOfUtc = request.AsOfUtc;
        if (input is not Pm029SecurityValuationInput
            and not Pm029CashBalanceValuationInput
            and not Pm029OptionValuationInput)
            return Unavailable(input, null, Pm029ValuationIssueCodes.UnsupportedComponent);

        var currency = input switch
        {
            Pm029SecurityValuationInput security => security.ListingCurrency,
            Pm029CashBalanceValuationInput cash => cash.Currency,
            Pm029OptionValuationInput option => option.ContractCurrency,
            _ => null
        };
        var evidenceIssue = CurrencyIssue(currency, asOfUtc);
        if (evidenceIssue is not null)
            return Unavailable(input, currency?.CurrencyCode, evidenceIssue);

        var currencyCode = NormalizeCurrency(currency!.CurrencyCode)!;
        var nativeValue = input switch
        {
            Pm029SecurityValuationInput security => security.Quantity is > 0m && security.QuotePrice is > 0m
                ? security.Quantity.Value * security.QuotePrice.Value
                : null,
            Pm029CashBalanceValuationInput cash => cash.Balance,
            Pm029OptionValuationInput option => option.QuotePrice is > 0m
                && option.SignedContracts is not null
                && option.ContractMultiplier is > 0m
                ? option.QuotePrice.Value * option.SignedContracts.Value * option.ContractMultiplier.Value
                : null,
            _ => null
        };

        var issue = input switch
        {
            Pm029SecurityValuationInput security => SecurityIssue(security, asOfUtc),
            Pm029CashBalanceValuationInput cash => CashIssue(cash, request, duplicateTradeLinks),
            Pm029OptionValuationInput option => OptionIssue(option, asOfUtc),
            _ => Pm029ValuationIssueCodes.UnsupportedComponent
        };
        if (issue is not null)
            return Unavailable(input, currencyCode, issue, nativeValue);

        if (currencyCode == "CAD")
            return Verified(input, currencyCode, nativeValue!.Value, nativeValue.Value, null);
        if (currencyCode != "USD")
            return Unavailable(input, currencyCode, Pm029ValuationIssueCodes.UnsupportedCurrency, nativeValue);

        var componentSession = input switch
        {
            Pm029SecurityValuationInput security => security.QuoteSession!.Value,
            Pm029OptionValuationInput option => option.QuoteSession!.Value,
            _ => request.ValuationSessionDate
        };
        var (fx, fxIssue) = ResolveFx(request, componentSession);
        if (fxIssue is not null)
            return Unavailable(input, currencyCode, fxIssue, nativeValue);

        return Verified(input, currencyCode, nativeValue!.Value,
            nativeValue.Value * fx!.Rate, fx);
    }

    private static string? CurrencyIssue(CurrencyEvidence? evidence, DateTimeOffset asOfUtc)
    {
        if (evidence is null || !evidence.IsVerified
            || string.IsNullOrWhiteSpace(evidence.CurrencyCode)
            || string.IsNullOrWhiteSpace(evidence.Source)
            || evidence.VerifiedAtUtc is null || evidence.VerifiedAtUtc == default(DateTimeOffset))
            return Pm029ValuationIssueCodes.CurrencyEvidenceMissing;
        return evidence.VerifiedAtUtc.Value.ToUniversalTime() > asOfUtc
            ? Pm029ValuationIssueCodes.CurrencyEvidenceNotAvailableAsOf
            : null;
    }

    private static string? SecurityIssue(Pm029SecurityValuationInput input, DateTimeOffset asOfUtc)
    {
        if (input.Quantity is not > 0m)
            return Pm029ValuationIssueCodes.InvalidQuantity;
        if (!string.Equals(NormalizeCurrency(input.ListingCurrency!.CurrencyCode),
                NormalizeCurrency(input.QuoteCurrency), StringComparison.Ordinal))
            return Pm029ValuationIssueCodes.QuoteCurrencyMismatch;
        return QuoteIssue(input.QuotePrice, input.QuoteSource, input.QuoteAsOfUtc,
            input.QuoteSession, input.ExpectedSession, input.ExpectedSessionCloseUtc, asOfUtc);
    }

    private static string? OptionIssue(Pm029OptionValuationInput input, DateTimeOffset asOfUtc)
    {
        if (!input.TermsVerified || string.IsNullOrWhiteSpace(input.TermsSource)
            || input.TermsVerifiedAtUtc is null
            || input.TermsVerifiedAtUtc.Value.ToUniversalTime() > asOfUtc
            || input.SignedContracts is null
            || input.SignedContracts == 0
            || input.ContractMultiplier is not > 0m)
            return Pm029ValuationIssueCodes.OptionTermsUnverified;
        if (!string.Equals(NormalizeCurrency(input.ContractCurrency!.CurrencyCode),
                NormalizeCurrency(input.QuoteCurrency), StringComparison.Ordinal))
            return Pm029ValuationIssueCodes.QuoteCurrencyMismatch;
        return QuoteIssue(input.QuotePrice, input.QuoteSource, input.QuoteAsOfUtc,
            input.QuoteSession, input.ExpectedSession, input.ExpectedSessionCloseUtc, asOfUtc);
    }

    private static string? QuoteIssue(
        decimal? price,
        string? source,
        DateTimeOffset? quoteAsOfUtc,
        DateOnly? quoteSession,
        DateOnly? expectedSession,
        DateTimeOffset? expectedSessionCloseUtc,
        DateTimeOffset asOfUtc)
    {
        if (price is not > 0m || string.IsNullOrWhiteSpace(source)
            || quoteAsOfUtc is null || quoteSession is null || expectedSession is null
            || expectedSessionCloseUtc is null)
            return Pm029ValuationIssueCodes.QuoteProvenanceMissing;
        if (quoteSession != expectedSession)
            return Pm029ValuationIssueCodes.QuoteSessionMismatch;
        if (quoteAsOfUtc.Value.ToUniversalTime() > asOfUtc)
            return Pm029ValuationIssueCodes.QuoteNotAvailableAsOf;
        if (quoteAsOfUtc.Value.ToUniversalTime() < expectedSessionCloseUtc.Value.ToUniversalTime())
            return Pm029ValuationIssueCodes.QuoteBeforeSessionClose;
        return null;
    }

    private static string? CashIssue(
        Pm029CashBalanceValuationInput input,
        Pm029ValuationRequest request,
        IReadOnlySet<string> duplicateTradeLinks)
    {
        if ((input.LinkedTradeKeys ?? []).Any(key => !string.IsNullOrWhiteSpace(key)
                && duplicateTradeLinks.Contains(key.Trim())))
            return Pm029ValuationIssueCodes.CashTradeLinkDuplicated;
        if (input.Balance is null || input.BalanceAsOfUtc is null || string.IsNullOrWhiteSpace(input.LedgerSource))
            return Pm029ValuationIssueCodes.CashLedgerEvidenceMissing;
        if (input.BalanceAsOfUtc.Value.ToUniversalTime() > request.AsOfUtc)
            return Pm029ValuationIssueCodes.CashLedgerNotAvailableAsOf;
        return input.CompleteThroughSession != request.ValuationSessionDate
            ? Pm029ValuationIssueCodes.CashLedgerIncomplete
            : null;
    }

    private (Pm029FxObservation? Observation, string? Issue) ResolveFx(
        Pm029ValuationRequest request,
        DateOnly componentSession)
    {
        var sourceObservations = request.FxObservations.Where(observation =>
            string.Equals(observation.Source, "Bank of Canada", StringComparison.OrdinalIgnoreCase)
            && string.Equals(observation.Series, "FXUSDCAD", StringComparison.OrdinalIgnoreCase)
            && string.Equals(observation.BaseCurrency, "USD", StringComparison.OrdinalIgnoreCase)
            && string.Equals(observation.QuoteCurrency, "CAD", StringComparison.OrdinalIgnoreCase)).ToList();
        if (sourceObservations.Count == 0)
            return (null, Pm029ValuationIssueCodes.FxUnavailable);

        var onOrBeforeSession = sourceObservations.Where(observation =>
            observation.ObservationDate <= componentSession
            && observation.ObservationDate <= request.ValuationSessionDate).ToList();
        if (onOrBeforeSession.Count == 0)
            return (null, Pm029ValuationIssueCodes.FxObservationAfterSession);
        if (onOrBeforeSession.GroupBy(observation => observation.ObservationDate).Any(group => group.Count() > 1))
            return (null, Pm029ValuationIssueCodes.FxObservationDuplicated);

        if (onOrBeforeSession.Any(observation =>
                observation.DataQuality is not ("ObservedAtRetrieval" or "VerifiedAvailability")
                || observation.RetrievedAtUtc == default
                || observation.FirstObservedAtUtc == default(DateTimeOffset)
                || observation.VerifiedAvailableAtUtc == default(DateTimeOffset)
                || observation.DataQuality == "VerifiedAvailability"
                    && observation.VerifiedAvailableAtUtc is null
                || observation.VerifiedAvailableAtUtc.HasValue
                    && (string.IsNullOrWhiteSpace(observation.AvailabilitySource)
                        || observation.VerifiedAvailableAtUtc > observation.RetrievedAtUtc)
                || observation.FirstObservedAtUtc > observation.RetrievedAtUtc))
            return (null, Pm029ValuationIssueCodes.FxAvailabilityUnverified);

        var availableAsOf = onOrBeforeSession
            .Where(observation => (observation.VerifiedAvailableAtUtc
                ?? observation.FirstObservedAtUtc ?? observation.RetrievedAtUtc) <= request.AsOfUtc)
            .ToList();
        if (availableAsOf.Count == 0)
            return (null, Pm029ValuationIssueCodes.FxNotAvailableAsOf);

        var latest = availableAsOf.OrderByDescending(observation => observation.ObservationDate).First();
        if (latest.Rate <= 0m)
            return (null, Pm029ValuationIssueCodes.FxInvalidRate);
        if (!publicationCalendar.Covers(latest.ObservationDate, request.ValuationSessionDate))
            return (null, Pm029ValuationIssueCodes.FxCalendarCoverageMissing);

        var days = publicationCalendar.GetDays(latest.ObservationDate, request.ValuationSessionDate);
        var observationDay = days.Single(day => day.Date == latest.ObservationDate);
        if (!observationDay.IsPublicationSession)
            return (null, Pm029ValuationIssueCodes.FxCalendarMismatch);

        var age = days.Count(day => day.Date > latest.ObservationDate
            && day.IsPublicationSession
            && day.ExpectedPublicationAtUtc!.Value.ToUniversalTime() <= request.AsOfUtc);
        return age > request.MaximumFxPublicationSessions
            ? (null, Pm029ValuationIssueCodes.FxStale)
            : (latest, null);
    }

    private static Pm029ValuationComponentResult Unavailable(
        Pm029ValuationComponentInput input,
        string? currency,
        string issue,
        decimal? nativeValue = null)
    {
        var (quoteSource, quoteAsOf, quoteSession) = QuoteProvenance(input);
        return new Pm029ValuationComponentResult(
            input, input.Id, Kind(input), input.Symbol, input.Sector, input.InvestmentRole, input.AccountType, input.OwnerId is null,
            NormalizeCurrency(currency), nativeValue, null, issue, quoteSource, quoteAsOf, quoteSession,
            null, null, null, null);
    }

    private static Pm029ValuationComponentResult Verified(
        Pm029ValuationComponentInput input,
        string currency,
        decimal nativeValue,
        decimal cadValue,
        Pm029FxObservation? fx)
    {
        var (quoteSource, quoteAsOf, quoteSession) = QuoteProvenance(input);
        return new Pm029ValuationComponentResult(
            input, input.Id, Kind(input), input.Symbol, input.Sector, input.InvestmentRole, input.AccountType, input.OwnerId is null,
            currency, nativeValue, cadValue, null, quoteSource, quoteAsOf, quoteSession,
            fx?.Series, fx?.ObservationDate, fx?.RetrievedAtUtc, fx?.Rate, fx);
    }

    private static (string? Source, DateTimeOffset? AsOfUtc, DateOnly? Session) QuoteProvenance(
        Pm029ValuationComponentInput input) => input switch
    {
        Pm029SecurityValuationInput security => (security.QuoteSource, security.QuoteAsOfUtc, security.QuoteSession),
        Pm029OptionValuationInput option => (option.QuoteSource, option.QuoteAsOfUtc, option.QuoteSession),
        _ => (null, null, null)
    };

    private static string Kind(Pm029ValuationComponentInput input) => input switch
    {
        Pm029SecurityValuationInput => "Security",
        Pm029CashBalanceValuationInput => "Cash",
        Pm029OptionValuationInput => "Option",
        _ => "Unsupported"
    };

    private static Pm029CadValuationResult Incomplete(
        Pm029ValuationRequest request,
        IReadOnlyList<Pm029ValuationComponentResult> components,
        IReadOnlyList<string> issues) =>
        new(request.AsOfUtc, request.ValuationSessionDate, request.AccountScope,
            request.MaximumFxPublicationSessions, false, null, null, components, issues);

    private static string? NormalizeCurrency(string? currency) =>
        string.IsNullOrWhiteSpace(currency) ? null : currency.Trim().ToUpperInvariant();
}

public static class Pm029PortfolioFitEvidenceCalculator
{
    public static Pm029AllocationEvidence Evaluate(
        Pm029CadValuationResult valuation,
        string symbol,
        decimal? positionLimitPct,
        string? sector,
        decimal? sectorTargetPct,
        decimal sectorTolerancePp)
    {
        ArgumentNullException.ThrowIfNull(valuation);
        if (sectorTolerancePp < 0m)
            throw new ArgumentOutOfRangeException(nameof(sectorTolerancePp));
        if (!valuation.Complete || valuation.DenominatorCad is not > 0m)
            return new(false, null, positionLimitPct, null, false, null, sectorTargetPct, null);

        var symbolRows = valuation.Components.Where(component =>
            component.Kind == "Security"
            && string.Equals(component.Symbol, symbol, StringComparison.OrdinalIgnoreCase)).ToList();
        var roles = symbolRows.Select(component => component.InvestmentRole ?? "Strategic")
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var roleAmbiguous = roles.Count > 1;
        var positionPct = symbolRows.Sum(component => component.ValueCad!.Value)
            / valuation.DenominatorCad.Value * 100m;
        bool? positionExceeded = !roleAmbiguous && positionLimitPct.HasValue
            ? positionPct > positionLimitPct.Value
            : null;

        decimal? sectorPct = null;
        if (!string.IsNullOrWhiteSpace(sector) && valuation.StockOnlyCad is > 0m
            && valuation.Components.Where(component => component.Kind == "Security")
                .All(component => !string.IsNullOrWhiteSpace(component.Sector)))
        {
            var sectorValue = valuation.Components.Where(component =>
                    component.Kind == "Security"
                    && string.Equals(component.Sector, sector, StringComparison.OrdinalIgnoreCase))
                .Sum(component => component.ValueCad!.Value);
            sectorPct = sectorValue / valuation.StockOnlyCad.Value * 100m;
        }

        bool? sectorOverTarget = sectorPct.HasValue && sectorTargetPct.HasValue
            ? sectorPct.Value > sectorTargetPct.Value + sectorTolerancePp
            : null;
        return new(true, positionPct, positionLimitPct, positionExceeded, roleAmbiguous,
            sectorPct, sectorTargetPct, sectorOverTarget);
    }
}
