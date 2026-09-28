namespace PortfolioManager.Api.Models;

/// <summary>Singleton row (Id=1) marking the accounting boundary: dates before LedgerStartDate use
/// frozen legacy history; dates on/after it are reconstructed authoritatively from the cash ledger.
/// Written once by the OpeningBalance migration — never inferred from MIN(TransactionDate) or
/// DateTime.Today, so the boundary can't shift on a rebuild/redeploy.</summary>
public class CashLedgerSettings
{
    public int Id { get; set; }
    public DateTime LedgerStartDate { get; set; }
}
