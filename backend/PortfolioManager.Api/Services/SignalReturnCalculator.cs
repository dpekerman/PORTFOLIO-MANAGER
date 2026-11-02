using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

/// <summary>
/// Pure signal-performance math over a daily-close series. Trading sessions are exactly the series
/// rows, so weekends and exchange holidays (TSX or U.S.) are handled by the data itself. Only closes
/// after the signal date are used for forward horizons (no look-ahead), and a missing horizon is
/// <c>Pending</c> or <c>NA</c> — never zero.
/// </summary>
public static class SignalReturnCalculator
{
    public const string Complete = "Complete";
    public const string Pending = "Pending";
    public const string NotAvailable = "NA";

    public sealed record Result(
        SignalHorizonDto Return5D,
        SignalHorizonDto Return20D,
        SignalHorizonDto Return1M,
        SignalHorizonDto Today);

    public static Result Compute(DateOnly signalDate, decimal signalPrice, IReadOnlyList<MarketDailyClose>? closes)
    {
        if (signalPrice <= 0m || closes is null || closes.Count == 0)
            return new Result(Na(), Na(), Na(), Na());

        var ordered = closes.OrderBy(c => c.Date).ToList();
        // History that starts after the signal cannot support any evaluation.
        if (ordered[0].Date > signalDate.AddDays(7))
            return new Result(Na(), Na(), Na(), Na());

        var after = ordered.Where(c => c.Date > signalDate).ToList();
        var latest = ordered[^1];

        var oneMonthTarget = signalDate.AddMonths(1);
        var oneMonth = latest.Date < oneMonthTarget
            ? Pend()
            : Horizon(signalPrice, ordered.First(c => c.Date >= oneMonthTarget));

        var today = latest.Date < signalDate ? Pend() : Horizon(signalPrice, latest);

        return new Result(
            after.Count >= 5 ? Horizon(signalPrice, after[4]) : Pend(),
            after.Count >= 20 ? Horizon(signalPrice, after[19]) : Pend(),
            oneMonth,
            today);
    }

    public static decimal ReturnPct(decimal signalPrice, decimal evalPrice) =>
        Math.Round((evalPrice / signalPrice - 1m) * 100m, 2);

    private static SignalHorizonDto Horizon(decimal signalPrice, MarketDailyClose close) =>
        new(ReturnPct(signalPrice, close.Close), close.Date.ToString("yyyy-MM-dd"), close.Close, Complete);

    private static SignalHorizonDto Pend() => new(null, null, null, Pending);
    private static SignalHorizonDto Na() => new(null, null, null, NotAvailable);
}
