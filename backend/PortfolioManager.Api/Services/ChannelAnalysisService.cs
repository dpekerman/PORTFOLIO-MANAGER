using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public interface IChannelAnalysisService
{
    ChannelAnalysisResult Analyze(
        IReadOnlyList<ChannelCandle> candles,
        decimal atr,
        decimal currentPrice,
        string momentumShift = "");
}

public sealed class ChannelAnalysisService : IChannelAnalysisService
{
    private static readonly int[] Windows = [63, 126, 252, 504];

    public ChannelAnalysisResult Analyze(
        IReadOnlyList<ChannelCandle> candles,
        decimal atr,
        decimal currentPrice,
        string momentumShift = "")
    {
        if (candles.Count < 30 || atr <= 0 || currentPrice <= 0)
            return ChannelAnalysisResult.None;

        ChannelCandidate? best = null;
        foreach (var window in Windows)
        {
            var start = Math.Max(0, candles.Count - window);
            var candidate = BuildCandidate(candles, start, atr, currentPrice);
            if (candidate is not null && (best is null || candidate.Quality > best.Quality))
                best = candidate;
        }

        if (best is null || best.Quality < 70)
            return ChannelAnalysisResult.None;

        var lowerToday = best.LowerSlope * (candles.Count - 1) + best.LowerIntercept;
        var upperToday = best.UpperSlope * (candles.Count - 1) + best.UpperIntercept;
        var distance = currentPrice - lowerToday;
        var distanceAtr = distance / atr;
        var state = ResolveState(best.ConfirmedTouches.Count, distanceAtr);

        var gaps = FindOpenGaps(candles, currentPrice);
        return new ChannelAnalysisResult(
            ChannelDirection.RISING,
            Math.Round(best.LowerSlope, 6),
            Math.Round(lowerToday, 4),
            Math.Round(upperToday, 4),
            best.Quality,
            best.ConfirmedTouches.Count,
            best.ConfirmedTouches.Count == 0 ? null : candles[best.ConfirmedTouches[^1]].Date,
            Math.Round(distance / currentPrice * 100m, 2),
            Math.Round(distanceAtr, 2),
            state,
            gaps.Above,
            gaps.Below,
            gaps.Above.HasValue ? Math.Round((gaps.Above.Value - currentPrice) / currentPrice * 100m, 2) : null,
            gaps.Below.HasValue ? Math.Round((currentPrice - gaps.Below.Value) / currentPrice * 100m, 2) : null,
            best.TouchDetails);
    }

    public static ChannelState ResolveState(int confirmedTouchCount, decimal distanceAtr)
    {
        if (distanceAtr < -0.5m) return ChannelState.CHANNEL_BROKEN;
        if (Math.Abs(distanceAtr) <= 0.35m)
            return confirmedTouchCount >= 3 ? ChannelState.LOWER_RAIL_RETEST : ChannelState.THIRD_TOUCH_TEST;
        if (distanceAtr > 0.35m && distanceAtr <= 1m)
            return confirmedTouchCount >= 3 ? ChannelState.LOWER_RAIL_APPROACHING : ChannelState.THIRD_TOUCH_APPROACHING;
        return ChannelState.CHANNEL_ACTIVE;
    }

    private static ChannelCandidate? BuildCandidate(
        IReadOnlyList<ChannelCandle> candles,
        int start,
        decimal atr,
        decimal currentPrice)
    {
        var pivots = Enumerable.Range(start + 2, candles.Count - start - 4)
            .Where(i => IsPivotLow(candles, i) || IsPivotHigh(candles, i))
            .ToList();
        var lows = pivots.Where(i => IsPivotLow(candles, i)).ToList();
        var highs = pivots.Where(i => IsPivotHigh(candles, i)).ToList();
        if (lows.Count < 2 || highs.Count < 2) return null;

        var lower = FitRail(lows, i => candles[i].Low);
        var upper = FitRail(highs, i => candles[i].High);
        if (lower.Slope <= 0 || upper.Slope <= 0) return null;
        var slopeRatio = lower.Slope == 0 ? decimal.MaxValue : upper.Slope / lower.Slope;
        if (slopeRatio < 0.5m || slopeRatio > 2m) return null;

        var confirmed = new List<int>();
        var touchDetails = new List<ChannelTouchDetail>();
        var lastTouch = -10000;
        var lastBounce = -10000;
        foreach (var index in lows)
        {
            var rail = lower.Slope * index + lower.Intercept;
            if (Math.Abs(candles[index].Low - rail) > 0.35m * atr || index - lastTouch < 10 || index - lastBounce < 10)
                continue;

            var bounceEnd = Math.Min(candles.Count - 1, index + 20);
            var bounceStart = Math.Min(candles.Count - 1, index + 1);
            var bounce = Enumerable.Range(bounceStart, bounceEnd - bounceStart + 1)
                .Any(i => candles[i].High >= rail + 1.5m * atr);
            if (!bounce) continue;

            confirmed.Add(index);
            lastTouch = index;
            lastBounce = Enumerable.Range(bounceStart, bounceEnd - bounceStart + 1)
                .First(i => candles[i].High >= rail + 1.5m * atr);
            touchDetails.Add(new ChannelTouchDetail(
                confirmed.Count,
                candles[index].Date,
                Math.Round(rail, 4),
                Math.Round(candles[index].Low, 4),
                Math.Round((candles[lastBounce].High - rail) / atr, 2),
                true));
        }

        if (confirmed.Count < 2) return null;

        var width = upper.Slope * (candles.Count - 1) + upper.Intercept
                  - (lower.Slope * (candles.Count - 1) + lower.Intercept);
        if (width <= atr || currentPrice < lower.Slope * (candles.Count - 1) + lower.Intercept - atr)
            return null;

        var parallelScore = 100m - Math.Min(100m, Math.Abs(1m - slopeRatio) * 100m);
        var spacingScore = Math.Min(100m, (decimal)confirmed.Zip(confirmed.Skip(1), (a, b) => b - a).Average() / 2m);
        var quality = (int)Math.Round(confirmed.Count >= 3
            ? parallelScore * 0.35m + spacingScore * 0.2m + 100m * 0.45m
            : parallelScore * 0.4m + spacingScore * 0.2m + 80m * 0.4m);
        return new ChannelCandidate(lower.Slope, lower.Intercept, upper.Slope, upper.Intercept, Math.Clamp(quality, 0, 100), confirmed, touchDetails);
    }

    private static (decimal Slope, decimal Intercept) FitRail(
        IReadOnlyList<int> indexes,
        Func<int, decimal> value)
    {
        var count = indexes.Count;
        var meanX = indexes.Average(i => (decimal)i);
        var meanY = indexes.Average(value);
        var denominator = indexes.Sum(i => ((decimal)i - meanX) * ((decimal)i - meanX));
        var slope = denominator == 0 ? 0 : indexes.Sum(i => ((decimal)i - meanX) * (value(i) - meanY)) / denominator;
        return (slope, meanY - slope * meanX);
    }

    private static bool IsPivotLow(IReadOnlyList<ChannelCandle> candles, int i)
        => candles[i].Low < candles[i - 1].Low && candles[i].Low < candles[i - 2].Low
        && candles[i].Low < candles[i + 1].Low && candles[i].Low < candles[i + 2].Low;

    private static bool IsPivotHigh(IReadOnlyList<ChannelCandle> candles, int i)
        => candles[i].High > candles[i - 1].High && candles[i].High > candles[i - 2].High
        && candles[i].High > candles[i + 1].High && candles[i].High > candles[i + 2].High;

    private static (decimal? Above, decimal? Below) FindOpenGaps(
        IReadOnlyList<ChannelCandle> candles,
        decimal currentPrice)
    {
        var above = new List<(decimal Low, decimal High)>();
        var below = new List<(decimal Low, decimal High)>();
        for (var i = 1; i < candles.Count; i++)
        {
            if (candles[i].Low > candles[i - 1].High && !IsGapFilled(candles, i, candles[i - 1].High, true))
                above.Add((candles[i - 1].High, candles[i].Low));
            if (candles[i].High < candles[i - 1].Low && !IsGapFilled(candles, i, candles[i - 1].Low, false))
                below.Add((candles[i].High, candles[i - 1].Low));
        }
        return (
            above.Where(g => g.Low > currentPrice).OrderBy(g => g.Low).Select(g => g.Low).FirstOrDefaultValue(),
            below.Where(g => g.High < currentPrice).OrderByDescending(g => g.High).Select(g => g.High).FirstOrDefaultValue());
    }

    private static bool IsGapFilled(IReadOnlyList<ChannelCandle> candles, int start, decimal boundary, bool gapUp)
        => Enumerable.Range(start + 1, candles.Count - start - 1)
            .Any(i => gapUp ? candles[i].Low <= boundary : candles[i].High >= boundary);

    private sealed record ChannelCandidate(decimal LowerSlope, decimal LowerIntercept, decimal UpperSlope, decimal UpperIntercept, int Quality, List<int> ConfirmedTouches, List<ChannelTouchDetail> TouchDetails);
}

public sealed record ChannelCandle(DateTime Date, decimal Open, decimal High, decimal Low, decimal Close);
public sealed record ChannelTouchDetail(int TouchNumber, DateTime TouchDate, decimal RailPrice, decimal ActualLow, decimal BounceATR, bool ConfirmedBounce);

public sealed record ChannelAnalysisResult(
    ChannelDirection Direction,
    decimal Slope,
    decimal LowerRailCurrent,
    decimal UpperRailCurrent,
    int Quality,
    int ConfirmedLowerTouches,
    DateTime? LastLowerTouchDate,
    decimal DistanceToLowerRailPercent,
    decimal DistanceToLowerRailAtr,
    ChannelState State,
    decimal? NearestOpenGapAbove,
    decimal? NearestOpenGapBelow,
    decimal? DistanceToGapAbovePercent,
    decimal? DistanceToGapBelowPercent,
    IReadOnlyList<ChannelTouchDetail> TouchDetails)
{
    public static ChannelAnalysisResult None { get; } = new(
            ChannelDirection.NONE, 0, 0, 0, 0, 0, null, 0, 0, ChannelState.NONE, null, null, null, null, []);
}

internal static class EnumerableExtensions
{
    public static decimal? FirstOrDefaultValue(this IEnumerable<decimal> values)
    {
        using var iterator = values.GetEnumerator();
        return iterator.MoveNext() ? iterator.Current : null;
    }
}