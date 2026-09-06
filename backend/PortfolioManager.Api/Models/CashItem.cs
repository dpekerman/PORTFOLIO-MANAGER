namespace PortfolioManager.Api.Models;

public class CashItem
{
    public int Id { get; set; }
    /// <summary>Owning user — null means legacy/unowned data visible only to Admins.</summary>
    public string? UserId { get; set; }
    public string Description { get; set; } = "CASH";
    public decimal Amount { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Account type e.g. TFSA_L_RBC, Corp_TD</summary>
    public string? AccountType { get; set; }
    /// <summary>Optional user-entered transaction date (separate from system AddedAt).</summary>
    public DateTime? TransactionDate { get; set; }
    /// <summary>Ledger classification: OpeningBalance, Deposit, Withdrawal, TradeProceeds, TradePurchase,
    /// Dividend, Interest, Fee, Tax, AdjustmentIncrease, AdjustmentDecrease. Required for every row except
    /// pre-migration legacy data being converted. See <see cref="Services.CashFlowTypeRules"/>.</summary>
    public string? CashFlowType { get; set; }
    /// <summary>Set when an existing row is edited via UpdateAsync. Null for rows never edited since creation.</summary>
    public DateTime? ModifiedAt { get; set; }
}
