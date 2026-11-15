using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services.Valuation;

public interface IVerifiedFinancialEvidenceAdapter
{
    ValuationAdapterResult Adapt(
        CashLedgerPackage cash, IReadOnlyList<OptionPositionEvidence> options,
        DateTimeOffset asOfUtc, DateOnly session, string userId, string accountScope);
}
