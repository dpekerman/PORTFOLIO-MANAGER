namespace PortfolioManager.Api.Models;

public sealed record DecisionPerformanceRow(
    string DecisionSource,
    int TradeCount,
    int WinCount,
    double WinRatePct,
    double AvgReturnPct,
    double AvgHoldingDays);

public sealed record AnalyticsDecisionPerformanceResponse(
    IReadOnlyList<DecisionPerformanceRow> Rows,
    int TotalClosedTrades,
    double OverallWinRatePct,
    double OverallAvgReturnPct);
