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
    public const decimal DefaultWedgeContractionThreshold = 0.30m;

    public static bool IsValidFallingWedge(decimal upperSlope, decimal lowerSlope, decimal startWidth, decimal currentWidth, decimal threshold = DefaultWedgeContractionThreshold) =>
        upperSlope < 0m && lowerSlope < 0m && Math.Abs(upperSlope) > Math.Abs(lowerSlope)
        && MeetsWedgeContraction(startWidth, currentWidth, threshold);

    public static bool IsValidRisingWedge(decimal upperSlope, decimal lowerSlope, decimal startWidth, decimal currentWidth, decimal threshold = DefaultWedgeContractionThreshold) =>
        upperSlope > 0m && lowerSlope > 0m && lowerSlope > upperSlope
        && MeetsWedgeContraction(startWidth, currentWidth, threshold);

    public static bool MeetsWedgeContraction(decimal startWidth, decimal currentWidth, decimal threshold = DefaultWedgeContractionThreshold) =>
        startWidth > 0m && currentWidth > 0m && 1m - currentWidth / startWidth >= threshold;

    public static bool IsFallingWedgeBreakout(decimal close, decimal upperTrendline, decimal atr) =>
        close > upperTrendline + 0.25m * atr;

    public static bool IsRisingWedgeBreakdown(decimal close, decimal lowerTrendline, decimal atr) =>
        close < lowerTrendline - 0.25m * atr;

    public static PriceStructureResult AnalyzePriceStructure(
        IReadOnlyList<ChannelCandle> candles,
        decimal atr,
        decimal ema9,
        string momentum,
        decimal volumeRatio20,
        decimal contractionThreshold = DefaultWedgeContractionThreshold)
    {
        if (candles.Count < 30 || atr <= 0m) return PriceStructureResult.None;
        var currentIndex = candles.Count - 1;
        var bestWedge = BuildBestWedgeCandidate(candles, atr, ema9, momentum, volumeRatio20, contractionThreshold);
        var channel = new ChannelAnalysisService().Analyze(candles, atr, candles[^1].Close);
        var keyLevel = SelectKeyLevel(candles, atr, bestWedge, channel);
        PriceStructureResult result;
        if (bestWedge is null)
        {
            if (channel.Direction != ChannelDirection.NONE)
                result = BuildChannelResult(channel, atr, ema9, volumeRatio20, keyLevel);
            else
                result = keyLevel is null ? PriceStructureResult.None : BuildKeyLevelOnlyResult(candles, atr, ema9, volumeRatio20, keyLevel);
        }
        else
            result = BuildWedgeResult(candles, atr, ema9, volumeRatio20, bestWedge, keyLevel, currentIndex);

        return WithMarketContext(result, candles, bestWedge, channel);
    }

    private static PriceStructureResult WithMarketContext(
        PriceStructureResult result,
        IReadOnlyList<ChannelCandle> candles,
        WedgeCandidate? wedge,
        ChannelAnalysisResult channel)
    {
        var current = candles[^1];
        var zoneHalfWidth = result.KeyLevelPrice.HasValue && result.KeyLevelConfluenceCount > 1 ? result.Atr * 0.25m : 0m;
        return result with
        {
            PatternHorizon = wedge is null ? result.PrimaryPatternType == "RISING_CHANNEL" ? "STRUCTURAL" : "NONE" : wedge.Tight ? "TIGHT" : "STRUCTURAL",
            PatternLookbackSessions = result.PrimaryPatternHorizon,
            KeyLevelLow = result.KeyLevelLow ?? result.KeyLevelPrice - zoneHalfWidth,
            KeyLevelHigh = result.KeyLevelHigh ?? result.KeyLevelPrice + zoneHalfWidth,
            DailyHigh = current.High,
            DailyLow = current.Low,
            EodClose = current.Close,
            ChannelTouchDetails = channel.TouchDetails,
            CalculatedAt = current.Date
        };
    }

    private static WedgeCandidate? BuildBestWedgeCandidate(
        IReadOnlyList<ChannelCandle> candles,
        decimal atr,
        decimal ema9,
        string momentum,
        decimal volumeRatio20,
        decimal structuralContractionThreshold)
    {
        var candidates = new List<WedgeCandidate>();
        foreach (var window in new[] { 15, 20, 30, 40 })
        {
            var candidate = BuildWedgeCandidate(candles, atr, ema9, momentum, volumeRatio20, window, true, 4, 1.0m, 0.40m, 10);
            if (candidate is not null) candidates.Add(candidate);
        }

        foreach (var window in new[] { 60, 126, 250 })
        {
            var candidate = BuildWedgeCandidate(candles, atr, ema9, momentum, volumeRatio20, window, false, 10, 1.5m, structuralContractionThreshold, 15);
            if (candidate is not null) candidates.Add(candidate);
        }

        return candidates
            .OrderByDescending(candidate => candidate.IsActive ? 1 : 0)
            .ThenByDescending(candidate => candidate.Quality)
            .ThenByDescending(candidate => candidate.Contraction)
            .ThenBy(candidate => candidate.DaysToApex > candidate.NearApexDays * 6 ? 1 : 0)
            .ThenBy(candidate => candidate.Horizon)
            .FirstOrDefault();
    }

    private static WedgeCandidate? BuildWedgeCandidate(
        IReadOnlyList<ChannelCandle> candles,
        decimal atr,
        decimal ema9,
        string momentum,
        decimal volumeRatio20,
        int window,
        bool tight,
        int touchSpacingDays,
        decimal moveAwayAtr,
        decimal minimumContraction,
        int nearApexDays)
    {
        if (candles.Count < window || window < 10) return null;
        var start = Math.Max(0, candles.Count - window);
        var rangeCount = candles.Count - start - 4;
        if (rangeCount <= 0) return null;
        var highs = Enumerable.Range(start + 2, rangeCount).Where(i => IsPivotHigh(candles, i)).ToList();
        var lows = Enumerable.Range(start + 2, rangeCount).Where(i => IsPivotLow(candles, i)).ToList();
        if (highs.Count < 2 || lows.Count < 2) return null;

        var upper = FitRail(highs, i => candles[i].High);
        var lower = FitRail(lows, i => candles[i].Low);
        var currentIndex = candles.Count - 1;
        var startWidth = (upper.Slope * start + upper.Intercept) - (lower.Slope * start + lower.Intercept);
        var currentUpper = upper.Slope * currentIndex + upper.Intercept;
        var currentLower = lower.Slope * currentIndex + lower.Intercept;
        var currentWidth = currentUpper - currentLower;
        if (startWidth <= 0m || currentWidth <= 0m) return null;

        var contraction = 1m - currentWidth / startWidth;
        if (contraction < minimumContraction) return null;
        var falling = IsValidFallingWedge(upper.Slope, lower.Slope, startWidth, currentWidth, minimumContraction);
        var rising = IsValidRisingWedge(upper.Slope, lower.Slope, startWidth, currentWidth, minimumContraction);
        if (!falling && !rising) return null;

        var slopeDifference = upper.Slope - lower.Slope;
        if (slopeDifference == 0m) return null;
        var apexIndex = (lower.Intercept - upper.Intercept) / slopeDifference;
        var daysToApex = (int)Math.Round(apexIndex - currentIndex);
        if (daysToApex < 0) return null;

        var upperFitQuality = CalculateFitQuality(highs, i => candles[i].High, upper.Slope, upper.Intercept);
        var lowerFitQuality = CalculateFitQuality(lows, i => candles[i].Low, lower.Slope, lower.Intercept);
        var independentHighs = FilterIndependentTouches(highs, candles, upper.Slope, upper.Intercept, atr, TouchSide.Upper, touchSpacingDays, moveAwayAtr);
        var independentLows = FilterIndependentTouches(lows, candles, lower.Slope, lower.Intercept, atr, TouchSide.Lower, touchSpacingDays, moveAwayAtr);
        if (independentHighs.Count < 2 || independentLows.Count < 2) return null;

        var railViolationCount = CountRailViolations(candles, start, currentIndex, upper, lower, atr);
        var quality = CalculateWedgeQuality(upperFitQuality, lowerFitQuality, independentHighs.Count, independentLows.Count,
            contraction, daysToApex, railViolationCount, upper.Slope, lower.Slope, falling);
        if (quality < 70) return null;

        var close = candles[^1].Close;
        var state = falling && IsFallingWedgeBreakout(close, currentUpper, atr) && close > ema9 && momentum is "Accelerating" or "Positive" ? "BREAKOUT"
            : rising && IsRisingWedgeBreakdown(close, currentLower, atr) ? "BREAKDOWN"
            : daysToApex <= nearApexDays ? "NEAR_APEX"
            : daysToApex <= nearApexDays * 3 ? "TIGHTENING"
            : "DEVELOPING";
        var type = tight
            ? falling ? "TIGHT_FALLING_WEDGE" : "TIGHT_RISING_WEDGE"
            : falling ? "FALLING_WEDGE" : "RISING_WEDGE";
        var label = PatternLabel(type, state);
        var isActive = close <= currentUpper + atr && close >= currentLower - atr;

        return new WedgeCandidate(label, type, state, quality, window, start, upper, lower, currentUpper, currentLower,
            startWidth, currentWidth, contraction, AddTradingDays(candles[^1].Date, daysToApex), daysToApex,
            highs.Count, lows.Count, independentHighs.Count, independentLows.Count,
            Math.Round(upperFitQuality, 3), Math.Round(lowerFitQuality, 3), tight, falling, isActive, nearApexDays);
    }

    public static string ResolveKeyLevelState(decimal levelPrice, decimal atr, decimal currentPrice, decimal dailyHigh, decimal dailyLow, decimal close, decimal previousClose)
    {
        var role = previousClose < levelPrice ? "RESISTANCE" : previousClose > levelPrice ? "SUPPORT" : "TRANSITION";
        return ResolveKeyLevelState(levelPrice, atr, currentPrice, dailyHigh, dailyLow, close, previousClose, role);
    }

    public static string ResolveKeyLevelState(decimal levelPrice, decimal atr, decimal currentPrice, decimal dailyHigh, decimal dailyLow, decimal close, decimal previousClose, string role)
    {
        if (atr <= 0m || levelPrice <= 0m) return "NONE";
        var distanceAtr = (currentPrice - levelPrice) / atr;
        if (Math.Abs(distanceAtr) > 1.0m) return "NONE";
        var resistanceTest = Math.Abs(dailyHigh - levelPrice) <= 0.35m * atr || Math.Abs(close - levelPrice) <= 0.35m * atr;
        var supportTest = Math.Abs(dailyLow - levelPrice) <= 0.35m * atr || Math.Abs(close - levelPrice) <= 0.35m * atr;
        var breakoutTrigger = levelPrice + 0.25m * atr;
        var breakdownTrigger = levelPrice - 0.25m * atr;

        if (role == "SUPPORT" && previousClose > breakoutTrigger && close < levelPrice) return "FAILED_BREAKOUT";
        if (role == "RESISTANCE" && previousClose < breakdownTrigger && close > levelPrice) return "SUPPORT_RECLAIM";
        if (role == "RESISTANCE")
        {
            if (previousClose <= breakoutTrigger && close > breakoutTrigger) return "BREAKOUT_CONFIRMED";
            if (resistanceTest) return "RESISTANCE_TEST";
            if (currentPrice > previousClose && Math.Abs(distanceAtr) <= 1.0m && Math.Abs(distanceAtr) >= 0.35m) return "APPROACHING_RESISTANCE";
        }
        else if (role == "SUPPORT")
        {
            if (previousClose >= breakdownTrigger && close < breakdownTrigger) return "BREAKDOWN_CONFIRMED";
            if (supportTest) return "SUPPORT_TEST";
            if (currentPrice < previousClose && distanceAtr <= 1.0m && distanceAtr >= 0.35m) return "APPROACHING_SUPPORT";
        }

        return "NONE";
    }

    public static bool IsValidTightFallingWedge(decimal upperSlope, decimal lowerSlope, decimal startWidth, decimal currentWidth) =>
        IsValidFallingWedge(upperSlope, lowerSlope, startWidth, currentWidth, 0.40m);

    public static bool IsValidTightRisingWedge(decimal upperSlope, decimal lowerSlope, decimal startWidth, decimal currentWidth) =>
        IsValidRisingWedge(upperSlope, lowerSlope, startWidth, currentWidth, 0.40m);

    public static bool IsConfluenceZone(IReadOnlyList<decimal> levels, decimal atr)
    {
        if (levels.Count < 2 || atr <= 0m) return false;
        var ordered = levels.OrderBy(level => level).ToList();
        for (var i = 0; i < ordered.Count - 1; i++)
        {
            if (ordered[i + 1] - ordered[i] <= 0.5m * atr)
                return true;
        }

        return false;
    }

    private static PriceStructureResult BuildWedgeResult(
        IReadOnlyList<ChannelCandle> candles,
        decimal atr,
        decimal ema9,
        decimal volumeRatio20,
        WedgeCandidate wedge,
        KeyLevelCandidate? keyLevel,
        int currentIndex)
    {
        var level = keyLevel ?? CreateWedgeLevel(candles, atr, wedge);
        return new PriceStructureResult(wedge.Label, wedge.Quality, candles[wedge.StartIndex].Date, wedge.CurrentUpper, wedge.CurrentLower,
            Math.Round(wedge.StartWidth, 2), Math.Round(wedge.CurrentWidth, 2), Math.Round(wedge.Contraction * 100m, 1),
            wedge.ProjectedApexDate, wedge.DaysToApex, wedge.RawPivotHighCount, wedge.RawPivotLowCount, atr, ema9, volumeRatio20,
            wedge.RawPivotHighCount, wedge.RawPivotLowCount, wedge.IndependentUpperTouchCount, wedge.IndependentLowerTouchCount,
            wedge.UpperFitQuality, wedge.LowerFitQuality, wedge.Type, wedge.State, wedge.Quality, wedge.Horizon,
            level.Price, level.Type, level.Role, level.State, level.DistancePercent, level.DistanceAtr,
            level.Quality, level.Sources, level.ConfluenceCount, level.BreakoutTriggerPrice, level.BreakdownTriggerPrice, DateTime.UtcNow)
        {
            KeyLevelLow = level.ZoneLow,
            KeyLevelHigh = level.ZoneHigh,
            KeyLevelOriginalRole = level.OriginalRole
        };
    }

    private static PriceStructureResult BuildKeyLevelOnlyResult(
        IReadOnlyList<ChannelCandle> candles,
        decimal atr,
        decimal ema9,
        decimal volumeRatio20,
        KeyLevelCandidate level) =>
        new(CompactLevelLabel(level), level.Quality, null, 0m, 0m, 0m, 0m, 0m, null, null, 0, 0, atr, ema9, volumeRatio20,
            0, 0, 0, 0, 0m, 0m, "NONE", "NONE", 0, null,
            level.Price, level.Type, level.Role, level.State, level.DistancePercent, level.DistanceAtr,
            level.Quality, level.Sources, level.ConfluenceCount, level.BreakoutTriggerPrice, level.BreakdownTriggerPrice, DateTime.UtcNow)
        {
            KeyLevelLow = level.ZoneLow,
            KeyLevelHigh = level.ZoneHigh,
            KeyLevelOriginalRole = level.OriginalRole
        };

    private static PriceStructureResult BuildChannelResult(
        ChannelAnalysisResult channel,
        decimal atr,
        decimal ema9,
        decimal volumeRatio20,
        KeyLevelCandidate? keyLevel)
    {
        var result = FromChannel(channel, atr, ema9, volumeRatio20);
        if (keyLevel is null) return result;
        return result with
        {
            KeyLevelPrice = keyLevel.Price,
            KeyLevelType = keyLevel.Type,
            KeyLevelRole = keyLevel.Role,
            KeyLevelState = keyLevel.State,
            KeyLevelDistancePercent = keyLevel.DistancePercent,
            KeyLevelDistanceAtr = keyLevel.DistanceAtr,
            KeyLevelQuality = keyLevel.Quality,
            KeyLevelSources = keyLevel.Sources,
            KeyLevelConfluenceCount = keyLevel.ConfluenceCount,
            BreakoutTriggerPrice = keyLevel.BreakoutTriggerPrice,
            BreakdownTriggerPrice = keyLevel.BreakdownTriggerPrice,
            KeyLevelLow = keyLevel.ZoneLow,
            KeyLevelHigh = keyLevel.ZoneHigh,
            KeyLevelOriginalRole = keyLevel.OriginalRole
        };
    }

    private static KeyLevelCandidate? SelectKeyLevel(
        IReadOnlyList<ChannelCandle> candles,
        decimal atr,
        WedgeCandidate? wedge,
        ChannelAnalysisResult channel)
    {
        var current = candles[^1];
        var previousClose = candles.Count >= 2 ? candles[^2].Close : current.Close;
        var candidates = BuildLevelCandidates(candles, atr, wedge, channel)
            .Where(level => Math.Abs(level.DistanceAtr ?? decimal.MaxValue) <= 1.0m)
            .ToList();
        if (candidates.Count == 0) return null;

        var best = candidates
            .OrderBy(level => Math.Abs(level.DistanceAtr ?? decimal.MaxValue))
            .ThenByDescending(level => StateRank(level.State))
            .ThenByDescending(level => level.Quality)
            .ThenBy(level => level.Type, StringComparer.Ordinal)
            .First();
        var confluence = candidates
            .Where(level => Math.Abs(level.Price - best.Price) <= 0.5m * atr)
            .ToList();
        var independentSourceCount = confluence.Select(level => SourceFamily(level.Type)).Distinct(StringComparer.Ordinal).Count();
        if (independentSourceCount < 2) return best;

        var price = confluence.Average(level => level.Price);
        var originalRole = confluence.Select(level => level.OriginalRole).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1
            ? confluence[0].OriginalRole
            : "TRANSITION";
        var role = confluence.Select(level => level.Role).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1
            ? confluence[0].Role
            : "TRANSITION";
        var state = ResolveKeyLevelState(price, atr, current.Close, current.High, current.Low, current.Close, previousClose, role);
        role = ResolveCurrentRole(role, state);
        var distancePercent = PercentDifference(current.Close, price);
        var distanceAtr = (current.Close - price) / atr;
        var quality = Math.Clamp(confluence.Max(level => level.Quality) + Math.Min(20, independentSourceCount * 5), 0, 100);
        var roleSuffix = role == "SUPPORT" ? "SUPPORT" : role == "RESISTANCE" ? "RESISTANCE" : "ZONE";
        return new KeyLevelCandidate(price, "CONFLUENCE_ZONE", role, state, Math.Round(distancePercent, 2), Math.Round(distanceAtr, 2), quality,
            confluence.SelectMany(level => level.Sources).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), independentSourceCount,
            price + 0.25m * atr, price - 0.25m * atr, $"CONFLUENCE_{roleSuffix}")
        {
            ZoneLow = confluence.Min(level => level.Price),
            ZoneHigh = confluence.Max(level => level.Price),
            OriginalRole = originalRole
        };
    }

    private static List<KeyLevelCandidate> BuildLevelCandidates(
        IReadOnlyList<ChannelCandle> candles,
        decimal atr,
        WedgeCandidate? wedge,
        ChannelAnalysisResult channel)
    {
        var current = candles[^1];
        var previousClose = candles.Count >= 2 ? candles[^2].Close : current.Close;
        var candidates = new List<KeyLevelCandidate>();

        if (wedge is not null)
        {
            candidates.Add(CreateLevelCandidate("WEDGE_RESISTANCE", "Upper Wedge Resistance", wedge.CurrentUpper, 90, candles, wedge.StartIndex, "RESISTANCE", atr));
            candidates.Add(CreateLevelCandidate("WEDGE_SUPPORT", "Lower Wedge Support", wedge.CurrentLower, 90, candles, wedge.StartIndex, "SUPPORT", atr));
        }

        if (channel.Direction != ChannelDirection.NONE)
        {
            candidates.Add(CreateLevelCandidate("CHANNEL_RAIL", "Upper Channel Rail", channel.UpperRailCurrent, channel.Quality, candles, 0, "RESISTANCE", atr));
            candidates.Add(CreateLevelCandidate("CHANNEL_RAIL", "Lower Channel Rail", channel.LowerRailCurrent, channel.Quality, candles, 0, "SUPPORT", atr));
        }

        var start = Math.Max(0, candles.Count - 60);
        var rangeCount = candles.Count - start - 4;
        if (rangeCount > 0)
        {
            var recentHigh = Enumerable.Range(start + 2, rangeCount)
                .Where(i => IsPivotHigh(candles, i) && IsMeaningfulSwing(candles, i, atr, TouchSide.Upper))
                .OrderByDescending(i => i).FirstOrDefault(-1);
            var recentLow = Enumerable.Range(start + 2, rangeCount)
                .Where(i => IsPivotLow(candles, i) && IsMeaningfulSwing(candles, i, atr, TouchSide.Lower))
                .OrderByDescending(i => i).FirstOrDefault(-1);
            if (recentHigh >= 0) candidates.Add(CreateLevelCandidate("SWING_HIGH", "Swing High", candles[recentHigh].High, 75, candles, recentHigh, "RESISTANCE", atr));
            if (recentLow >= 0) candidates.Add(CreateLevelCandidate("SWING_LOW", "Swing Low", candles[recentLow].Low, 75, candles, recentLow, "SUPPORT", atr));
        }

        var closes = candles.Select(candle => candle.Close).ToList();
        if (closes.Count >= 20) candidates.Add(CreateContextualLevelCandidate("EMA_20", "EMA20", CalculateEma(closes, 20), 68, candles, candles.Count - 20, atr));
        if (closes.Count >= 50) candidates.Add(CreateContextualLevelCandidate("SMA50", "SMA50", closes.TakeLast(50).Average(), 70, candles, candles.Count - 50, atr));
        if (closes.Count >= 200) candidates.Add(CreateContextualLevelCandidate("SMA200", "SMA200", closes.TakeLast(200).Average(), 80, candles, candles.Count - 200, atr));

        foreach (var fib in CalculateFibLevels(candles))
            candidates.Add(CreateContextualLevelCandidate(fib.Type, fib.Source, fib.Price, 72, candles, Math.Max(0, candles.Count - 60), atr));

        var gaps = FindOpenGaps(candles, current.Close);
        if (gaps.Above.HasValue) candidates.Add(CreateLevelCandidate("OPEN_GAP", "Open Gap Above", gaps.Above.Value, 68, candles, 0, "RESISTANCE", atr));
        if (gaps.Below.HasValue) candidates.Add(CreateLevelCandidate("OPEN_GAP", "Open Gap Below", gaps.Below.Value, 68, candles, 0, "SUPPORT", atr));

        return candidates.Where(level => level.Price > 0m).ToList();
    }

    private static KeyLevelCandidate CreateWedgeLevel(IReadOnlyList<ChannelCandle> candles, decimal atr, WedgeCandidate wedge)
    {
        var current = candles[^1];
        var previousClose = candles.Count >= 2 ? candles[^2].Close : current.Close;
        return CreateLevelCandidate(wedge.Falling ? "WEDGE_RESISTANCE" : "WEDGE_SUPPORT",
            wedge.Falling ? "Upper Wedge Resistance" : "Lower Wedge Support",
            wedge.Falling ? wedge.CurrentUpper : wedge.CurrentLower,
            wedge.Quality,
            candles,
            wedge.StartIndex,
            wedge.Falling ? "RESISTANCE" : "SUPPORT",
            atr);
    }

    private static KeyLevelCandidate CreateContextualLevelCandidate(
        string type,
        string source,
        decimal price,
        int quality,
        IReadOnlyList<ChannelCandle> candles,
        int originIndex,
        decimal atr)
    {
        var referenceIndex = Math.Clamp(originIndex, 0, candles.Count - 2);
        var originatingRole = candles[referenceIndex].Close <= price ? "RESISTANCE" : "SUPPORT";
        return CreateLevelCandidate(type, source, price, quality, candles, referenceIndex, originatingRole, atr);
    }

    private static KeyLevelCandidate CreateLevelCandidate(
        string type,
        string source,
        decimal price,
        int quality,
        IReadOnlyList<ChannelCandle> candles,
        int originIndex,
        string originatingRole,
        decimal atr)
    {
        var current = candles[^1];
        var previousClose = candles.Count >= 2 ? candles[^2].Close : current.Close;
        var role = ReplayRole(candles, price, atr, originIndex, originatingRole);
        var state = ResolveKeyLevelState(price, atr, current.Close, current.High, current.Low, current.Close, previousClose, role);
        role = ResolveCurrentRole(role, state);
        return new KeyLevelCandidate(Math.Round(price, 4), type, role, state, Math.Round(PercentDifference(current.Close, price), 2),
            Math.Round((current.Close - price) / atr, 2), Math.Clamp(quality + StateRank(state) * 4, 0, 100), [source], 1,
            Math.Round(price + 0.25m * atr, 4), Math.Round(price - 0.25m * atr, 4), source)
        {
            ZoneLow = Math.Round(price, 4),
            ZoneHigh = Math.Round(price, 4),
            OriginalRole = originatingRole
        };
    }

    public static string ResolveCurrentRole(string roleBeforeCurrentBar, string state) => state switch
    {
        "SUPPORT_RECLAIM" or "BREAKOUT_CONFIRMED" => "SUPPORT",
        "BREAKDOWN_CONFIRMED" or "SUPPORT_BROKEN" or "FAILED_BREAKOUT" => "RESISTANCE",
        _ => roleBeforeCurrentBar,
    };

    public static string ReplayRole(
        IReadOnlyList<ChannelCandle> candles,
        decimal levelPrice,
        decimal atr,
        int originIndex,
        string originatingRole)
    {
        var role = originatingRole;
        var breakoutTrigger = levelPrice + 0.25m * atr;
        var breakdownTrigger = levelPrice - 0.25m * atr;
        for (var index = Math.Max(0, originIndex + 1); index < candles.Count - 1; index++)
        {
            var close = candles[index].Close;
            if (role == "RESISTANCE" && close > breakoutTrigger)
                role = "SUPPORT";
            else if (role == "SUPPORT" && close < breakdownTrigger)
                role = "RESISTANCE";
        }

        return role;
    }

    private static IEnumerable<(string Type, string Source, decimal Price)> CalculateFibLevels(IReadOnlyList<ChannelCandle> candles)
    {
        var lookback = Math.Min(60, candles.Count);
        if (lookback < 10) yield break;
        var recent = candles.TakeLast(lookback).ToList();
        var high = recent.Max(candle => candle.High);
        var low = recent.Min(candle => candle.Low);
        var range = high - low;
        if (range <= 0m || range / high < 0.08m) yield break;
        yield return ("FIB_38_2", "Fib 38.2", Math.Round(high - range * 0.382m, 4));
        yield return ("FIB_50", "Fib 50", Math.Round(high - range * 0.50m, 4));
        yield return ("FIB_61_8", "Fib 61.8", Math.Round(high - range * 0.618m, 4));
    }

    private static decimal CalculateEma(IReadOnlyList<decimal> values, int period)
    {
        var ema = values.Take(period).Average();
        var multiplier = 2m / (period + 1m);
        foreach (var value in values.Skip(period)) ema += (value - ema) * multiplier;
        return ema;
    }

    private static string SourceFamily(string type) => type switch
    {
        "FIB_38_2" or "FIB_50" or "FIB_61_8" => "FIB",
        "SMA50" or "SMA200" or "EMA_20" => "MA",
        "SWING_HIGH" or "SWING_LOW" => "SWING",
        "WEDGE_RESISTANCE" or "WEDGE_SUPPORT" => "WEDGE",
        "CHANNEL_RAIL" => "CHANNEL",
        "OPEN_GAP" => "GAP",
        _ => type,
    };

    private static string CompactLevelLabel(KeyLevelCandidate level)
    {
        if (level.Type == "CONFLUENCE_ZONE") return level.Role == "SUPPORT" ? "Confluence Support" : level.Role == "RESISTANCE" ? "Confluence Resistance" : "Confluence Zone";
        return level.State switch
        {
            "RESISTANCE_TEST" => $"{level.SourceLabel} Test",
            "SUPPORT_TEST" => $"{level.SourceLabel} Test",
            "APPROACHING_RESISTANCE" => $"Approaching {level.SourceLabel}",
            "APPROACHING_SUPPORT" => $"Approaching {level.SourceLabel}",
            "BREAKOUT_CONFIRMED" => "Breakout Confirmed",
            "BREAKDOWN_CONFIRMED" => "Breakdown Confirmed",
            _ => level.SourceLabel
        };
    }

    private static string PatternLabel(string type, string state)
    {
        var prefix = type switch
        {
            "TIGHT_FALLING_WEDGE" => "Tight Falling Wedge",
            "TIGHT_RISING_WEDGE" => "Tight Rising Wedge",
            "FALLING_WEDGE" => "Falling Wedge",
            "RISING_WEDGE" => "Rising Wedge",
            _ => "—"
        };
        return state switch
        {
            "BREAKOUT" => $"{prefix} Breakout",
            "BREAKDOWN" => $"{prefix} Breakdown",
            "NEAR_APEX" => $"{prefix} Near Apex",
            "TIGHTENING" => $"{prefix} Tightening",
            "FAILED" => $"{prefix} Failed",
            _ => prefix
        };
    }

    private static int StateRank(string state) => state switch
    {
        "BREAKOUT_CONFIRMED" => 7,
        "SUPPORT_RECLAIM" => 6,
        "RESISTANCE_TEST" => 5,
        "SUPPORT_TEST" => 5,
        "APPROACHING_RESISTANCE" => 4,
        "APPROACHING_SUPPORT" => 4,
        "BREAKDOWN_CONFIRMED" => 3,
        "FAILED_BREAKOUT" => 2,
        _ => 1
    };

    private static decimal PercentDifference(decimal value, decimal baseValue) =>
        baseValue == 0m ? 0m : ((value / baseValue) - 1m) * 100m;

    public static PriceStructureResult FromChannel(ChannelAnalysisResult channel, decimal atr, decimal ema9, decimal volumeRatio20)
    {
        var label = channel.State switch
        {
            ChannelState.THIRD_TOUCH_APPROACHING => "3rd Rail Approaching",
            ChannelState.THIRD_TOUCH_TEST => "3rd Rail Test",
            ChannelState.LOWER_RAIL_APPROACHING => "Lower Rail Retest",
            ChannelState.LOWER_RAIL_RETEST => "Lower Rail Retest",
            ChannelState.BOUNCE_CONFIRMED => "Bounce Confirmed",
            ChannelState.CHANNEL_BROKEN => "Channel Broken",
            _ => "—",
        };
        return label == "—" ? PriceStructureResult.None : new PriceStructureResult(
            label, channel.Quality, null, channel.UpperRailCurrent, channel.LowerRailCurrent,
            0m, 0m, 0m, null, null, channel.ConfirmedLowerTouches, 0, atr, ema9, volumeRatio20,
            0, 0, 0, 0, 0m, 0m,
            "RISING_CHANNEL", channel.State.ToString(), channel.Quality, null,
            channel.LowerRailCurrent, "CHANNEL_SUPPORT", "SUPPORT", channel.State.ToString(),
            channel.DistanceToLowerRailPercent, channel.DistanceToLowerRailAtr, channel.Quality,
            ["Channel Support"], 1, channel.UpperRailCurrent + 0.25m * atr, channel.LowerRailCurrent - 0.25m * atr,
            DateTime.UtcNow);
    }

    private static DateTime AddTradingDays(DateTime date, int tradingDays)
    {
        while (tradingDays > 0)
        {
            date = date.AddDays(1);
            if (date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday) tradingDays--;
        }
        return date;
    }

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
        var lowDistanceAtr = (candles[^1].Low - lowerToday) / atr;
        var state = ResolveChannelState(best.ConfirmedTouches.Count, distanceAtr, lowDistanceAtr);

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

    public static ChannelState ResolveChannelState(int confirmedTouchCount, decimal closeDistanceAtr, decimal lowDistanceAtr)
    {
        if (closeDistanceAtr < -0.5m) return ChannelState.CHANNEL_BROKEN;
        if (Math.Abs(lowDistanceAtr) <= 0.35m)
            return confirmedTouchCount >= 3 ? ChannelState.LOWER_RAIL_RETEST : ChannelState.THIRD_TOUCH_TEST;
        if (lowDistanceAtr > 0.35m && lowDistanceAtr <= 1m)
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

    public static int CalculateWedgeQuality(
        decimal upperFitQuality,
        decimal lowerFitQuality,
        int independentUpperTouchCount,
        int independentLowerTouchCount,
        decimal contraction,
        int daysToApex,
        int railViolationCount,
        decimal upperSlope,
        decimal lowerSlope,
        bool fallingWedge)
    {
        var fitScore = (int)Math.Round(Math.Clamp((upperFitQuality + lowerFitQuality) / 2m, 0m, 1m) * 30m);
        var touchScore = CalculateTouchScore(independentUpperTouchCount, independentLowerTouchCount);
        var contractionScore = CalculateContractionScore(contraction);
        var geometryScore = CalculateGeometryScore(upperSlope, lowerSlope, fallingWedge);
        var railScore = railViolationCount switch
        {
            0 => 10,
            <= 3 => 7,
            <= 6 => 4,
            _ => 0
        };
        var apexScore = daysToApex switch
        {
            <= 15 => 10,
            <= 60 => 8,
            <= 120 => 4,
            <= 180 => 2,
            _ => 0
        };

        return Math.Clamp(fitScore + touchScore + contractionScore + geometryScore + railScore + apexScore, 0, 100);
    }

    public static int CalculateWedgeTouchScore(int independentUpperTouchCount, int independentLowerTouchCount) =>
        CalculateTouchScore(independentUpperTouchCount, independentLowerTouchCount);

    public static int CalculateWedgeContractionScore(decimal contraction) => CalculateContractionScore(contraction);

    public static int CalculateWedgeApexScore(int daysToApex) => daysToApex switch
    {
        <= 15 => 10,
        <= 60 => 8,
        <= 120 => 4,
        <= 180 => 2,
        _ => 0
    };

    private static decimal CalculateFitQuality(
        IReadOnlyList<int> indexes,
        Func<int, decimal> value,
        decimal slope,
        decimal intercept)
    {
        if (indexes.Count < 2) return 0m;
        var mean = indexes.Average(value);
        var total = indexes.Sum(i => Square(value(i) - mean));
        if (total == 0m) return 0m;
        var residual = indexes.Sum(i => Square(value(i) - (slope * i + intercept)));
        return Math.Clamp(1m - residual / total, 0m, 1m);
    }

    private static List<int> FilterIndependentTouches(
        IReadOnlyList<int> pivots,
        IReadOnlyList<ChannelCandle> candles,
        decimal slope,
        decimal intercept,
        decimal atr,
        TouchSide side,
        int touchSpacingDays = 10,
        decimal moveAwayAtr = 1.5m)
    {
        var independent = new List<int>();
        foreach (var pivot in pivots)
        {
            var rail = slope * pivot + intercept;
            var pivotPrice = side == TouchSide.Upper ? candles[pivot].High : candles[pivot].Low;
            if (Math.Abs(pivotPrice - rail) > 0.65m * atr) continue;

            if (independent.Count == 0)
            {
                independent.Add(pivot);
                continue;
            }

            var lastTouch = independent[^1];
            if (pivot - lastTouch < touchSpacingDays) continue;
            if (!MovedAwayFromRail(candles, lastTouch, pivot, slope, intercept, atr, side, moveAwayAtr)) continue;
            independent.Add(pivot);
        }

        return independent;
    }

    private static bool MovedAwayFromRail(
        IReadOnlyList<ChannelCandle> candles,
        int previousTouch,
        int nextTouch,
        decimal slope,
        decimal intercept,
        decimal atr,
        TouchSide side,
        decimal moveAwayAtr)
    {
        for (var i = previousTouch + 1; i < nextTouch; i++)
        {
            var rail = slope * i + intercept;
            var distance = side == TouchSide.Upper
                ? rail - candles[i].Low
                : candles[i].High - rail;
            if (distance >= moveAwayAtr * atr) return true;
        }

        return false;
    }

    private static int CountRailViolations(
        IReadOnlyList<ChannelCandle> candles,
        int start,
        int currentIndex,
        (decimal Slope, decimal Intercept) upper,
        (decimal Slope, decimal Intercept) lower,
        decimal atr)
    {
        var count = 0;
        for (var i = start; i < currentIndex; i++)
        {
            var upperRail = upper.Slope * i + upper.Intercept;
            var lowerRail = lower.Slope * i + lower.Intercept;
            if (candles[i].Close > upperRail + 0.75m * atr || candles[i].Close < lowerRail - 0.75m * atr)
                count++;
        }

        return count;
    }

    private static int CalculateTouchScore(int upperTouchCount, int lowerTouchCount)
    {
        var balancedTouchCount = Math.Min(upperTouchCount, lowerTouchCount);
        return balancedTouchCount switch
        {
            >= 4 => 20,
            3 => 15,
            2 => 8,
            _ => 0
        };
    }

    private static int CalculateContractionScore(decimal contraction) => contraction switch
    {
        < 0.30m => 0,
        < 0.40m => 8,
        < 0.50m => 12,
        < 0.65m => 16,
        _ => 20
    };

    private static int CalculateGeometryScore(decimal upperSlope, decimal lowerSlope, bool fallingWedge)
    {
        var upperMagnitude = Math.Abs(upperSlope);
        var lowerMagnitude = Math.Abs(lowerSlope);
        if (upperMagnitude == 0m || lowerMagnitude == 0m) return 0;
        var weaker = fallingWedge ? lowerMagnitude : upperMagnitude;
        var stronger = fallingWedge ? upperMagnitude : lowerMagnitude;
        if (stronger <= weaker) return 0;
        return Math.Clamp((int)Math.Round(5m + Math.Min(1m, weaker / stronger) * 5m), 0, 10);
    }

    private static decimal Square(decimal value) => value * value;

    private static bool IsPivotLow(IReadOnlyList<ChannelCandle> candles, int i)
        => candles[i].Low < candles[i - 1].Low && candles[i].Low < candles[i - 2].Low
        && candles[i].Low < candles[i + 1].Low && candles[i].Low < candles[i + 2].Low;

    private static bool IsPivotHigh(IReadOnlyList<ChannelCandle> candles, int i)
        => candles[i].High > candles[i - 1].High && candles[i].High > candles[i - 2].High
        && candles[i].High > candles[i + 1].High && candles[i].High > candles[i + 2].High;

    private static bool IsMeaningfulSwing(
        IReadOnlyList<ChannelCandle> candles,
        int pivotIndex,
        decimal atr,
        TouchSide side)
    {
        var end = Math.Min(candles.Count - 1, pivotIndex + 15);
        if (end <= pivotIndex) return false;
        var pivotPrice = side == TouchSide.Upper ? candles[pivotIndex].High : candles[pivotIndex].Low;
        return Enumerable.Range(pivotIndex + 1, end - pivotIndex).Any(index =>
            side == TouchSide.Upper
                ? pivotPrice - candles[index].Low >= atr
                : candles[index].High - pivotPrice >= atr);
    }

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

    private sealed record WedgeCandidate(
        string Label,
        string Type,
        string State,
        int Quality,
        int Horizon,
        int StartIndex,
        (decimal Slope, decimal Intercept) Upper,
        (decimal Slope, decimal Intercept) Lower,
        decimal CurrentUpper,
        decimal CurrentLower,
        decimal StartWidth,
        decimal CurrentWidth,
        decimal Contraction,
        DateTime ProjectedApexDate,
        int DaysToApex,
        int RawPivotHighCount,
        int RawPivotLowCount,
        int IndependentUpperTouchCount,
        int IndependentLowerTouchCount,
        decimal UpperFitQuality,
        decimal LowerFitQuality,
        bool Tight,
        bool Falling,
        bool IsActive,
        int NearApexDays);

    private sealed record KeyLevelCandidate(
        decimal Price,
        string Type,
        string Role,
        string State,
        decimal? DistancePercent,
        decimal? DistanceAtr,
        int Quality,
        IReadOnlyList<string> Sources,
        int ConfluenceCount,
        decimal? BreakoutTriggerPrice,
        decimal? BreakdownTriggerPrice,
        string SourceLabel)
    {
        public decimal ZoneLow { get; init; } = Price;
        public decimal ZoneHigh { get; init; } = Price;
        public string OriginalRole { get; init; } = Role;
    }

    private enum TouchSide { Upper, Lower }
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

public sealed record PriceStructureResult(
    string Label,
    int Quality,
    DateTime? PatternStart,
    decimal UpperTrendline,
    decimal LowerTrendline,
    decimal StartWidth,
    decimal CurrentWidth,
    decimal ContractionPercent,
    DateTime? ProjectedApexDate,
    int? TradingDaysToApex,
    int PivotHighs,
    int PivotLows,
    decimal Atr,
    decimal Ema9,
    decimal VolumeRatio20,
    int RawPivotHighCount,
    int RawPivotLowCount,
    int IndependentUpperTouchCount,
    int IndependentLowerTouchCount,
    decimal UpperFitQuality,
    decimal LowerFitQuality,
    string PrimaryPatternType,
    string PrimaryPatternState,
    int PrimaryPatternQuality,
    int? PrimaryPatternHorizon,
    decimal? KeyLevelPrice,
    string KeyLevelType,
    string KeyLevelRole,
    string KeyLevelState,
    decimal? KeyLevelDistancePercent,
    decimal? KeyLevelDistanceAtr,
    int KeyLevelQuality,
    IReadOnlyList<string> KeyLevelSources,
    int KeyLevelConfluenceCount,
    decimal? BreakoutTriggerPrice,
    decimal? BreakdownTriggerPrice,
    DateTime CalculatedAt)
{
    public string Symbol { get; init; } = string.Empty;
    public string PatternHorizon { get; init; } = "NONE";
    public int? PatternLookbackSessions { get; init; }
    public decimal? KeyLevelLow { get; init; }
    public decimal? KeyLevelHigh { get; init; }
    public decimal DailyHigh { get; init; }
    public decimal DailyLow { get; init; }
    public decimal EodClose { get; init; }
    public IReadOnlyList<ChannelTouchDetail> ChannelTouchDetails { get; init; } = [];
    public string KeyLevelOriginalRole { get; init; } = KeyLevelRole;
    public bool HasHardStructuralNegative =>
        PrimaryPatternState is "BREAKDOWN" or "CHANNEL_BROKEN"
        || KeyLevelState is "SUPPORT_BROKEN" or "BREAKDOWN_CONFIRMED" or "FAILED_BREAKOUT";

    public static PriceStructureResult None { get; } = new("—", 0, null, 0m, 0m, 0m, 0m, 0m, null, null, 0, 0, 0m, 0m, 0m, 0, 0, 0, 0, 0m, 0m,
        "NONE", "NONE", 0, null, null, "NONE", "TRANSITION", "NONE", null, null, 0, [], 0, null, null, DateTime.UtcNow);
}