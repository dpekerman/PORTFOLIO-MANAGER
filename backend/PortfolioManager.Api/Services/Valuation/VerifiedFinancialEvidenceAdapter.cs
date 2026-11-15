using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services.Valuation;

public sealed class VerifiedFinancialEvidenceAdapter : IVerifiedFinancialEvidenceAdapter
{
    public ValuationAdapterResult Adapt(
        CashLedgerPackage cash, IReadOnlyList<OptionPositionEvidence> options,
        DateTimeOffset asOfUtc, DateOnly session, string userId, string accountScope)
    {
        var issues = new List<EvidenceIssue>();
        var components = new List<Pm029ValuationComponentInput>();
        var flows = new SortedDictionary<string, decimal>(StringComparer.Ordinal);
        bool Visible(string? owner, string? account) =>
            (owner is null || owner == userId)
            && (string.Equals(accountScope, Pm029ValuationScopes.Consolidated, StringComparison.OrdinalIgnoreCase)
                || string.Equals(account, accountScope, StringComparison.OrdinalIgnoreCase));
        var rows = cash.Entries.Where(row => Visible(row.OwnerId, row.AccountType)
            && row.EffectiveDate <= session).ToList();
        if (rows.Count > 0 && (cash.AsOfUtc == default || cash.AsOfUtc > asOfUtc || cash.CompleteThroughSession != session
                || string.IsNullOrWhiteSpace(cash.Source)))
            issues.Add(new("CashLedger", "LedgerCoverageUnverified"));
        foreach (var duplicates in rows.GroupBy(row => row.Id).Where(group => group.Count() > 1))
            issues.Add(new(duplicates.Key, "DuplicateLedgerRow"));
        foreach (var account in rows.GroupBy(row => (row.OwnerId, row.AccountId))
                     .Where(group => group.Select(row => row.AccountType).Distinct().Count() > 1))
            issues.Add(new(account.Key.AccountId ?? "Cash", "AccountLabelConflict"));
        foreach (var duplicates in rows.Where(row => row.SourceType is not null && row.SourceItemId.HasValue)
                     .GroupBy(row => $"{row.SourceType!.ToUpperInvariant()}:{row.SourceItemId}")
                     .Where(group => group.Count() > 1))
            issues.Add(new(duplicates.Key, Pm029ValuationIssueCodes.CashTradeLinkDuplicated));

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Id) || string.IsNullOrWhiteSpace(row.AccountId))
                issues.Add(new(row.Id, "CashAccountIdentityMissing"));
            if (row.RecordedAtUtc == default || row.RecordedAtUtc > asOfUtc)
                issues.Add(new(row.Id, "LedgerRowNotAvailableAsOf"));
            if (!CashFlowTypeRules.IsKnownType(row.CashFlowType))
                issues.Add(new(row.Id, "CashFlowClassificationMissing"));
            else if (row.SignedAmount != 0m && Math.Sign(row.SignedAmount) != CashFlowTypeRules.AllowedSign(row.CashFlowType))
                issues.Add(new(row.Id, "CashFlowSignConflict"));
            if ((row.SourceType is null) != (row.SourceItemId is null))
                issues.Add(new(row.Id, "IncompleteTradeLink"));
            var priorCodes = (row.HistoricalCurrencyEvidence ?? []).Append(row.Currency)
                .Where(evidence => evidence is { IsVerified: true }
                    && evidence.VerifiedAtUtc <= asOfUtc)
                .Select(evidence => Normalize(evidence!.CurrencyCode)).Distinct().ToList();
            if (priorCodes.Count > 1)
                issues.Add(new(row.Id, "ConflictingCurrencyEvidence"));
        }

        foreach (var bucket in rows.GroupBy(row =>
                     (row.OwnerId, row.AccountId, row.AccountType, Currency: Normalize(row.Currency?.CurrencyCode))))
        {
            var entries = bucket.ToList();
            var id = $"cash:{bucket.Key.OwnerId ?? "shared"}:{bucket.Key.AccountId}:{bucket.Key.Currency}";
            if (entries.Count(row => row.CashFlowType == CashFlowTypeRules.OpeningBalance) > 1)
                issues.Add(new(id, "MultipleOpeningBalances"));
            var verified = entries.All(row => row.Currency is { IsVerified: true, VerifiedAtUtc: not null }
                && !string.IsNullOrWhiteSpace(row.Currency.Source)
                && row.Currency.VerifiedAtUtc <= asOfUtc
                && row.Currency.CurrencyCode?.Trim().Length == 3);
            var evidence = verified
                ? new CurrencyEvidence(bucket.Key.Currency, cash.Source, true,
                    entries.Max(row => row.Currency!.VerifiedAtUtc))
                : new CurrencyEvidence(bucket.Key.Currency, null, false, null);
            components.Add(new Pm029CashBalanceValuationInput(
                id, bucket.Key.OwnerId, bucket.Key.AccountType, entries.Sum(row => row.SignedAmount),
                evidence, cash.AsOfUtc, cash.CompleteThroughSession, cash.Source,
                entries.Where(row => row.SourceType is not null && row.SourceItemId.HasValue)
                    .Select(row => $"{row.SourceType}:{row.SourceItemId}").ToArray()));
            if (verified)
            {
                flows.TryGetValue(bucket.Key.Currency, out var external);
                flows[bucket.Key.Currency] = external + entries
                    .Where(row => CashFlowTypeRules.IsExternalFlow(row.CashFlowType)).Sum(row => row.SignedAmount);
            }
        }

        var selectedOptions = options.Where(row => Visible(row.OwnerId, row.AccountType)).ToList();
        foreach (var duplicate in selectedOptions.GroupBy(row => (row.OwnerId, row.AccountId, row.ContractId))
                     .Where(group => group.Count() > 1))
            issues.Add(new(duplicate.Key.ContractId ?? "Option", "DuplicateOptionPosition"));
        foreach (var option in selectedOptions)
        {
            var positionCurrent = !string.IsNullOrWhiteSpace(option.PositionSource)
                && option.PositionAsOfUtc.HasValue && option.PositionAsOfUtc <= asOfUtc
                && option.ExpectedSessionCloseUtc.HasValue
                && option.PositionAsOfUtc >= option.ExpectedSessionCloseUtc;
            if (option.State == "Closed" && option.SignedOpenContracts == 0 && positionCurrent
                && !string.IsNullOrWhiteSpace(option.AccountId) && !string.IsNullOrWhiteSpace(option.ContractId))
                continue;
            var verified = option.State == "Open" && positionCurrent
                && option.ContractType is "CALL" or "PUT"
                && option.PositionSide is "LONG" or "SHORT"
                && option.SignedOpenContracts is not null and not 0
                && Math.Sign(option.SignedOpenContracts.Value) == (option.PositionSide == "LONG" ? 1 : -1)
                && !string.IsNullOrWhiteSpace(option.ContractId)
                && !string.IsNullOrWhiteSpace(option.AccountId);
            if (!verified)
                issues.Add(new(option.Id, "OptionPositionEvidenceUnverified"));
            components.Add(new Pm029OptionValuationInput(
                option.Id, option.OwnerId, option.AccountType, option.Symbol, option.InvestmentRole,
                option.Currency, option.QuoteCurrency, option.MarkPrice, option.QuoteSource, option.QuoteAsOfUtc,
                option.QuoteSession, option.ExpectedSession, option.ExpectedSessionCloseUtc,
                option.SignedOpenContracts, option.Multiplier, verified, option.TermsSource, option.TermsVerifiedAtUtc));
        }
        return new(components, issues, flows);
    }

    private static string Normalize(string? code) => code?.Trim().ToUpperInvariant() ?? "Unknown";
}
