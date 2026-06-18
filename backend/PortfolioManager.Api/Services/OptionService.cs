using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public interface IOptionService
{
    Task<IReadOnlyList<OptionItemDto>> GetAllAsync(CancellationToken ct = default);
    Task<OptionItemDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<OptionItemDto> AddAsync(AddOptionItemRequest request, CancellationToken ct = default);
    Task<OptionItemDto?> UpdateAsync(int id, UpdateOptionItemRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
    Task<OptionTechnicalDataDto?> GetTechnicalDataAsync(string symbol, CancellationToken ct = default);
}

public sealed class OptionService(AppDbContext db, HttpClient http, ILogger<OptionService> logger) : IOptionService
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public async Task<IReadOnlyList<OptionItemDto>> GetAllAsync(CancellationToken ct = default)
    {
        var items = await db.OptionItems
            .AsNoTracking()
            .OrderBy(x => x.AddedAt)
            .ToListAsync(ct);
        return items.Select(ToDto).ToList();
    }

    public async Task<OptionItemDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var item = await db.OptionItems.FindAsync([id], ct);
        return item is null ? null : ToDto(item);
    }

    public async Task<OptionItemDto> AddAsync(AddOptionItemRequest request, CancellationToken ct = default)
    {
        var item = new OptionItem
        {
            UnderlyingTicker  = request.UnderlyingTicker.ToUpperInvariant(),
            PositionType      = request.PositionType.ToUpperInvariant(),
            ExpirationDate    = request.ExpirationDate,
            Strike            = request.Strike,
            Premium           = request.Premium,
            NumberOfContracts = request.NumberOfContracts,
            MarketPrice       = request.MarketPrice,
            AddedAt           = DateTime.UtcNow
        };
        db.OptionItems.Add(item);
        await db.SaveChangesAsync(ct);
        return ToDto(item);
    }

    public async Task<OptionItemDto?> UpdateAsync(int id, UpdateOptionItemRequest request, CancellationToken ct = default)
    {
        var item = await db.OptionItems.FindAsync([id], ct);
        if (item is null) return null;
        item.UnderlyingTicker  = request.UnderlyingTicker.ToUpperInvariant();
        item.PositionType      = request.PositionType.ToUpperInvariant();
        item.ExpirationDate    = request.ExpirationDate;
        item.Strike            = request.Strike;
        item.Premium           = request.Premium;
        item.NumberOfContracts = request.NumberOfContracts;
        item.MarketPrice       = request.MarketPrice;
        await db.SaveChangesAsync(ct);
        return ToDto(item);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var item = await db.OptionItems.FindAsync([id], ct);
        if (item is null) return false;
        db.OptionItems.Remove(item);
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Fetches 1y of daily OHLC data for <paramref name="symbol"/> from Yahoo Finance
    /// and returns the technical indicators needed for the frontend option-state rules engine.
    /// </summary>
    public async Task<OptionTechnicalDataDto?> GetTechnicalDataAsync(string symbol, CancellationToken ct = default)
    {
        try
        {
            var url  = $"v8/finance/chart/{Uri.EscapeDataString(symbol)}?interval=1d&range=1y";
            var resp = await http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync(ct);
            var data = JsonSerializer.Deserialize<YahooChartResponse>(json, _json);
            var chartResult = data?.Chart?.Result?.FirstOrDefault();
            if (chartResult is null) return null;

            var qd = chartResult.Indicators?.Quote?.FirstOrDefault();
            if (qd is null) return null;

            var closes  = qd.Close.Where(c => c.HasValue).Select(c => c!.Value).ToList();
            var highs   = qd.High.Where(h => h.HasValue).Select(h => h!.Value).ToList();
            var lows    = qd.Low.Where(l => l.HasValue).Select(l => l!.Value).ToList();

            if (closes.Count < 22) return null;

            decimal currentPrice  = closes.Last();
            decimal previousClose = closes.Count >= 2 ? closes[^2] : currentPrice;
            decimal yesterdayHigh = highs.Count >= 2 ? highs[^2] : highs.Last();
            decimal yesterdayLow  = lows.Count >= 2 ? lows[^2] : lows.Last();

            decimal rsi14              = CalculateRsi(closes, 14);
            var (rsiSignal, rsigAvail) = CalculateRsiSignal(closes);
            decimal sma20              = CalculateSma(closes, 20);
            decimal sma50              = CalculateSma(closes, 50);
            decimal ema21              = CalculateEma(closes, 21);
            decimal atr14              = CalculateAtr(highs, lows, closes, 14);
            var (bbUpper, _, bbLower)  = CalculateBollingerBands(closes, 20);

            return new OptionTechnicalDataDto(
                Symbol            : symbol.ToUpperInvariant(),
                CurrentPrice      : Math.Round(currentPrice, 4),
                PreviousClose     : Math.Round(previousClose, 4),
                YesterdayHigh     : Math.Round(yesterdayHigh, 4),
                YesterdayLow      : Math.Round(yesterdayLow, 4),
                Rsi14             : Math.Round(rsi14, 2),
                RsiSignal9        : Math.Round(rsiSignal, 2),
                RsiSignalAvailable: rsigAvail,
                Sma20             : Math.Round(sma20, 4),
                Sma50             : Math.Round(sma50, 4),
                Ema21             : Math.Round(ema21, 4),
                Atr14             : Math.Round(atr14, 4),
                BollingerUpper    : Math.Round(bbUpper, 4),
                BollingerLower    : Math.Round(bbLower, 4));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch technical data for {Symbol}", symbol);
            return null;
        }
    }

    // ── Technical computation helpers ────────────────────────────────────────

    private static decimal CalculateRsi(List<decimal> closes, int period)
    {
        if (closes.Count < period + 1) return 50m;
        decimal avgGain = 0, avgLoss = 0;
        for (int i = 1; i <= period; i++)
        {
            var diff = closes[i] - closes[i - 1];
            if (diff >= 0) avgGain += diff; else avgLoss -= diff;
        }
        avgGain /= period;
        avgLoss /= period;
        for (int i = period + 1; i < closes.Count; i++)
        {
            var diff = closes[i] - closes[i - 1];
            decimal gain = diff >= 0 ? diff : 0;
            decimal loss = diff < 0 ? -diff : 0;
            avgGain = (avgGain * (period - 1) + gain) / period;
            avgLoss = (avgLoss * (period - 1) + loss) / period;
        }
        if (avgLoss == 0) return 100m;
        return 100m - (100m / (1m + avgGain / avgLoss));
    }

    private static List<decimal> CalculateRsiSeries(List<decimal> closes, int period)
    {
        var result = new List<decimal>();
        if (closes.Count < period + 1) return result;
        decimal avgGain = 0, avgLoss = 0;
        for (int i = 1; i <= period; i++)
        {
            var diff = closes[i] - closes[i - 1];
            if (diff >= 0) avgGain += diff; else avgLoss -= diff;
        }
        avgGain /= period;
        avgLoss /= period;
        decimal rs = avgLoss == 0 ? 100m : avgGain / avgLoss;
        result.Add(100m - (100m / (1m + rs)));
        for (int i = period + 1; i < closes.Count; i++)
        {
            var diff = closes[i] - closes[i - 1];
            decimal gain = diff >= 0 ? diff : 0;
            decimal loss = diff < 0 ? -diff : 0;
            avgGain = (avgGain * (period - 1) + gain) / period;
            avgLoss = (avgLoss * (period - 1) + loss) / period;
            rs = avgLoss == 0 ? 100m : avgGain / avgLoss;
            result.Add(100m - (100m / (1m + rs)));
        }
        return result;
    }

    private static (decimal value, bool available) CalculateRsiSignal(List<decimal> closes, int rsiPeriod = 14, int emaPeriod = 9)
    {
        var rsiSeries = CalculateRsiSeries(closes, rsiPeriod);
        if (rsiSeries.Count < emaPeriod) return (0m, false);
        decimal mult = 2m / (emaPeriod + 1);
        decimal ema  = rsiSeries.Take(emaPeriod).Average();
        for (int i = emaPeriod; i < rsiSeries.Count; i++)
            ema = rsiSeries[i] * mult + ema * (1m - mult);
        return (Math.Round(ema, 2), true);
    }

    private static decimal CalculateEma(IReadOnlyList<decimal> values, int period)
    {
        if (values.Count < period) return values[^1];
        decimal mult = 2m / (period + 1);
        decimal ema  = values.Take(period).Average();
        for (int i = period; i < values.Count; i++)
            ema = values[i] * mult + ema * (1 - mult);
        return ema;
    }

    private static decimal CalculateSma(List<decimal> values, int period)
        => values.Count >= period ? values.TakeLast(period).Average() : values.Average();

    private static decimal CalculateAtr(List<decimal> highs, List<decimal> lows, List<decimal> closes, int period)
    {
        if (highs.Count < 2) return 0m;
        var trList = new List<decimal>();
        for (int i = 1; i < highs.Count; i++)
        {
            decimal tr = Math.Max(highs[i] - lows[i],
                         Math.Max(Math.Abs(highs[i] - closes[i - 1]),
                                  Math.Abs(lows[i]  - closes[i - 1])));
            trList.Add(tr);
        }
        if (trList.Count == 0) return 0m;
        decimal atr = trList.Take(period).Average();
        for (int i = period; i < trList.Count; i++)
            atr = (atr * (period - 1) + trList[i]) / period;
        return atr;
    }

    private static (decimal upper, decimal middle, decimal lower) CalculateBollingerBands(List<decimal> closes, int period = 20)
    {
        if (closes.Count < period) return (0m, 0m, 0m);
        var recent   = closes.TakeLast(period).ToList();
        decimal sma  = recent.Average();
        decimal var_ = recent.Select(c => (c - sma) * (c - sma)).Average();
        decimal std  = (decimal)Math.Sqrt((double)var_);
        return (sma + 2 * std, sma, sma - 2 * std);
    }

    private static OptionItemDto ToDto(OptionItem item) =>
        new(item.Id, item.UnderlyingTicker, item.PositionType, item.ExpirationDate,
            item.Strike, item.Premium, item.NumberOfContracts, item.MarketPrice, item.AddedAt);
}
