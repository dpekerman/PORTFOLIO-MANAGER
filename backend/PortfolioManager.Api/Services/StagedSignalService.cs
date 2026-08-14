using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public interface IStagedSignalService
{
    /// <summary>Returns symbol→ScanType map for all active staged signals.</summary>
    Task<Dictionary<string, ScanType>> LoadActiveStagedSymbolsAsync(CancellationToken ct = default);

    /// <summary>
    /// Upserts StagedSignal records and enriches each scan result with:
    /// RsiDelta1D, TrendShift, DynamicStopLoss, Sma200, TrendSetup200, IsTracked.
    /// </summary>
    Task UpsertAndEnrichAsync(
        IEnumerable<RsiScanResult> results,
        decimal trendShiftThreshold = 0.25m,
        CancellationToken ct = default);

    /// <summary>Marks the staged signal inactive after a confirmed signal is written to DailySignals.</summary>
    Task DeactivateAsync(string symbol, string scanType, CancellationToken ct = default);
}

public sealed class StagedSignalService(
    AppDbContext db,
    ILogger<StagedSignalService> logger) : IStagedSignalService
{
    private static readonly string[] EasternTzIds = ["Eastern Standard Time", "America/New_York"];

    public async Task<Dictionary<string, ScanType>> LoadActiveStagedSymbolsAsync(CancellationToken ct = default)
    {
        var staged = await db.StagedSignals
            .Where(s => s.IsActiveWatch)
            .Select(s => new { s.Symbol, s.ScanType })
            .ToListAsync(ct);

        var result = new Dictionary<string, ScanType>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in staged)
        {
            if (Enum.TryParse<ScanType>(s.ScanType, out var st))
                result[s.Symbol] = st;
        }
        return result;
    }

    public async Task UpsertAndEnrichAsync(
        IEnumerable<RsiScanResult> results,
        decimal trendShiftThreshold = 0.25m,
        CancellationToken ct = default)
    {
        var resultList = results.ToList();
        if (resultList.Count == 0) return;

        var etToday = GetEtToday();
        var today   = DateOnly.Parse(etToday);

        var symbols = resultList.Select(r => r.Symbol).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        // Load all active staged signals for these symbols in one query
        var stagedMap = await db.StagedSignals
            .Where(s => s.IsActiveWatch && symbols.Contains(s.Symbol))
            .ToDictionaryAsync(s => (s.Symbol.ToUpperInvariant(), s.ScanType), ct);

        foreach (var result in resultList)
        {
            var scanTypeStr = result.ScanType.ToString();
            var key = (result.Symbol.ToUpperInvariant(), scanTypeStr);

            if (!stagedMap.TryGetValue(key, out var staged))
            {
                // Day 1: create new staged signal
                staged = new StagedSignal
                {
                    Symbol        = result.Symbol,
                    ScanType      = scanTypeStr,
                    BasePrice     = result.CurrentPrice,
                    BaseRsi       = result.Rsi,
                    BaseHigh      = result.DayHigh,
                    BaseLow       = result.DayLow,
                    CurrentPrice  = result.CurrentPrice,
                    CurrentRsi    = result.Rsi,
                    ExtremeLow    = result.ScanType == Models.ScanType.Oversold ? result.DayLow  : null,
                    ExtremeHigh   = result.ScanType == Models.ScanType.Overbought ? result.DayHigh : null,
                    StagedDate    = today,
                    LastEvaluatedDate = today,
                    IsActiveWatch = true,
                };
                db.StagedSignals.Add(staged);
                logger.LogInformation("[StagedSignal] Created new staged signal: {Symbol} {ScanType} RSI={Rsi}",
                    result.Symbol, scanTypeStr, result.Rsi);
            }
            else
            {
                // Existing staged signal: roll forward on new trading day
                if (staged.LastEvaluatedDate != today)
                {
                    staged.PreviousRsi   = staged.CurrentRsi;
                    staged.PreviousPrice = staged.CurrentPrice;
                    staged.LastEvaluatedDate = today;
                }

                // Always update current values
                staged.CurrentRsi   = result.Rsi;
                staged.CurrentPrice = result.CurrentPrice;

                if (staged.PreviousRsi.HasValue)
                    staged.RsiDelta1D = staged.CurrentRsi - staged.PreviousRsi.Value;

                // Track extreme prices since setup started
                if (result.ScanType == Models.ScanType.Oversold)
                    staged.ExtremeLow  = staged.ExtremeLow.HasValue
                        ? Math.Min(staged.ExtremeLow.Value, result.DayLow)
                        : result.DayLow;
                else
                    staged.ExtremeHigh = staged.ExtremeHigh.HasValue
                        ? Math.Max(staged.ExtremeHigh.Value, result.DayHigh)
                        : result.DayHigh;

                staged.UpdatedAt = DateTime.UtcNow;
            }

            // Enrich the scan result
            result.RsiDelta1D    = staged.RsiDelta1D;
            result.TrendShift    = ComputeTrendShift(staged.RsiDelta1D, result.ScanType, trendShiftThreshold);
            result.IsTracked     = true;

            // Dynamic stop loss
            if (result.DailyAtr > 0)
            {
                if (result.ScanType == Models.ScanType.Oversold && staged.ExtremeLow.HasValue)
                    result.DynamicStopLoss = Math.Round(staged.ExtremeLow.Value - (1.5m * result.DailyAtr), 4);
                else if (result.ScanType == Models.ScanType.Overbought && staged.ExtremeHigh.HasValue)
                    result.DynamicStopLoss = Math.Round(staged.ExtremeHigh.Value + (1.5m * result.DailyAtr), 4);
            }

            // Trend setup vs SMA200
            if (result.Sma200 > 0)
                result.TrendSetup200 = result.CurrentPrice > result.Sma200 ? "Trend-Aligned" : "Counter-Trend";
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task DeactivateAsync(string symbol, string scanType, CancellationToken ct = default)
    {
        var staged = await db.StagedSignals
            .Where(s => s.Symbol == symbol && s.ScanType == scanType && s.IsActiveWatch)
            .FirstOrDefaultAsync(ct);

        if (staged is not null)
        {
            staged.IsActiveWatch = false;
            staged.UpdatedAt     = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            logger.LogInformation("[StagedSignal] Deactivated staged signal: {Symbol} {ScanType}", symbol, scanType);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string ComputeTrendShift(decimal? rsiDelta, ScanType scanType, decimal threshold)
    {
        if (!rsiDelta.HasValue) return "Waiting";

        if (scanType == ScanType.Oversold)
        {
            return rsiDelta.Value > threshold  ? "\ud83d\udfe2 Bull Turn"
                 : rsiDelta.Value < -threshold ? "\ud83d\udd34 Still Falling"
                 :                               "\ud83d\udfe1 Stabilizing";
        }
        else // Overbought
        {
            return rsiDelta.Value < -threshold ? "\ud83d\udfe2 Bear Turn"
                 : rsiDelta.Value > threshold  ? "\ud83d\udd34 Still Rising"
                 :                               "\ud83d\udfe1 Stabilizing";
        }
    }

    private static string GetEtToday()
    {
        TimeZoneInfo? tz = null;
        foreach (var id in EasternTzIds)
        {
            try { tz = TimeZoneInfo.FindSystemTimeZoneById(id); break; }
            catch { /* try next */ }
        }
        var dt = tz is not null
            ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz)
            : DateTime.UtcNow;
        return dt.ToString("yyyy-MM-dd");
    }
}
