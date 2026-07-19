namespace PortfolioManager.Api.Models;

/// <summary>End-of-day portfolio value snapshot persisted at 4:30 PM ET on trading days.</summary>
public class PortfolioValueHistory
{
    public int Id { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Trading date in ET, formatted as "YYYY-MM-DD".</summary>
    public string RecordedDate { get; set; } = "";

    public decimal TotalValue { get; set; }
    public decimal StocksValue { get; set; }
    public decimal CashValue { get; set; }
    public decimal OptionsValue { get; set; }
}

public record PortfolioValueHistoryDto(
    int Id,
    DateTime RecordedAt,
    string RecordedDate,
    decimal TotalValue,
    decimal StocksValue,
    decimal CashValue,
    decimal OptionsValue);

public record PortfolioBetaResult(
    decimal PortfolioBeta,
    decimal ExCashBeta,
    decimal CashPct,
    decimal ProxyPct,
    string Status,
    List<BetaContributor> TopContributors);

public record BetaContributor(
    string Symbol,
    decimal WeightPct,
    decimal Beta,
    bool IsProxy);
