using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public sealed record TechnicalSnapshot(
    string Symbol,
    bool HasTechnicalData,
    string? DataError,
    MarketLeadershipAnalysis Analysis,
    PriceStructureResult PriceStructure,
    decimal Atr,
    decimal Ema9,
    decimal VolumeRatio20,
    DateTime ComputedAt);

public interface ITechnicalSnapshotService
{
    Task<TechnicalSnapshot> GetSnapshotAsync(string symbol, CancellationToken ct = default);
}

public sealed class TechnicalSnapshotService(IMarketDataProvider marketData) : ITechnicalSnapshotService
{
    public async Task<TechnicalSnapshot> GetSnapshotAsync(string symbol, CancellationToken ct = default)
    {
        var normalizedSymbol = symbol.Trim().ToUpperInvariant();
        var history = await marketData.GetDailyClosesAsync(normalizedSymbol, ct);
        return FromHistory(normalizedSymbol, history);
    }

    public static TechnicalSnapshot FromHistory(string symbol, IReadOnlyList<MarketDailyClose>? history)
    {
        var analysis = history is null ? MarketLeadershipCalculator.Analyze(Array.Empty<decimal>()) : MarketLeadershipCalculator.Analyze(history);
        if (history is null || !analysis.HasTechnicalData)
            return new TechnicalSnapshot(symbol, false, "At least 200 daily closes are required.", analysis, PriceStructureResult.None, 0m, 0m, 0m, DateTime.UtcNow);

        var candles = history.Select(item => new ChannelCandle(
            item.Date.ToDateTime(TimeOnly.MinValue), item.Open, item.High, item.Low, item.Close)).ToList();
        var atr = CalculateAtr(candles);
        var ema9 = CalculateEma(history.Select(item => item.Close).ToList(), 9);
        var volumeRatio20 = history.TakeLast(21).SkipLast(1).Select(item => item.Volume).DefaultIfEmpty().Average() is var averageVolume && averageVolume > 0
            ? Math.Round(history[^1].Volume / (decimal)averageVolume, 2) : 0m;
        var wedge = ChannelAnalysisService.AnalyzePriceStructure(candles, atr, ema9, analysis.MomentumState, volumeRatio20);
        var channel = new ChannelAnalysisService().Analyze(candles, atr, analysis.CurrentPrice);
        var structure = wedge.Label == "—"
            ? ChannelAnalysisService.FromChannel(channel, atr, ema9, volumeRatio20)
            : wedge;

        return new TechnicalSnapshot(symbol, true, null, analysis, structure, Math.Round(atr, 4), Math.Round(ema9, 4), volumeRatio20, DateTime.UtcNow);
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

    private static decimal CalculateEma(IReadOnlyList<decimal> values, int period)
    {
        if (values.Count == 0) return 0m;
        if (values.Count < period) return values.Average();

        var seed = values.Take(period).Average();
        var multiplier = 2m / (period + 1);
        var ema = seed;
        foreach (var value in values.Skip(period))
            ema = ((value - ema) * multiplier) + ema;
        return ema;
    }
}
