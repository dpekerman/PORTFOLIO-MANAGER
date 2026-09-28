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
    /// RsiDelta1D, TrendShift, StageStatus, TurnStrength, ChaseRisk,
    /// DynamicStopLoss, Sma200, TrendSetup200, IsTracked.
    /// Expired setups (beyond MaxActiveTradingDays without promotion) are deactivated here.
    /// </summary>
    Task UpsertAndEnrichAsync(
        IEnumerable<RsiScanResult> results,
        decimal trendShiftThreshold = 0.25m,
        decimal earlyMin    = 0.25m,
        decimal normalMin   = 1.0m,
        decimal strongMin   = 5.0m,
        decimal explosiveMin = 10.0m,
        int maxActiveTradingDays = 7,
        CancellationToken ct = default);

    /// <summary>Marks the staged signal inactive after a confirmed signal is written to DailySignals.</summary>
    Task DeactivateAsync(string symbol, string scanType, CancellationToken ct = default);

    /// <summary>Deletes deactivated staged signals older than <paramref name="retentionDays"/> — these are temporary tracking rows, not meant for long-term retention.</summary>
    Task<int> CleanupStaleAsync(int retentionDays = 30, CancellationToken ct = default);
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
        decimal earlyMin    = 0.25m,
        decimal normalMin   = 1.0m,
        decimal strongMin   = 5.0m,
        decimal explosiveMin = 10.0m,
        int maxActiveTradingDays = 7,
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
                // Check expiration before updating
                int daysSinceStaged = (today.DayNumber - staged.StagedDate.DayNumber);
                if (daysSinceStaged > maxActiveTradingDays)
                {
                    staged.IsActiveWatch = false;
                    staged.UpdatedAt = DateTime.UtcNow;
                    logger.LogInformation(
                        "[StagedSignal] Expired: {Symbol} {ScanType} — {Days} trading days without promotion (max={Max})",
                        result.Symbol, scanTypeStr, daysSinceStaged, maxActiveTradingDays);
                    // Result is still returned to the caller but IsTracked = false
                    continue;
                }

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
            result.RsiDelta1D  = staged.RsiDelta1D;
            result.TrendShift  = ComputeTrendShift(staged.RsiDelta1D, result.ScanType, trendShiftThreshold);
            result.TurnStrength = ComputeTurnStrength(staged.RsiDelta1D, result.ScanType, earlyMin, normalMin, strongMin, explosiveMin);
            result.ChaseRisk   = result.TurnStrength == "Explosive" ? "Elevated" : string.Empty;
            result.StageStatus = ComputeStageStatus(staged.RsiDelta1D, result.TrendShift);
            result.IsTracked   = true;

            if (result.ChannelState is "THIRD_TOUCH_TEST" or "LOWER_RAIL_RETEST")
            {
                result.ChannelState = result.TrendShift.Contains("Bull Turn", StringComparison.OrdinalIgnoreCase)
                    ? "BOUNCE_CONFIRMED"
                    : result.TrendShift.Contains("Stabilizing", StringComparison.OrdinalIgnoreCase)
                        ? "REVERSAL_DEVELOPING"
                        : result.ChannelState;
            }

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

    public async Task<int> CleanupStaleAsync(int retentionDays = 30, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        var stale = await db.StagedSignals
            .Where(s => !s.IsActiveWatch && s.UpdatedAt < cutoff)
            .ToListAsync(ct);

        if (stale.Count == 0) return 0;

        db.StagedSignals.RemoveRange(stale);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("[StagedSignal] Cleaned up {Count} stale deactivated staged signal(s) older than {Days} days.",
            stale.Count, retentionDays);
        return stale.Count;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    internal static string ComputeTrendShift(decimal? rsiDelta, ScanType scanType, decimal threshold)
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

    /// <summary>
    /// Stage status derived purely from RsiDelta1D and TrendShift.
    /// STAGED     = no delta yet (Day 1).
    /// CONFIRMING = momentum reversed (Bull Turn / Bear Turn).
    /// TRACKING   = delta exists but not yet a meaningful reversal.
    /// </summary>
    internal static string ComputeStageStatus(decimal? rsiDelta, string trendShift)
    {
        if (!rsiDelta.HasValue) return "STAGED";
        if (trendShift.Contains("Bull Turn") || trendShift.Contains("Bear Turn")) return "CONFIRMING";
        return "TRACKING";
    }

    /// <summary>
    /// Velocity label for a confirmed turn. Returns "" when not applicable.
    /// Uses absolute RSI delta so the same thresholds apply to both Oversold and Overbought.
    /// </summary>
    internal static string ComputeTurnStrength(
        decimal? rsiDelta, ScanType scanType,
        decimal earlyMin, decimal normalMin, decimal strongMin, decimal explosiveMin)
    {
        if (!rsiDelta.HasValue) return string.Empty;

        // Only label a turn when the delta direction matches the scan type
        bool isTurn = scanType == ScanType.Oversold
            ? rsiDelta.Value > earlyMin
            : rsiDelta.Value < -earlyMin;

        if (!isTurn) return string.Empty;

        decimal abs = Math.Abs(rsiDelta.Value);
        if (abs >= explosiveMin) return "Explosive";
        if (abs >= strongMin)    return "Strong";
        if (abs >= normalMin)    return "Normal";
        return "Early";
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
