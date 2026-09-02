using PortfolioManager.Api.Models;
using Microsoft.Extensions.Caching.Memory;

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
    DateTime ComputedAt,
    string? AnalysisTicker = null,
    string? AnalysisMarket = null,
    string? AnalysisCurrency = null,
    bool UsesUnderlyingSecurity = false);

public interface ITechnicalSnapshotService
{
    Task<TechnicalSnapshot> GetSnapshotAsync(string tradingTicker, string? userId = null, CancellationToken ct = default);
}

public sealed class TechnicalSnapshotService(
    IMarketDataProvider marketData,
    ISecurityAnalysisResolver analysisResolver,
    IMemoryCache cache) : ITechnicalSnapshotService
{
    public async Task<TechnicalSnapshot> GetSnapshotAsync(
        string tradingTicker,
        string? userId = null,
        CancellationToken ct = default)
    {
        var resolved = await analysisResolver.ResolveAsync(tradingTicker, userId, ct);
        if (resolved.ResolutionStatus == UnderlyingResolutionStatus.NeedsUserInput)
        {
            var unavailable = FromHistory(resolved.TradingTicker, null);
            return unavailable with
            {
                DataError = resolved.DataError,
                AnalysisTicker = resolved.AnalysisTicker,
                AnalysisMarket = resolved.AnalysisMarket,
                AnalysisCurrency = resolved.AnalysisCurrency,
                UsesUnderlyingSecurity = false,
            };
        }

        var cacheKey = $"technical-snapshot:{resolved.AnalysisTicker}";
        var snapshot = await cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(4);
            var history = await marketData.GetDailyClosesAsync(resolved.AnalysisTicker, ct);
            return FromHistory(resolved.AnalysisTicker, history);
        }) ?? FromHistory(resolved.AnalysisTicker, null);
        return snapshot with
        {
            Symbol = resolved.TradingTicker,
            PriceStructure = snapshot.PriceStructure with { Symbol = resolved.TradingTicker },
            AnalysisTicker = resolved.AnalysisTicker,
            AnalysisMarket = resolved.AnalysisMarket,
            AnalysisCurrency = resolved.AnalysisCurrency,
            UsesUnderlyingSecurity = resolved.UsesUnderlyingSecurity,
        };
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

        structure = structure with { Symbol = symbol };
        return new TechnicalSnapshot(symbol, true, null, analysis, structure, Math.Round(atr, 4), Math.Round(ema9, 4), volumeRatio20, structure.CalculatedAt);
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
