using System.Text.Json;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services.Valuation;

namespace PortfolioManager.Phase15Verification;

public sealed record CashCandidate(
    int Id, int Shared, int HasAccountLabel, int HasEffectiveDate,
    string? CashFlowType, string? SourceType, int? SourceItemId);

public sealed record OptionCandidate(
    int Id, string? TransactionType, int NumberOfContracts,
    int HasAccountLabel, int HasCandidateTerms, string CandidateGroup);

public sealed record LegacyEvidenceExport(
    DateTime CapturedAtUtc, CashCandidate[] Cash, OptionCandidate[] Options,
    int UniverseListings, int VerifiedBrokerStops, int DuplicateTradeLinks);

public sealed record ApprovalRecord(
    string Reference, string Kind, string Category, IReadOnlyList<string> MissingEvidence,
    string? CandidateGroup = null, string? HistoricalTransaction = null);

public sealed record EvidenceManifestResult(
    DateTime CapturedAtUtc, int UniverseListings, int DuplicateTradeLinks,
    IReadOnlyDictionary<string, int> CashCategories,
    IReadOnlyDictionary<string, int> OptionCategories,
    int OptionCandidateGroups, int CandidateGroupsWithOpenAndClose,
    IReadOnlyList<ApprovalRecord> Records,
    bool CompletePortfolioValuationAvailable = false);

public static class EvidenceManifest
{
    public static EvidenceManifestResult Classify(LegacyEvidenceExport export)
    {
        ArgumentNullException.ThrowIfNull(export);
        if (export.CapturedAtUtc.Kind != DateTimeKind.Utc
            || export.Cash.Any(row => row.Id <= 0) || export.Options.Any(row => row.Id <= 0)
            || export.Cash.Select(row => row.Id).Distinct().Count() != export.Cash.Length
            || export.Options.Select(row => row.Id).Distinct().Count() != export.Options.Length)
            throw new InvalidDataException("Evidence export requires UTC capture time and valid unique record identities.");
        var classifier = new LegacyProvenanceClassifier();
        var cutoff = new DateTimeOffset(export.CapturedAtUtc);
        var records = new List<ApprovalRecord>();
        foreach (var row in export.Cash)
        {
            var core = classifier.ClassifyCash(new CashItem { Id = row.Id }, [], cutoff);
            var missing = core.MissingEvidence.ToList();
            missing.AddRange(["CurrencyEffectiveDate", "StableBrokerCashAccountId"]);
            if (row.Shared != 0) missing.Add("LegacyOwnershipConfirmation");
            if (row.HasEffectiveDate == 0) missing.Add("LedgerEffectiveDate");
            if (row.HasAccountLabel == 0) missing.Add("AccountAssignment");
            if (row.CashFlowType == "OpeningBalance") missing.Add("CurrencySpecificOpeningOwnership");
            if (row.SourceType is not null && row.SourceItemId is null) missing.Add("TradeLinkIdentity");
            records.Add(new(core.Id, "Cash", row.HasAccountLabel == 0
                ? "OwnerConfirmationRequired" : "BrokerConfirmationRequired", missing));
        }
        var groups = export.Options.GroupBy(row => row.CandidateGroup, StringComparer.Ordinal).ToArray();
        var groupReferences = groups.Select((group, index) => (group.Key, Reference: $"candidate-contract:{index + 1:D3}"))
            .ToDictionary(pair => pair.Key, pair => pair.Reference, StringComparer.Ordinal);
        foreach (var row in export.Options)
        {
            var core = classifier.ClassifyOption(new OptionItem { Id = row.Id }, null, cutoff);
            var missing = core.MissingEvidence.ToList();
            missing.AddRange(["ReconciledOutstandingQuantity", "PositionEffectiveDate", "QuoteCurrency", "OpenCloseLinkage"]);
            if (row.HasCandidateTerms == 0) missing.Add("ExpirationStrikeCallPut");
            if (row.NumberOfContracts <= 0) missing.Add("ValidHistoricalQuantity");
            records.Add(new(core.Id, "Option", row.HasAccountLabel == 0
                ? "OwnerConfirmationRequired" : "BrokerConfirmationRequired", missing,
                groupReferences[row.CandidateGroup], row.TransactionType));
        }
        IReadOnlyDictionary<string, int> Counts(string kind) =>
            new[] { "Verified", "BrokerConfirmationRequired", "OwnerConfirmationRequired", "ConflictingEvidence", "Unknown" }
                .ToDictionary(category => category, category => records.Count(row => row.Kind == kind && row.Category == category));
        return new(export.CapturedAtUtc, export.UniverseListings, export.DuplicateTradeLinks,
            Counts("Cash"), Counts("Option"), groups.Length,
            groups.Count(group => group.Any(row => row.TransactionType == "OPEN")
                && group.Any(row => row.TransactionType == "CLOSE")), records);
    }

    public static JsonSerializerOptions JsonOptions { get; } = new() { WriteIndented = true };
}
