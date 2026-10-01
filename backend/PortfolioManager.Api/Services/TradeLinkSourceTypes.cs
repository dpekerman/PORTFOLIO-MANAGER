namespace PortfolioManager.Api.Services;

/// <summary>Trade legs that can own a cash ledger row. A position has an Open leg (cash out) and, once
/// closed, a Close leg (cash in) — each links independently.</summary>
public static class TradeLinkSourceTypes
{
    public const string PortfolioOpen = "PortfolioOpen";
    public const string PortfolioClose = "PortfolioClose";
    public const string OptionOpen = "OptionOpen";
    public const string OptionClose = "OptionClose";

    public static bool IsKnown(string? sourceType) =>
        sourceType is PortfolioOpen or PortfolioClose or OptionOpen or OptionClose;

    public static bool IsPortfolio(string sourceType) => sourceType is PortfolioOpen or PortfolioClose;

    public static string CashFlowTypeFor(string sourceType) =>
        sourceType is PortfolioOpen or OptionOpen ? CashFlowTypeRules.TradePurchase : CashFlowTypeRules.TradeProceeds;
}
