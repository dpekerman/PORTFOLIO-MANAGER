using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public sealed record MarketLeadershipRow(
    int Id,
    string Symbol,
    string DisplayName,
    MarketLeadershipTrackerType TrackerType,
    bool HasTechnicalData,
    string? DataError,
    decimal CurrentPrice,
    decimal DayReturnPct,
    decimal FiveDayReturnPct,
    decimal PreviousFiveDayReturnPct,
    decimal TwentyDayReturnPct,
    decimal PreviousTwentyDayReturnPct,
    decimal Sma50,
    decimal Sma200,
    decimal PriceVsSma50Pct,
    decimal PriceVsSma200Pct,
    decimal Sma50VsSma200Pct,
    string TrendState,
    string MomentumState,
    string MaStructure,
    string MaBadge,
    string? LastCross,
    DateOnly? LastCrossDate,
    int? LastCrossTradingDaysAgo,
    string MomentumReason,
    PriceStructureResult PriceStructure,
    string LeadershipSignal,
    string LeadershipReason,
    string? AnalysisTicker = null,
    string? AnalysisMarket = null,
    string? AnalysisCurrency = null,
    bool UsesUnderlyingSecurity = false);

public sealed record MarketLeadershipResponse(
    IReadOnlyList<MarketLeadershipRow> Rows,
    int EmergingCount,
    int LeadingCount,
    int CoolingCount,
    int NeutralCount,
    int WeakCount,
    DateTime ComputedAt);

public sealed record CreateMarketLeadershipTrackerRequest(
    string Symbol,
    string? DisplayName,
    MarketLeadershipTrackerType TrackerType);

public sealed record MarketLeadershipTrackerDto(
    int Id,
    string Symbol,
    string DisplayName,
    MarketLeadershipTrackerType TrackerType);

public interface IMarketLeadershipService
{
    Task<MarketLeadershipResponse> GetLeadershipAsync(string userId, CancellationToken ct = default);
    Task<MarketLeadershipTrackerDto> AddTrackerAsync(string userId, CreateMarketLeadershipTrackerRequest request, CancellationToken ct = default);
    Task<MarketLeadershipTrackerDto?> UpdateTrackerAsync(string userId, int trackerId, CreateMarketLeadershipTrackerRequest request, CancellationToken ct = default);
    Task<bool> RemoveTrackerAsync(string userId, int trackerId, CancellationToken ct = default);
}

public sealed class MarketLeadershipService(AppDbContext db, IMarketDataProvider marketData, ITechnicalSnapshotService technicalSnapshots) : IMarketLeadershipService
{
    public async Task<MarketLeadershipResponse> GetLeadershipAsync(string userId, CancellationToken ct = default)
    {
        var trackers = await db.MarketLeadershipTrackers.AsNoTracking()
            .Where(tracker => tracker.UserId == userId && tracker.IsActive)
            .OrderBy(tracker => tracker.SortOrder)
            .ThenBy(tracker => tracker.Symbol)
            .ToListAsync(ct);
        var rows = new List<MarketLeadershipRow>(trackers.Count);

        foreach (var tracker in trackers)
        {
            var snapshot = await technicalSnapshots.GetSnapshotAsync(tracker.Symbol, userId, ct);
            rows.Add(ToRow(tracker, snapshot));
        }

        var sorted = rows.OrderBy(row => SignalOrder(row.LeadershipSignal))
            .ThenByDescending(row => row.TwentyDayReturnPct)
            .ThenBy(row => row.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new MarketLeadershipResponse(
            sorted,
            rows.Count(row => row.LeadershipSignal == "Emerging"),
            rows.Count(row => row.LeadershipSignal == "Leading"),
            rows.Count(row => row.LeadershipSignal == "Cooling"),
            rows.Count(row => row.LeadershipSignal == "Neutral"),
            rows.Count(row => row.LeadershipSignal == "Weak"),
            DateTime.UtcNow);
    }

    public async Task<MarketLeadershipTrackerDto> AddTrackerAsync(string userId, CreateMarketLeadershipTrackerRequest request, CancellationToken ct = default)
    {
        var symbol = request.Symbol.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(symbol) || symbol.Length > 20)
            throw new ArgumentException("Enter a valid Yahoo Finance symbol.", nameof(request));
        if (await db.MarketLeadershipTrackers.AnyAsync(item => item.UserId == userId && item.Symbol == symbol && item.IsActive, ct))
            throw new InvalidOperationException($"{symbol} is already being tracked.");

        var quote = await marketData.GetQuoteAsync(symbol, ct);
        if (quote is null || quote.CurrentPrice <= 0m)
            throw new ArgumentException($"Yahoo Finance could not load {symbol}.", nameof(request));

        var nextSortOrder = (await db.MarketLeadershipTrackers.Where(item => item.UserId == userId)
            .Select(item => (int?)item.SortOrder).MaxAsync(ct) ?? -1) + 1;
        var tracker = new MarketLeadershipTracker
        {
            UserId = userId,
            Symbol = symbol,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? quote.CompanyName : request.DisplayName.Trim(),
            TrackerType = request.TrackerType,
            SortOrder = nextSortOrder,
        };
        db.MarketLeadershipTrackers.Add(tracker);
        await db.SaveChangesAsync(ct);
        return new MarketLeadershipTrackerDto(tracker.Id, tracker.Symbol, tracker.DisplayName, tracker.TrackerType);
    }

    public async Task<bool> RemoveTrackerAsync(string userId, int trackerId, CancellationToken ct = default)
    {
        var tracker = await db.MarketLeadershipTrackers.SingleOrDefaultAsync(
            item => item.Id == trackerId && item.UserId == userId && item.IsActive, ct);
        if (tracker is null) return false;
        tracker.IsActive = false;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<MarketLeadershipTrackerDto?> UpdateTrackerAsync(
        string userId,
        int trackerId,
        CreateMarketLeadershipTrackerRequest request,
        CancellationToken ct = default)
    {
        var tracker = await db.MarketLeadershipTrackers.SingleOrDefaultAsync(
            item => item.Id == trackerId && item.UserId == userId && item.IsActive, ct);
        if (tracker is null) return null;

        var symbol = request.Symbol.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(symbol) || symbol.Length > 20)
            throw new ArgumentException("Enter a valid Yahoo Finance symbol.", nameof(request));

        if (!string.Equals(symbol, tracker.Symbol, StringComparison.OrdinalIgnoreCase))
        {
            if (await db.MarketLeadershipTrackers.AnyAsync(
                    item => item.UserId == userId && item.Id != trackerId && item.Symbol == symbol && item.IsActive, ct))
                throw new InvalidOperationException($"{symbol} is already being tracked.");

            var quote = await marketData.GetQuoteAsync(symbol, ct);
            if (quote is null || quote.CurrentPrice <= 0m)
                throw new ArgumentException($"Yahoo Finance could not load {symbol}.", nameof(request));
            tracker.Symbol = symbol;
            if (string.IsNullOrWhiteSpace(request.DisplayName)) tracker.DisplayName = quote.CompanyName;
        }

        if (!string.IsNullOrWhiteSpace(request.DisplayName)) tracker.DisplayName = request.DisplayName.Trim();
        tracker.TrackerType = request.TrackerType;
        await db.SaveChangesAsync(ct);
        return new MarketLeadershipTrackerDto(tracker.Id, tracker.Symbol, tracker.DisplayName, tracker.TrackerType);
    }

    private static MarketLeadershipRow ToRow(
        MarketLeadershipTracker tracker,
        TechnicalSnapshot snapshot)
    {
        var analysis = snapshot.Analysis;
        if (analysis is null || !analysis.HasTechnicalData)
            return new MarketLeadershipRow(tracker.Id, tracker.Symbol, tracker.DisplayName, tracker.TrackerType,
                false, snapshot.DataError ?? "Technical history is unavailable.", analysis?.CurrentPrice ?? 0m,
                0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, "Unavailable", "Unavailable", "Unavailable", "Unavailable",
                null, null, null, "Technical history is unavailable.", PriceStructureResult.None, "Neutral", "Technical history is unavailable.",
                snapshot.AnalysisTicker, snapshot.AnalysisMarket, snapshot.AnalysisCurrency, snapshot.UsesUnderlyingSecurity);

        return new MarketLeadershipRow(tracker.Id, tracker.Symbol, tracker.DisplayName, tracker.TrackerType,
            true, null, analysis.CurrentPrice, analysis.DayReturnPct, analysis.FiveDayReturnPct, analysis.PreviousFiveDayReturnPct,
            analysis.TwentyDayReturnPct, analysis.PreviousTwentyDayReturnPct,
            analysis.Sma50, analysis.Sma200, PercentDifference(analysis.CurrentPrice, analysis.Sma50),
            PercentDifference(analysis.CurrentPrice, analysis.Sma200), PercentDifference(analysis.Sma50, analysis.Sma200),
            analysis.TrendState, analysis.MomentumState, analysis.MaStructure, analysis.MaBadge,
            analysis.LastCross, analysis.LastCrossDate, analysis.LastCrossTradingDaysAgo, analysis.MomentumReason,
            snapshot.PriceStructure, analysis.LeadershipSignal, analysis.LeadershipReason,
            snapshot.AnalysisTicker, snapshot.AnalysisMarket, snapshot.AnalysisCurrency, snapshot.UsesUnderlyingSecurity);
    }

    private static decimal PercentDifference(decimal value, decimal baseValue) =>
        baseValue == 0m ? 0m : Math.Round(((value / baseValue) - 1m) * 100m, 2);

    private static int SignalOrder(string signal) => signal switch
    {
        "Emerging" => 0,
        "Leading" => 1,
        "Cooling" => 2,
        "Neutral" => 3,
        "Weak" => 4,
        _ => 5,
    };
}
