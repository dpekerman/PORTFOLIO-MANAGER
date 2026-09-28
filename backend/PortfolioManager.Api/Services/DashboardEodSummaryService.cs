using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public interface IDashboardEodSummaryService
{
    Task<DashboardEodSummary> GetLatestAsync(string userId, CancellationToken ct = default);
}

public sealed class DashboardEodSummaryService(
    AppDbContext db,
    IPortfolioActionsService portfolioActions) : IDashboardEodSummaryService
{
    private const int DashboardRowLimit = 12;

    public async Task<DashboardEodSummary> GetLatestAsync(string userId, CancellationToken ct = default)
    {
        var tradingDate = await db.DailySignals.AsNoTracking()
            .Where(signal => signal.TradingDate != null)
            .MaxAsync(signal => signal.TradingDate, ct);

        if (tradingDate is null)
            return new DashboardEodSummary(null, 0, 0, []);

        var sessionRecords = await db.DailySignals.AsNoTracking()
            .Where(signal => signal.TradingDate == tradingDate)
            .ToListAsync(ct);
        var canonicalRecords = sessionRecords
            .GroupBy(signal => CanonicalTicker(signal.Symbol), StringComparer.Ordinal)
            .Select(group => group
                .OrderByDescending(signal => signal.ScannedAt ?? DateTime.MinValue)
                .ThenByDescending(signal => signal.UpdatedAt ?? signal.RecordedAt)
                .ThenByDescending(signal => signal.RecordedAt)
                .ThenByDescending(signal => signal.Id)
                .First())
            .ToList();

        var portfolioSymbols = await db.PortfolioItems.AsNoTracking()
            .Where(item => (item.UserId == userId || item.UserId == null)
                && (item.TransactionType == null || item.TransactionType != "CLOSE"))
            .Select(item => item.Symbol)
            .ToListAsync(ct);
        var watchlistSymbols = await db.WatchlistItems.AsNoTracking()
            .Where(item => item.UserId == userId || item.UserId == null)
            .Select(item => item.Symbol)
            .ToListAsync(ct);
        var portfolioTickers = portfolioSymbols.Select(CanonicalTicker).ToHashSet(StringComparer.Ordinal);
        var watchlistTickers = watchlistSymbols.Select(CanonicalTicker).ToHashSet(StringComparer.Ordinal);
        var actions = await portfolioActions.GetActionsAsync(userId, ct);
        var actionsByTicker = actions
            .GroupBy(action => CanonicalTicker(action.Symbol), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(action => action.IsInPortfolio).First(),
                StringComparer.Ordinal);

        var rows = canonicalRecords
            .Select(signal => ToRow(signal, portfolioTickers, watchlistTickers, actionsByTicker))
            .OrderBy(row => ActionImportance(row.Action, row.ActionPriority))
            .ThenBy(row => row.Symbol, StringComparer.Ordinal)
            .Take(DashboardRowLimit)
            .ToList()
            .AsReadOnly();

        return new DashboardEodSummary(tradingDate, sessionRecords.Count, canonicalRecords.Count, rows);
    }

    private static DashboardEodSummaryRow ToRow(
        DailySignal signal,
        HashSet<string> portfolioTickers,
        HashSet<string> watchlistTickers,
        Dictionary<string, PortfolioActionDto> actionsByTicker)
    {
        var symbol = CanonicalTicker(signal.Symbol);
        var ownership = portfolioTickers.Contains(symbol)
            ? "Portfolio"
            : watchlistTickers.Contains(symbol)
                ? "Watchlist"
                : "Universe";
        var action = actionsByTicker.GetValueOrDefault(symbol);
        var isOwnedAction = action is not null && (ownership != "Portfolio" || action.IsInPortfolio);
        var (actionLabel, actionPriority, resolutionStatus, resolutionReason) = ownership switch
        {
            "Universe" => ("—", "INFORMATIONAL", "OwnershipActionNotApplicable", "No portfolio or watchlist action context."),
            _ when isOwnedAction => (action!.ActionLabel, action.ActionPriority, "Resolved", string.Empty),
            _ => ("—", "INFORMATIONAL", "OwnershipActionNotCalculated", "Ownership action not calculated; technical snapshot may be unavailable."),
        };

        return new DashboardEodSummaryRow(
            symbol,
            signal.CompanyName,
            signal.ScanType,
            signal.Rsi,
            signal.SignalState,
            signal.TrendShift,
            action?.PriceStructure?.Label ?? action?.PriceStructure?.PrimaryPatternType ?? "—",
            string.IsNullOrWhiteSpace(signal.TriggerDetails) ? "EOD signal recorded for this session." : signal.TriggerDetails,
            ownership,
            actionLabel,
            actionPriority,
            resolutionStatus,
            resolutionReason);
    }

    private static int ActionImportance(string action, string priority)
    {
        if (action.Contains("EXIT", StringComparison.OrdinalIgnoreCase)
            || action.Contains("TRIM", StringComparison.OrdinalIgnoreCase)
            || action.Contains("REVIEW", StringComparison.OrdinalIgnoreCase)
            || action.Contains("AVOID", StringComparison.OrdinalIgnoreCase)
            || action.Contains("WAIT FOR RECLAIM", StringComparison.OrdinalIgnoreCase))
            return 0;
        if (action is "ENTRY CANDIDATE" or "STARTER ENTRY") return 1;
        if (action is "BUY WATCH" or "REVERSAL WATCH") return 2;
        return priority == "REQUIRED" ? 0 : 3;
    }

    private static string CanonicalTicker(string symbol) => symbol.Trim().ToUpperInvariant();
}