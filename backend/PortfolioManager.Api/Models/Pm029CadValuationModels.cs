using System.Text.Json.Serialization;

namespace PortfolioManager.Api.Models;

public static class Pm029ValuationScopes
{
    public const string Consolidated = "Consolidated";
}

public static class Pm029ValuationIssueCodes
{
    public const string NoComponents = "NoComponents";
    public const string CurrencyEvidenceMissing = "CurrencyEvidenceMissing";
    public const string CurrencyEvidenceNotAvailableAsOf = "CurrencyEvidenceNotAvailableAsOf";
    public const string UnsupportedCurrency = "UnsupportedCurrency";
    public const string QuoteProvenanceMissing = "QuoteProvenanceMissing";
    public const string QuoteCurrencyMismatch = "QuoteCurrencyMismatch";
    public const string QuoteSessionMismatch = "QuoteSessionMismatch";
    public const string QuoteNotAvailableAsOf = "QuoteNotAvailableAsOf";
    public const string QuoteBeforeSessionClose = "QuoteBeforeSessionClose";
    public const string InvalidQuantity = "InvalidQuantity";
    public const string CashLedgerEvidenceMissing = "CashLedgerEvidenceMissing";
    public const string CashLedgerNotAvailableAsOf = "CashLedgerNotAvailableAsOf";
    public const string CashLedgerIncomplete = "CashLedgerIncomplete";
    public const string CashTradeLinkDuplicated = "CashTradeLinkDuplicated";
    public const string OptionTermsUnverified = "OptionTermsUnverified";
    public const string FxUnavailable = "FxUnavailable";
    public const string FxObservationAfterSession = "FxObservationAfterSession";
    public const string FxObservationDuplicated = "FxObservationDuplicated";
    public const string FxNotAvailableAsOf = "FxNotAvailableAsOf";
    public const string FxCalendarCoverageMissing = "FxCalendarCoverageMissing";
    public const string FxCalendarMismatch = "FxCalendarMismatch";
    public const string FxInvalidRate = "FxInvalidRate";
    public const string FxStale = "FxStale";
    public const string FxAvailabilityUnverified = "FxAvailabilityUnverified";
    public const string NonPositiveDenominator = "NonPositiveDenominator";
    public const string UnsupportedComponent = "UnsupportedComponent";
}

public sealed record CurrencyEvidence(
    string? CurrencyCode,
    string? Source,
    bool IsVerified,
    DateTimeOffset? VerifiedAtUtc);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$kind")]
[JsonDerivedType(typeof(Pm029SecurityValuationInput), "Security")]
[JsonDerivedType(typeof(Pm029CashBalanceValuationInput), "Cash")]
[JsonDerivedType(typeof(Pm029OptionValuationInput), "Option")]
public abstract record Pm029ValuationComponentInput(
    string Id,
    string? OwnerId,
    string? AccountType,
    string? Symbol,
    string? Sector,
    string? InvestmentRole);

public sealed record Pm029SecurityValuationInput(
    string Id,
    string? OwnerId,
    string? AccountType,
    string? Symbol,
    string? Sector,
    string? InvestmentRole,
    decimal? Quantity,
    CurrencyEvidence? ListingCurrency,
    string? QuoteCurrency,
    decimal? QuotePrice,
    string? QuoteSource,
    DateTimeOffset? QuoteAsOfUtc,
    DateOnly? QuoteSession,
    DateOnly? ExpectedSession,
    DateTimeOffset? ExpectedSessionCloseUtc)
    : Pm029ValuationComponentInput(Id, OwnerId, AccountType, Symbol, Sector, InvestmentRole);

public sealed record Pm029CashBalanceValuationInput(
    string Id,
    string? OwnerId,
    string? AccountType,
    decimal? Balance,
    CurrencyEvidence? Currency,
    DateTimeOffset? BalanceAsOfUtc,
    DateOnly? CompleteThroughSession,
    string? LedgerSource,
    IReadOnlyList<string>? LinkedTradeKeys = null)
    : Pm029ValuationComponentInput(Id, OwnerId, AccountType, null, null, null);

public sealed record Pm029OptionValuationInput(
    string Id,
    string? OwnerId,
    string? AccountType,
    string? Symbol,
    string? InvestmentRole,
    CurrencyEvidence? ContractCurrency,
    string? QuoteCurrency,
    decimal? QuotePrice,
    string? QuoteSource,
    DateTimeOffset? QuoteAsOfUtc,
    DateOnly? QuoteSession,
    DateOnly? ExpectedSession,
    DateTimeOffset? ExpectedSessionCloseUtc,
    int? SignedContracts,
    decimal? ContractMultiplier,
    bool TermsVerified,
    string? TermsSource,
    DateTimeOffset? TermsVerifiedAtUtc)
    : Pm029ValuationComponentInput(Id, OwnerId, AccountType, Symbol, null, InvestmentRole);

public sealed record Pm029FxObservation(
    string Source,
    string Series,
    string BaseCurrency,
    string QuoteCurrency,
    DateOnly ObservationDate,
    decimal Rate,
    DateTimeOffset RetrievedAtUtc,
    DateTimeOffset? FirstObservedAtUtc = null,
    DateTimeOffset? VerifiedAvailableAtUtc = null,
    string? AvailabilitySource = null,
    string DataQuality = "ObservedAtRetrieval");

public sealed record Pm029ValuationRequest(
    DateTimeOffset AsOfUtc,
    DateOnly ValuationSessionDate,
    string RequestingUserId,
    string AccountScope,
    int MaximumFxPublicationSessions,
    IReadOnlyList<Pm029ValuationComponentInput> Components,
    IReadOnlyList<Pm029FxObservation> FxObservations);

public sealed record Pm029ValuationComponentResult(
    Pm029ValuationComponentInput Input,
    string Id,
    string Kind,
    string? Symbol,
    string? Sector,
    string? InvestmentRole,
    string? AccountType,
    bool IsShared,
    string? Currency,
    decimal? NativeValue,
    decimal? ValueCad,
    string? IssueCode,
    string? QuoteSource,
    DateTimeOffset? QuoteAsOfUtc,
    DateOnly? QuoteSession,
    string? FxSeries,
    DateOnly? FxObservationDate,
    DateTimeOffset? FxRetrievedAtUtc,
    decimal? FxRateCadPerUsd,
    Pm029FxObservation? SelectedFx = null);

public sealed record Pm029CadValuationResult(
    DateTimeOffset AsOfUtc,
    DateOnly ValuationSessionDate,
    string AccountScope,
    int ProvisionalMaximumFxPublicationSessions,
    bool Complete,
    decimal? DenominatorCad,
    decimal? StockOnlyCad,
    IReadOnlyList<Pm029ValuationComponentResult> Components,
    IReadOnlyList<string> IssueCodes);

public sealed record Pm029AllocationEvidence(
    bool Reliable,
    decimal? PositionPct,
    decimal? PositionLimitPct,
    bool? PositionLimitExceeded,
    bool RolePolicyAmbiguous,
    decimal? SectorPct,
    decimal? SectorTargetPct,
    bool? SectorOverTarget);
