namespace PortfolioManager.Api.Services.Opportunities;

/// <summary>Benchmark resolved for a symbol's sector and analysis market (from SectorBenchmarkMappings).</summary>
public sealed record SectorBenchmarkInfo(string Symbol, string Name, string Currency);

/// <summary>
/// Pure stock-vs-sector-benchmark comparison over completed trading sessions (docs/pm-029-trading-opportunities.md §4).
/// No I/O: callers pass the split-adjusted daily closes, the expected last completed session E and the analysis exchange.
/// </summary>
public static class SectorRelativeCalculator
{
    public static SectorRelativeResult Compute(
        IReadOnlyList<MarketDailyClose>? stockBars,
        IReadOnlyList<MarketDailyClose>? benchmarkBars,
        DateOnly expectedSession,
        MarketExchange exchange,
        string? analysisCurrency,
        SectorBenchmarkInfo? benchmark,
        bool sectorClassified = true)
    {
        SectorRelativeResult All(SectorHeatStatus status) => new(status, benchmark?.Symbol, benchmark?.Name, expectedSession,
            Empty(OpportunityScoringConfig.ShortWindowSessions, status), Empty(OpportunityScoringConfig.LongWindowSessions, status),
            OpportunityScoringConfig.SectorMethodology);

        if (!sectorClassified) return All(SectorHeatStatus.Unclassified);
        if (benchmark is null) return All(SectorHeatStatus.Unmapped);
        if (string.IsNullOrWhiteSpace(analysisCurrency)
            || !string.Equals(analysisCurrency.Trim(), benchmark.Currency.Trim(), StringComparison.OrdinalIgnoreCase))
            return All(SectorHeatStatus.CurrencyMismatch);

        var stock = Normalize(stockBars, expectedSession);
        var bench = Normalize(benchmarkBars, expectedSession);
        if (!stock.ContainsKey(expectedSession) || !bench.ContainsKey(expectedSession))
            return All(SectorHeatStatus.Stale);

        var d20 = Window(stock, bench, expectedSession, exchange, OpportunityScoringConfig.ShortWindowSessions, OpportunityScoringConfig.ShortWindowMaxMissing);
        var d60 = Window(stock, bench, expectedSession, exchange, OpportunityScoringConfig.LongWindowSessions, OpportunityScoringConfig.LongWindowMaxMissing);
        return new SectorRelativeResult(d20.Status, benchmark.Symbol, benchmark.Name, expectedSession, d20, d60, OpportunityScoringConfig.SectorMethodology);
    }

    /// <summary>The session <paramref name="sessions"/> completed trading sessions before <paramref name="end"/> (holidays excluded).</summary>
    public static DateOnly WindowStart(DateOnly end, int sessions, MarketExchange exchange)
    {
        var d = end;
        for (var i = 0; i < sessions; i++) d = TradingCalendar.PreviousTradingDay(d, exchange);
        return d;
    }

    /// <summary>Rounds to 0.1 pp (half away from zero) — the displayed value the neutral band is applied to.</summary>
    public static decimal Display(decimal diffPp) => Math.Round(diffPp, 1, MidpointRounding.AwayFromZero);

    public static (SectorHeatDirection Direction, int Intensity) Classify(decimal displayDiffPp)
    {
        var abs = Math.Abs(displayDiffPp);
        if (abs < OpportunityScoringConfig.NeutralBandPp) return (SectorHeatDirection.Neutral, 0);
        var intensity = abs >= OpportunityScoringConfig.Intensity3Pp ? 3 : abs >= OpportunityScoringConfig.Intensity2Pp ? 2 : 1;
        return (displayDiffPp > 0 ? SectorHeatDirection.Positive : SectorHeatDirection.Negative, intensity);
    }

    private static SectorWindowResult Window(
        SortedDictionary<DateOnly, decimal> stock, SortedDictionary<DateOnly, decimal> bench,
        DateOnly end, MarketExchange exchange, int sessions, int maxMissing)
    {
        var start = WindowStart(end, sessions, exchange);
        var expected = sessions + 1;
        int Present(SortedDictionary<DateOnly, decimal> s) =>
            s.Keys.Count(d => d >= start && d <= end && TradingCalendar.IsTradingDay(d, exchange));
        var stockObs = Present(stock);
        var benchObs = Present(bench);

        SectorWindowResult Fail(SectorHeatStatus status) => new(sessions, status, start, end, null, null, null, null,
            SectorHeatDirection.None, 0, stockObs, benchObs, expected);

        if (!stock.ContainsKey(start) || !bench.ContainsKey(start)
            || expected - stockObs > maxMissing || expected - benchObs > maxMissing)
            return Fail(SectorHeatStatus.InsufficientObservations);
        if (HasCorporateActionMove(stock, start, end) || HasCorporateActionMove(bench, start, end))
            return Fail(SectorHeatStatus.PossibleCorporateAction);

        var stockReturn = (stock[end] / stock[start] - 1m) * 100m;
        var benchReturn = (bench[end] / bench[start] - 1m) * 100m;
        var diff = stockReturn - benchReturn;
        var display = Display(diff);
        var (direction, intensity) = Classify(display);
        return new SectorWindowResult(sessions, SectorHeatStatus.Matched, start, end, stockReturn, benchReturn, diff, display,
            direction, intensity, stockObs, benchObs, expected);
    }

    private static bool HasCorporateActionMove(SortedDictionary<DateOnly, decimal> series, DateOnly start, DateOnly end)
    {
        decimal? prev = null;
        foreach (var (date, close) in series)
        {
            if (date < start) continue;
            if (date > end) break;
            if (prev is { } p && Math.Abs(close / p - 1m) * 100m > OpportunityScoringConfig.CorporateActionMovePct) return true;
            prev = close;
        }
        return false;
    }

    /// <summary>Drops bars after E (intraday/partial sessions) and non-positive closes; one close per date (last wins).</summary>
    private static SortedDictionary<DateOnly, decimal> Normalize(IReadOnlyList<MarketDailyClose>? bars, DateOnly expectedSession)
    {
        var map = new SortedDictionary<DateOnly, decimal>();
        if (bars is null) return map;
        foreach (var b in bars)
            if (b.Date <= expectedSession && b.Close > 0m) map[b.Date] = b.Close;
        return map;
    }

    private static SectorWindowResult Empty(int sessions, SectorHeatStatus status) =>
        new(sessions, status, null, null, null, null, null, null, SectorHeatDirection.None, 0, 0, 0, sessions + 1);
}
