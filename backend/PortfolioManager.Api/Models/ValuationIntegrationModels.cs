namespace PortfolioManager.Api.Models;

public sealed record CashLedgerEvidence(
    string Id, string? OwnerId, string? AccountId, string? AccountType,
    decimal SignedAmount, DateOnly EffectiveDate, DateTimeOffset RecordedAtUtc,
    string? CashFlowType, CurrencyEvidence? Currency,
    string? SourceType = null, int? SourceItemId = null,
    IReadOnlyList<CurrencyEvidence>? HistoricalCurrencyEvidence = null);

public sealed record CashLedgerPackage(
    DateTimeOffset AsOfUtc, DateOnly CompleteThroughSession, string Source,
    IReadOnlyList<CashLedgerEvidence> Entries);

public sealed record OptionPositionEvidence(
    string Id, string? OwnerId, string? AccountId, string? AccountType,
    string? ContractId, string? Symbol, string? InvestmentRole,
    string? ContractType, string? PositionSide, string? State,
    int? SignedOpenContracts, DateTimeOffset? PositionAsOfUtc,
    string? PositionSource, CurrencyEvidence? Currency,
    decimal? Multiplier, string? TermsSource, DateTimeOffset? TermsVerifiedAtUtc,
    string? QuoteCurrency, decimal? MarkPrice, string? QuoteSource,
    DateTimeOffset? QuoteAsOfUtc, DateOnly? QuoteSession, DateOnly? ExpectedSession,
    DateTimeOffset? ExpectedSessionCloseUtc);

public sealed record EvidenceIssue(string ComponentId, string Code);

public sealed record ValuationAdapterResult(
    IReadOnlyList<Pm029ValuationComponentInput> Components,
    IReadOnlyList<EvidenceIssue> Issues,
    IReadOnlyDictionary<string, decimal> ExternalFlowsByCurrency);

public sealed record ValuationProjectionPolicy(
    string Symbol, string? Sector, decimal? PositionLimitPct,
    decimal? SectorTargetPct, decimal SectorTolerancePp);

public sealed record ValuationExposureProjection(
    string Symbol, Pm029AllocationEvidence Evidence,
    decimal? PositionHeadroomPp, decimal? SectorDeviationPp);

public sealed record ShadowValuationRequest(
    string DatasetKind, DateTimeOffset AsOfUtc, DateOnly Session, string RequestingUserId,
    string AccountScope, int MaximumFxPublicationSessions,
    IReadOnlyList<Pm029SecurityValuationInput> Securities,
    CashLedgerPackage Cash,
    IReadOnlyList<OptionPositionEvidence> Options,
    IReadOnlyList<Pm029FxObservation> Fx,
    IReadOnlyList<ValuationProjectionPolicy> Policies,
    IReadOnlyList<LegacyValuationComponent> Legacy);

public sealed record LegacyValuationComponent(
    string Id, string Kind, decimal Value, string? Currency,
    bool UsedCostFallback = false);

public sealed record ShadowValuationDifference(
    string ComponentId, decimal? LegacyValue, decimal? CadValue,
    string Classification);

public sealed record SharedValuationSnapshot(
    ShadowValuationRequest Input,
    Pm029CadValuationResult Valuation,
    IReadOnlyList<ValuationExposureProjection> Exposures,
    IReadOnlyList<EvidenceIssue> AdapterIssues,
    IReadOnlyDictionary<string, decimal> ExternalFlowsByCurrency,
    IReadOnlyList<ShadowValuationDifference> Differences,
    decimal LegacyRawDenominator,
    bool ProductionActivationAllowed = false);

public sealed record ValuationConsumerEvidence(
    string Consumer, DateTimeOffset AsOfUtc, bool Complete,
    decimal? DenominatorCad, decimal? StockOnlyCad,
    IReadOnlyList<ValuationExposureProjection> Exposures,
    IReadOnlyList<string> IssueCodes, bool IsAuthoritative);

public sealed record PortfolioLotEvidence(
    PortfolioSummaryDto Summary, string? OwnerId, CurrencyEvidence? ListingCurrency,
    DateOnly ExpectedSession, DateTimeOffset ExpectedSessionCloseUtc);
