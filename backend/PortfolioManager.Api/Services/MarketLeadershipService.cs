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
    string LeadershipReason);

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

public sealed class MarketLeadershipService(AppDbContext db, IMarketDataProvider marketData) : IMarketLeadershipService
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
            var closes = await marketData.GetDailyClosesAsync(tracker.Symbol, ct);
            var analysis = closes is null ? null : MarketLeadershipCalculator.Analyze(closes);
            rows.Add(ToRow(tracker, analysis, closes));
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
        MarketLeadershipAnalysis? analysis,
        IReadOnlyList<MarketDailyClose>? history)
    {
        if (analysis is null || !analysis.HasTechnicalData)
            return new MarketLeadershipRow(tracker.Id, tracker.Symbol, tracker.DisplayName, tracker.TrackerType,
                false, "At least 200 daily closes are required.", analysis?.CurrentPrice ?? 0m,
                0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, "Unavailable", "Unavailable", "Unavailable", "Unavailable",
                null, null, null, "Technical history is unavailable.", PriceStructureResult.None, "Neutral", "Technical history is unavailable.");

        var validHistory = history ?? [];
        var candles = validHistory.Select(item => new ChannelCandle(
            item.Date.ToDateTime(TimeOnly.MinValue), item.Open, item.High, item.Low, item.Close)).ToList();
        var atr = CalculateAtr(candles);
        var ema9 = validHistory.TakeLast(9).Average(item => item.Close);
        var volumeRatio20 = validHistory.TakeLast(21).SkipLast(1).Select(item => item.Volume).DefaultIfEmpty().Average() is var averageVolume && averageVolume > 0
            ? Math.Round(validHistory[^1].Volume / (decimal)averageVolume, 2) : 0m;
        var wedge = ChannelAnalysisService.AnalyzePriceStructure(candles, atr, ema9, analysis.MomentumState, volumeRatio20);
        var channel = new ChannelAnalysisService().Analyze(candles, atr, analysis.CurrentPrice);
        var structure = wedge.Label == "—"
            ? ChannelAnalysisService.FromChannel(channel, atr, ema9, volumeRatio20)
            : wedge;

        return new MarketLeadershipRow(tracker.Id, tracker.Symbol, tracker.DisplayName, tracker.TrackerType,
            true, null, analysis.CurrentPrice, analysis.DayReturnPct, analysis.FiveDayReturnPct, analysis.PreviousFiveDayReturnPct,
            analysis.TwentyDayReturnPct, analysis.PreviousTwentyDayReturnPct,
            analysis.Sma50, analysis.Sma200, PercentDifference(analysis.CurrentPrice, analysis.Sma50),
            PercentDifference(analysis.CurrentPrice, analysis.Sma200), PercentDifference(analysis.Sma50, analysis.Sma200),
            analysis.TrendState, analysis.MomentumState, analysis.MaStructure, analysis.MaBadge,
            analysis.LastCross, analysis.LastCrossDate, analysis.LastCrossTradingDaysAgo, analysis.MomentumReason,
            structure, analysis.LeadershipSignal, analysis.LeadershipReason);
    }

    private static decimal CalculateAtr(IReadOnlyList<ChannelCandle> candles)
    {
        if (candles.Count < 15) return 0m;
        return candles.TakeLast(14).Select((candle, index) =>
        {
            var previous = candles[candles.Count - 15 + index].Close;
            return Math.Max(candle.High - candle.Low, Math.Max(Math.Abs(candle.High - previous), Math.Abs(candle.Low - previous)));
        }).Average();
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
