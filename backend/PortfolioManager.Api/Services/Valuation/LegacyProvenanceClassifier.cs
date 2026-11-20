using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services.Valuation;

public sealed record LegacyProvenanceCandidate(
    string Id, string Kind, string Classification, IReadOnlyList<string> MissingEvidence);

public interface ILegacyProvenanceClassifier
{
    LegacyProvenanceCandidate ClassifyCash(CashItem row, IReadOnlyList<CurrencyEvidence> evidence, DateTimeOffset asOfUtc);
    LegacyProvenanceCandidate ClassifyOption(OptionItem row, OptionPositionEvidence? evidence, DateTimeOffset asOfUtc);
}

public sealed class LegacyProvenanceClassifier : ILegacyProvenanceClassifier
{
    public LegacyProvenanceCandidate ClassifyCash(
        CashItem row, IReadOnlyList<CurrencyEvidence> evidence, DateTimeOffset asOfUtc)
    {
        var verified = evidence.Where(item => item.IsVerified && item.VerifiedAtUtc <= asOfUtc
            && !string.IsNullOrWhiteSpace(item.Source) && !string.IsNullOrWhiteSpace(item.CurrencyCode)).ToArray();
        var codes = verified.Select(item => item.CurrencyCode!.Trim().ToUpperInvariant()).Distinct().ToArray();
        var classification = codes.Length > 1 ? "ConflictingEvidence"
            : codes.Length == 1 ? "VerifiedFromAuthoritativeEvidence"
            : evidence.Count > 0 ? "RequiresBrokerOrOwnerConfirmation" : "UnknownCurrency";
        return new($"cash-row:{row.Id}", "Cash", classification,
            codes.Length == 1 ? [] : ["Currency", "CurrencyVerificationSource", "CurrencyVerifiedAtUtc"]);
    }

    public LegacyProvenanceCandidate ClassifyOption(
        OptionItem row, OptionPositionEvidence? evidence, DateTimeOffset asOfUtc)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(evidence?.ContractId)) missing.Add("ContractIdentity");
        if (string.IsNullOrWhiteSpace(evidence?.AccountId)) missing.Add("AccountIdentity");
        if (evidence?.Currency is not { IsVerified: true, VerifiedAtUtc: not null }
            || evidence.Currency.VerifiedAtUtc > asOfUtc || string.IsNullOrWhiteSpace(evidence.Currency.Source)
            || evidence.Currency.CurrencyCode is not ("CAD" or "USD"))
            missing.Add("VerifiedCurrency");
        if (evidence?.PositionSide is not ("LONG" or "SHORT")) missing.Add("PositionSide");
        if (evidence?.Multiplier is not > 0m || string.IsNullOrWhiteSpace(evidence.TermsSource)
            || evidence.TermsVerifiedAtUtc is null || evidence.TermsVerifiedAtUtc > asOfUtc)
            missing.Add("VerifiedMultiplier");
        if (string.IsNullOrWhiteSpace(evidence?.QuoteSource) || evidence.QuoteAsOfUtc is null
            || evidence.QuoteAsOfUtc > asOfUtc) missing.Add("QuoteProvenance");
        if (evidence?.PositionAsOfUtc is null || evidence.PositionAsOfUtc > asOfUtc
            || string.IsNullOrWhiteSpace(evidence.PositionSource)) missing.Add("PositionProvenance");
        if (evidence?.State is not ("Open" or "Closed") || evidence.SignedOpenContracts is null
            || evidence.State == "Open" && evidence.SignedOpenContracts == 0
            || evidence.State == "Closed" && evidence.SignedOpenContracts != 0)
            missing.Add("OpenQuantityAndState");
        if (evidence?.ContractType is not ("CALL" or "PUT")) missing.Add("ContractType");
        if (evidence?.State != "Closed" && evidence?.MarkPrice is not > 0m) missing.Add("PositiveMark");
        var conflict = evidence is not null && (evidence.OwnerId != row.UserId
            || evidence.AccountType != row.AccountType || evidence.ContractType != row.PositionType
            || !string.Equals(evidence.Symbol, row.UnderlyingTicker, StringComparison.OrdinalIgnoreCase)
            || evidence.SignedOpenContracts.HasValue && evidence.SignedOpenContracts != 0
                && evidence.PositionSide is "LONG" or "SHORT"
                && Math.Sign(evidence.SignedOpenContracts.Value) != (evidence.PositionSide == "LONG" ? 1 : -1));
        if (conflict) missing.Add("ConflictingPositionEvidence");
        return new($"option-row:{row.Id}", "Option", conflict ? "ConflictingEvidence"
            : missing.Count > 0 ? "RequiresBrokerOrOwnerConfirmation" : "VerifiedFromAuthoritativeEvidence", missing);
    }
}
