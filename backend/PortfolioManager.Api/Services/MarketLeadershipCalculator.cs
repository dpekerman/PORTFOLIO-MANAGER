namespace PortfolioManager.Api.Services;

public sealed record MarketLeadershipAnalysis(
    bool HasTechnicalData,
    decimal CurrentPrice,
    decimal DayReturnPct,
    decimal FiveDayReturnPct,
    decimal TwentyDayReturnPct,
    decimal PreviousFiveDayReturnPct,
    decimal PreviousTwentyDayReturnPct,
    decimal Sma50,
    decimal Sma200,
    string TrendState,
    string MomentumState,
    string MaStructure,
    string MaBadge,
    string? LastCross,
    DateOnly? LastCrossDate,
    int? LastCrossTradingDaysAgo,
    string MomentumReason,
    string LeadershipSignal,
    string LeadershipReason);

public static class MarketLeadershipCalculator
{
    private const int MinimumCloseCount = 200;

    public static MarketLeadershipAnalysis Analyze(IReadOnlyList<decimal> closes)
        => Analyze(closes, null);

    public static MarketLeadershipAnalysis Analyze(IReadOnlyList<MarketDailyClose> history)
        => Analyze(history.Select(item => item.Close).ToList(), history.Select(item => item.Date).ToList());

    private static MarketLeadershipAnalysis Analyze(IReadOnlyList<decimal> closes, IReadOnlyList<DateOnly>? dates)
    {
        if (closes.Count < MinimumCloseCount || closes.Any(close => close <= 0))
            return Unavailable(closes.LastOrDefault());

        var last = closes.Count - 1;
        var price = closes[last];
        var sma50 = closes.Skip(last - 49).Take(50).Average();
        var sma200 = closes.Skip(last - 199).Take(200).Average();
        var day = Return(closes[last], closes[last - 1]);
        var fiveDay = Return(closes[last], closes[last - 5]);
        var previousFiveDay = Return(closes[last - 5], closes[last - 10]);
        var twentyDay = Return(closes[last], closes[last - 20]);
        var previousTwentyDay = Return(closes[last - 20], closes[last - 40]);
        var trend = DetermineTrend(price, sma50, sma200);
        var momentum = ClassifyMomentum(fiveDay, previousFiveDay, twentyDay, previousTwentyDay);
        var cross = FindLastCross(closes, dates);
        var nearCross = DetermineNearCross(closes, sma50, sma200);
        var maStructure = DetermineMaStructure(price, sma50, sma200);
        var maBadge = cross.TradingDaysAgo is <= 20
            ? cross.Type == "Golden Cross" ? "GOLDEN CROSS" : "DEATH CROSS"
            : nearCross ?? maStructure;
        var signal = ClassifySignal(price, sma50, sma200, fiveDay, twentyDay, previousTwentyDay, trend, momentum);

        return new MarketLeadershipAnalysis(
            true,
            price,
            day,
            fiveDay,
            twentyDay,
            previousFiveDay,
            previousTwentyDay,
            sma50,
            sma200,
            trend,
            momentum,
            maStructure,
            maBadge,
            cross.Type,
            cross.Date,
            cross.TradingDaysAgo,
            MomentumReason(momentum),
            signal,
            LeadershipReason(signal));
    }

    private static MarketLeadershipAnalysis Unavailable(decimal price) => new(
        false, price, 0m, 0m, 0m, 0m, 0m, 0m, 0m,
        "Unavailable", "Unavailable", "Unavailable", "Unavailable", null, null, null,
        "Technical history is unavailable.", "Neutral", "Technical history is unavailable.");

    private static decimal Return(decimal endingClose, decimal startingClose) =>
        Math.Round(((endingClose / startingClose) - 1m) * 100m, 2);

    private static string DetermineTrend(decimal price, decimal sma50, decimal sma200) =>
        price > sma50 && sma50 > sma200 ? "Bullish" :
        price > sma50 ? "Recovering" :
        price > sma200 ? "Constructive" :
        sma50 < sma200 ? "Bearish" : "Constructive";

    public static string ClassifyMomentum(decimal fiveDay, decimal previousFiveDay, decimal twentyDay, decimal previousTwentyDay) =>
        fiveDay > 0m && fiveDay > previousFiveDay && twentyDay > previousTwentyDay ? "Accelerating" :
        fiveDay > 0m && twentyDay > 0m ? "Positive" :
        fiveDay < 0m && twentyDay < 0m ? "Declining" :
        twentyDay > 0m && (fiveDay < 0m || fiveDay < previousFiveDay) ? "Weakening" : "Neutral";

    private static string DetermineMaStructure(decimal price, decimal sma50, decimal sma200) =>
        price > sma50 && sma50 > sma200 ? "P > 50 > 200" :
        price > sma200 && sma200 > sma50 ? "P > 200 > 50" :
        sma200 > price && price > sma50 ? "200 > P > 50" :
        sma50 > price && price > sma200 ? "50 > P > 200" :
        sma50 > sma200 ? "50 > 200 > P" : "200 > 50 > P";

    private static (string? Type, DateOnly? Date, int? TradingDaysAgo) FindLastCross(
        IReadOnlyList<decimal> closes,
        IReadOnlyList<DateOnly>? dates)
    {
        for (var index = closes.Count - 1; index >= 200; index--)
        {
            var prior50 = closes.Skip(index - 50).Take(50).Average();
            var prior200 = closes.Skip(index - 200).Take(200).Average();
            var current50 = closes.Skip(index - 49).Take(50).Average();
            var current200 = closes.Skip(index - 199).Take(200).Average();
            var type = prior50 <= prior200 && current50 > current200 ? "Golden Cross"
                : prior50 >= prior200 && current50 < current200 ? "Death Cross" : null;
            if (type is not null)
                return (type, dates?[index], closes.Count - 1 - index);
        }
        return (null, null, null);
    }

    private static string? DetermineNearCross(IReadOnlyList<decimal> closes, decimal sma50, decimal sma200)
    {
        if (closes.Count < 201 || sma200 == 0m || Math.Abs(sma50 - sma200) / sma200 > 0.02m)
            return null;

        var last = closes.Count - 1;
        var previous50 = closes.Skip(last - 50).Take(50).Average();
        var previous200 = closes.Skip(last - 200).Take(200).Average();
        return sma50 < sma200 && sma50 > previous50 && previous50 <= previous200 ? "50 ↑ near 200"
            : sma50 > sma200 && sma50 < previous50 && previous50 >= previous200 ? "50 ↓ near 200"
            : null;
    }

    public static string ClassifySignal(
        decimal price,
        decimal sma50,
        decimal sma200,
        decimal fiveDay,
        decimal twentyDay,
        decimal previousTwentyDay,
        string trend,
        string momentum) =>
        price > sma50 && fiveDay > 0m && twentyDay > previousTwentyDay
            && momentum == "Accelerating" && trend != "Bearish" ? "Emerging" :
        price > sma50 && sma50 > sma200 && fiveDay > 0m && twentyDay > 0m
            && (momentum == "Accelerating" || momentum == "Positive") ? "Leading" :
        price > sma200 && fiveDay < 0m && twentyDay > 0m
            && momentum == "Weakening" && trend is "Bullish" or "Constructive" or "Recovering" ? "Cooling" :
        momentum == "Declining" && (price < sma50 || sma50 < sma200) ? "Weak" : "Neutral";

    private static string MomentumReason(string momentum) => momentum switch
    {
        "Accelerating" => "5D return is positive and both 5D and 20D returns improved versus their prior periods.",
        "Positive" => "5D and 20D returns are positive but acceleration conditions are not all met.",
        "Weakening" => "20D return remains positive while 5D momentum is negative or deteriorating.",
        "Declining" => "Both 5D and 20D returns are negative.",
        _ => "Short-term and longer-term returns are mixed or near flat.",
    };

    private static string LeadershipReason(string signal) => signal switch
    {
        "Emerging" => "Price is above SMA50 with positive, accelerating short-term momentum and improving 20D return.",
        "Leading" => "Price is above both moving averages with positive 5D and 20D returns.",
        "Cooling" => "Longer-term structure is constructive but recent momentum is weakening.",
        "Weak" => "Price structure is bearish or both 5D and 20D momentum are declining.",
        _ => "No leadership condition is currently dominant.",
    };
}