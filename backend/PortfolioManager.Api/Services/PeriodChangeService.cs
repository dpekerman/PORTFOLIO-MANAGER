using Microsoft.Extensions.Caching.Memory;

namespace PortfolioManager.Api.Services;

/// <summary>5-trading-day and 1-calendar-month percentage change for one symbol. Null = not enough history.</summary>
public sealed record PeriodChangeDto(string Symbol, decimal? Change5DPct, decimal? Change1MPct);

public interface IPeriodChangeService
{
    Task<IReadOnlyList<PeriodChangeDto>> GetChangesAsync(IEnumerable<string> symbols, CancellationToken ct = default);
}

/// <summary>
/// Derives 5D / 1M change % from Yahoo daily closes. Results are cached briefly per symbol so grid
/// refreshes do not re-hit Yahoo; fetches are throttled to respect the existing Yahoo rate-limit posture.
/// </summary>
public sealed class PeriodChangeService(IMarketDataProvider marketData, IMemoryCache cache) : IPeriodChangeService
{
    private const int MaxSymbols = 100;
    private const int MaxParallelFetches = 4;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);

    public async Task<IReadOnlyList<PeriodChangeDto>> GetChangesAsync(
        IEnumerable<string> symbols, CancellationToken ct = default)
    {
        var distinct = symbols
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim().ToUpperInvariant())
            .Distinct()
            .Take(MaxSymbols)
            .ToList();

        using var gate = new SemaphoreSlim(MaxParallelFetches);
        var tasks = distinct.Select(async symbol =>
        {
            var key = $"period-change:{symbol}";
            if (cache.TryGetValue(key, out PeriodChangeDto? hit) && hit is not null) return hit;

            await gate.WaitAsync(ct);
            try
            {
                var closes = await marketData.GetDailyClosesAsync(symbol, ct);
                var dto = Compute(symbol, closes);
                // Do not cache a failed/empty lookup so the next refresh retries.
                if (closes is { Count: > 0 }) cache.Set(key, dto, CacheTtl);
                return dto;
            }
            finally
            {
                gate.Release();
            }
        });

        return await Task.WhenAll(tasks);
    }

    /// <summary>
    /// 5D: latest close vs the close 5 trading sessions earlier.
    /// 1M: latest close vs the last close on or before the same calendar day one month earlier.
    /// </summary>
    public static PeriodChangeDto Compute(string symbol, IReadOnlyList<MarketDailyClose>? closes)
    {
        if (closes is null || closes.Count == 0) return new PeriodChangeDto(symbol, null, null);

        var ordered = closes.OrderBy(c => c.Date).ToList();
        var latest = ordered[^1];

        decimal? fiveDay = null;
        if (ordered.Count > 5)
            fiveDay = Pct(latest.Close, ordered[^6].Close);

        decimal? oneMonth = null;
        var target = latest.Date.AddMonths(-1);
        var baseline = ordered.LastOrDefault(c => c.Date <= target);
        if (baseline is not null) oneMonth = Pct(latest.Close, baseline.Close);

        return new PeriodChangeDto(symbol, fiveDay, oneMonth);
    }

    private static decimal? Pct(decimal latest, decimal prior) =>
        prior > 0m ? Math.Round((latest / prior - 1m) * 100m, 2) : null;
}
