namespace PortfolioManager.Tests.Phase12;

internal enum Pm029ValuationComponentKind
{
    Security,
    Cash,
    Option
}

internal enum Pm029ValuationIssue
{
    NoComponents,
    UnsupportedBaseCurrency,
    CurrencyEvidenceMissing,
    UnsupportedCurrency,
    QuoteProvenanceMissing,
    QuoteSessionMismatch,
    QuoteBeforeSessionClose,
    QuoteNotAvailableAsOf,
    CashBalanceEvidenceMissing,
    CashBalanceNotAvailableAsOf,
    OptionTermsUnverified,
    FxUnavailable,
    FxPublicationUnknown,
    FxNotPublishedAsOf,
    FxObservationAfterSession,
    FxInvalidRate,
    FxStale,
    NonPositiveDenominator
}

internal enum Pm029ValuationEvidenceQuality
{
    Verified,
    NativeOnly,
    Unavailable
}

internal sealed record Pm029FxObservation(
    string Source,
    string Series,
    string BaseCurrency,
    string QuoteCurrency,
    DateOnly ObservationDate,
    DateTimeOffset? PublishedAtUtc,
    decimal? Rate,
    DateTimeOffset? RetrievedAtUtc);

internal sealed record Pm029FxPublicationSession(
    DateOnly Date,
    DateTimeOffset ExpectedPublicationAtUtc);

internal sealed record Pm029ValuationComponentInput(
    string Id,
    string? OwnerId,
    Pm029ValuationComponentKind Kind,
    string? Symbol,
    string? Sector,
    string? InvestmentRole,
    string? AccountType,
    string? Currency,
    bool CurrencyVerified,
    decimal? Shares,
    decimal? CashBalance,
    decimal? SignedContracts,
    decimal? MarketPrice,
    decimal? ContractMultiplier,
    bool OptionTermsVerified,
    DateOnly? QuoteSessionDate,
    DateOnly? ExpectedQuoteSessionDate,
    DateTimeOffset? QuoteSourceTimestampUtc,
    DateTimeOffset? ExpectedSessionCloseUtc,
    string? QuoteSource,
    DateTimeOffset? LedgerAsOfUtc,
    string? LedgerSource);

internal sealed record Pm029CadValuationRequest(
    DateTimeOffset AsOfUtc,
    DateOnly ValuationSessionDate,
    string BaseCurrency,
    string RequestingUserId,
    string AccountScope,
    int MaximumFxPublicationDays,
    IReadOnlyList<Pm029ValuationComponentInput> Components,
    IReadOnlyList<Pm029FxObservation> FxObservations,
    IReadOnlyList<Pm029FxPublicationSession> FxPublicationSessions);

internal sealed record Pm029ValuationComponentResult(
    string Id,
    Pm029ValuationComponentKind Kind,
    string? Symbol,
    string? Sector,
    string? InvestmentRole,
    string? AccountType,
    bool IsShared,
    string? NativeCurrency,
    decimal? NativeMarketValue,
    decimal? MarketValueCad,
    Pm029ValuationEvidenceQuality EvidenceQuality,
    Pm029ValuationIssue? UnavailableReason,
    string? QuoteSource,
    DateTimeOffset? QuoteSourceTimestampUtc,
    DateOnly? QuoteSessionDate,
    string? FxSource,
    string? FxSeries,
    DateOnly? FxObservationDate,
    DateTimeOffset? FxPublishedAtUtc,
    DateTimeOffset? FxRetrievedAtUtc,
    decimal? FxRateCadPerUsd);

internal sealed record Pm029CadValuationResult(
    DateTimeOffset AsOfUtc,
    DateOnly ValuationSessionDate,
    string BaseCurrency,
    string AccountScope,
    int ProvisionalMaximumFxPublicationDays,
    bool Complete,
    decimal? DenominatorCad,
    decimal? StockTotalCad,
    IReadOnlyList<Pm029ValuationComponentResult> Components,
    IReadOnlyList<Pm029ValuationIssue> ReasonCodes);

internal sealed record Pm029AllocationEvidence(
    bool Reliable,
    decimal? PositionPct,
    decimal? PositionLimitPct,
    bool? PositionLimitExceeded,
    bool RolePolicyAmbiguous,
    decimal? SectorPct,
    decimal? SectorTargetPct,
    bool? SectorOverTarget);

internal static class Pm029CadValuationPrototype
{
    public static Pm029CadValuationResult Evaluate(Pm029CadValuationRequest request)
    {
        if (request.MaximumFxPublicationDays < 0)
            throw new ArgumentOutOfRangeException(nameof(request), "The provisional FX age cannot be negative.");

        if (!string.Equals(request.BaseCurrency, "CAD", StringComparison.OrdinalIgnoreCase))
            return Incomplete(request, [], [Pm029ValuationIssue.UnsupportedBaseCurrency]);

        var visible = request.Components
            .Where(c => c.OwnerId is null || string.Equals(c.OwnerId, request.RequestingUserId, StringComparison.Ordinal))
            .ToList();
        var selected = string.Equals(request.AccountScope, "Consolidated", StringComparison.OrdinalIgnoreCase)
            ? visible
            : visible.Where(c => string.Equals(c.AccountType, request.AccountScope, StringComparison.OrdinalIgnoreCase)).ToList();

        if (selected.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() != selected.Count)
            throw new ArgumentException("Component identifiers must be unique within the requested account scope.", nameof(request));

        if (selected.Count == 0)
            return Incomplete(request, [], [Pm029ValuationIssue.NoComponents]);

        var results = selected.Select(component => ValueComponent(request, component)).ToList();
        var reasons = results.Where(c => c.UnavailableReason.HasValue)
            .Select(c => c.UnavailableReason!.Value)
            .Distinct()
            .ToList();
        if (reasons.Count > 0)
            return Incomplete(request, results, reasons);

        var denominator = results.Sum(c => c.MarketValueCad!.Value);
        if (denominator <= 0m)
            return Incomplete(request, results, [Pm029ValuationIssue.NonPositiveDenominator]);

        var stockTotal = results.Where(c => c.Kind == Pm029ValuationComponentKind.Security)
            .Sum(c => c.MarketValueCad!.Value);

        return new Pm029CadValuationResult(
            request.AsOfUtc,
            request.ValuationSessionDate,
            request.BaseCurrency,
            request.AccountScope,
            request.MaximumFxPublicationDays,
            true,
            denominator,
            stockTotal,
            results,
            []);
    }

    public static Pm029AllocationEvidence AllocationEvidence(
        Pm029CadValuationResult valuation,
        string symbol,
        decimal? rolePositionLimitPct,
        string? sector,
        decimal? sectorTargetPct,
        decimal sectorTolerancePp)
    {
        if (!valuation.Complete || valuation.DenominatorCad is not > 0m)
            return new(false, null, rolePositionLimitPct, null, false, null, sectorTargetPct, null);

        var symbolRows = valuation.Components
            .Where(c => c.Kind == Pm029ValuationComponentKind.Security
                && string.Equals(c.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var roles = symbolRows.Select(c => c.InvestmentRole ?? "Strategic")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var ambiguous = roles.Count > 1;
        var positionPct = symbolRows.Sum(c => c.MarketValueCad!.Value) / valuation.DenominatorCad.Value * 100m;
        bool? positionLimitExceeded = !ambiguous && rolePositionLimitPct.HasValue
            ? positionPct > rolePositionLimitPct.Value
            : null;

        decimal? sectorPct = null;
        if (!string.IsNullOrWhiteSpace(sector) && valuation.StockTotalCad is > 0m)
        {
            var sectorValue = valuation.Components
                .Where(c => c.Kind == Pm029ValuationComponentKind.Security
                    && string.Equals(c.Sector, sector, StringComparison.OrdinalIgnoreCase))
                .Sum(c => c.MarketValueCad!.Value);
            sectorPct = sectorValue / valuation.StockTotalCad.Value * 100m;
        }

        bool? sectorOverTarget = sectorPct.HasValue && sectorTargetPct.HasValue
            ? sectorPct.Value > sectorTargetPct.Value + sectorTolerancePp
            : null;

        return new(true, positionPct, rolePositionLimitPct, positionLimitExceeded, ambiguous,
            sectorPct, sectorTargetPct, sectorOverTarget);
    }

    private static Pm029ValuationComponentResult ValueComponent(
        Pm029CadValuationRequest request,
        Pm029ValuationComponentInput component)
    {
        var currency = NormalizeCurrency(component.Currency);
        if (!component.CurrencyVerified || currency is null)
            return Unavailable(component, currency, null, Pm029ValuationIssue.CurrencyEvidenceMissing);

        var issue = component.Kind switch
        {
            Pm029ValuationComponentKind.Security => SecurityIssue(request, component),
            Pm029ValuationComponentKind.Cash => CashIssue(request, component),
            Pm029ValuationComponentKind.Option => OptionIssue(request, component),
            _ => Pm029ValuationIssue.UnsupportedCurrency
        };
        if (issue.HasValue)
            return Unavailable(component, currency, null, issue.Value);

        var nativeValue = component.Kind switch
        {
            Pm029ValuationComponentKind.Security => component.Shares!.Value * component.MarketPrice!.Value,
            Pm029ValuationComponentKind.Cash => component.CashBalance!.Value,
            Pm029ValuationComponentKind.Option => component.MarketPrice!.Value
                * component.SignedContracts!.Value
                * component.ContractMultiplier!.Value,
            _ => throw new ArgumentOutOfRangeException(nameof(component))
        };

        if (currency == "CAD")
            return Verified(component, currency, nativeValue, nativeValue, null);

        if (currency != "USD")
            return Unavailable(component, currency, nativeValue, Pm029ValuationIssue.UnsupportedCurrency);

        var fxSessionBound = component.Kind == Pm029ValuationComponentKind.Cash
            ? request.ValuationSessionDate
            : component.QuoteSessionDate!.Value;
        var fx = ResolveFx(request, fxSessionBound);
        if (fx.Issue is { } fxIssue)
            return Unavailable(component, currency, nativeValue, fxIssue);

        return Verified(component, currency, nativeValue, nativeValue * fx.Observation!.Rate!.Value, fx.Observation);
    }

    private static Pm029ValuationIssue? SecurityIssue(
        Pm029CadValuationRequest request,
        Pm029ValuationComponentInput component) =>
        component.Shares is null
            ? Pm029ValuationIssue.QuoteProvenanceMissing
            : QuoteIssue(request, component);

    private static Pm029ValuationIssue? QuoteIssue(
        Pm029CadValuationRequest request,
        Pm029ValuationComponentInput component)
    {
        if (component.MarketPrice is not > 0m
            || string.IsNullOrWhiteSpace(component.QuoteSource)
            || component.QuoteSourceTimestampUtc is null
            || component.QuoteSessionDate is null
            || component.ExpectedQuoteSessionDate is null
            || component.ExpectedSessionCloseUtc is null)
            return Pm029ValuationIssue.QuoteProvenanceMissing;
        if (component.QuoteSessionDate != component.ExpectedQuoteSessionDate)
            return Pm029ValuationIssue.QuoteSessionMismatch;
        if (component.QuoteSourceTimestampUtc > request.AsOfUtc)
            return Pm029ValuationIssue.QuoteNotAvailableAsOf;
        if (component.QuoteSourceTimestampUtc < component.ExpectedSessionCloseUtc)
            return Pm029ValuationIssue.QuoteBeforeSessionClose;
        return null;
    }

    private static Pm029ValuationIssue? CashIssue(
        Pm029CadValuationRequest request,
        Pm029ValuationComponentInput component)
    {
        if (component.CashBalance is null || component.LedgerAsOfUtc is null || string.IsNullOrWhiteSpace(component.LedgerSource))
            return Pm029ValuationIssue.CashBalanceEvidenceMissing;
        return component.LedgerAsOfUtc > request.AsOfUtc
            ? Pm029ValuationIssue.CashBalanceNotAvailableAsOf
            : null;
    }

    private static Pm029ValuationIssue? OptionIssue(
        Pm029CadValuationRequest request,
        Pm029ValuationComponentInput component)
    {
        if (!component.OptionTermsVerified
            || component.SignedContracts is null
            || component.ContractMultiplier is not > 0m)
            return Pm029ValuationIssue.OptionTermsUnverified;
        return QuoteIssue(request, component);
    }

    private static (Pm029FxObservation? Observation, Pm029ValuationIssue? Issue) ResolveFx(
        Pm029CadValuationRequest request,
        DateOnly componentSession)
    {
        var observations = request.FxObservations
            .Where(o => string.Equals(o.Source, "Bank of Canada", StringComparison.OrdinalIgnoreCase)
                && string.Equals(o.Series, "FXUSDCAD", StringComparison.OrdinalIgnoreCase)
                && string.Equals(o.BaseCurrency, "USD", StringComparison.OrdinalIgnoreCase)
                && string.Equals(o.QuoteCurrency, "CAD", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (observations.Count == 0)
            return (null, Pm029ValuationIssue.FxUnavailable);

        var onOrBeforeSession = observations.Where(o => o.ObservationDate <= componentSession
            && o.ObservationDate <= request.ValuationSessionDate).ToList();
        if (onOrBeforeSession.Count == 0)
            return (null, Pm029ValuationIssue.FxObservationAfterSession);

        var publishedByAsOf = onOrBeforeSession
            .Where(o => o.PublishedAtUtc is { } published && published <= request.AsOfUtc)
            .ToList();
        if (publishedByAsOf.Count == 0)
        {
            var hasPublicationTime = onOrBeforeSession.Any(o => o.PublishedAtUtc.HasValue);
            return (null, hasPublicationTime
                ? Pm029ValuationIssue.FxNotPublishedAsOf
                : Pm029ValuationIssue.FxPublicationUnknown);
        }

        var validRates = publishedByAsOf.Where(o => o.Rate is > 0m).ToList();
        if (validRates.Count == 0)
            return (null, Pm029ValuationIssue.FxInvalidRate);

        var latest = validRates.OrderByDescending(o => o.ObservationDate)
            .ThenByDescending(o => o.PublishedAtUtc)
            .First();
        var age = request.FxPublicationSessions.Count(s =>
            s.Date > latest.ObservationDate
            && s.Date <= request.ValuationSessionDate
            && s.ExpectedPublicationAtUtc <= request.AsOfUtc);
        return age > request.MaximumFxPublicationDays
            ? (null, Pm029ValuationIssue.FxStale)
            : (latest, null);
    }

    private static Pm029CadValuationResult Incomplete(
        Pm029CadValuationRequest request,
        IReadOnlyList<Pm029ValuationComponentResult> components,
        IReadOnlyList<Pm029ValuationIssue> reasons) =>
        new(request.AsOfUtc, request.ValuationSessionDate, request.BaseCurrency, request.AccountScope,
            request.MaximumFxPublicationDays, false, null, null, components, reasons);

    private static Pm029ValuationComponentResult Unavailable(
        Pm029ValuationComponentInput component,
        string? currency,
        decimal? nativeValue,
        Pm029ValuationIssue issue) =>
        new(component.Id, component.Kind, component.Symbol, component.Sector, component.InvestmentRole,
            component.AccountType, component.OwnerId is null, currency, nativeValue, null,
            nativeValue.HasValue ? Pm029ValuationEvidenceQuality.NativeOnly : Pm029ValuationEvidenceQuality.Unavailable,
            issue, component.QuoteSource, component.QuoteSourceTimestampUtc, component.QuoteSessionDate,
            null, null, null, null, null, null);

    private static Pm029ValuationComponentResult Verified(
        Pm029ValuationComponentInput component,
        string currency,
        decimal nativeValue,
        decimal cadValue,
        Pm029FxObservation? fx) =>
        new(component.Id, component.Kind, component.Symbol, component.Sector, component.InvestmentRole,
            component.AccountType, component.OwnerId is null, currency, nativeValue, cadValue,
            Pm029ValuationEvidenceQuality.Verified, null, component.QuoteSource,
            component.QuoteSourceTimestampUtc, component.QuoteSessionDate, fx?.Source, fx?.Series,
            fx?.ObservationDate, fx?.PublishedAtUtc, fx?.RetrievedAtUtc, fx?.Rate);

    private static string? NormalizeCurrency(string? currency) =>
        string.IsNullOrWhiteSpace(currency) ? null : currency.Trim().ToUpperInvariant();
}
