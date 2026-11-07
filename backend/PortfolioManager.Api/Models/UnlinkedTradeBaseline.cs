namespace PortfolioManager.Api.Models;

/// <summary>A trade leg that had no cash entry when monitoring began and was deliberately left unfixed.
/// Kept purely as history: legs listed here are hidden from the "Trades without a cash entry" panel
/// unless the trade is edited afterwards (see <see cref="Fingerprint"/>). Never touches cash or trades.</summary>
public class UnlinkedTradeBaseline
{
    public int Id { get; set; }
    public string SourceType { get; set; } = "";
    public int SourceItemId { get; set; }
    public string Symbol { get; set; } = "";
    public string Label { get; set; } = "";
    public decimal? Amount { get; set; }
    public string? AccountType { get; set; }
    public DateTime TradeDate { get; set; }
    /// <summary>Quantity, price, amount, account and date of the leg when it was acknowledged.</summary>
    public string Fingerprint { get; set; } = "";
    public DateTime AcknowledgedAt { get; set; } = DateTime.UtcNow;
    public string Note { get; set; } = "Legacy gap - intentionally not fixed";
}
