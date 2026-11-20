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

    /// <summary>Calculates the same staged-signal enrichment without tracking or persisting lifecycle changes.</summary>
    Task PreviewAndEnrichAsync(
        IEnumerable<RsiScanResult> results,
        decimal trendShiftThreshold = 0.25m,
        decimal earlyMin    = 0.25m,
        decimal normalMin   = 1.0m,
        decimal strongMin   = 5.0m,
        decimal explosiveMin = 10.0m,
        int maxActiveTradingDays = 7,
        CancellationToken ct = default) =>
        throw new NotSupportedException("This staged-signal service does not support read-only previews.");

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
        await EnrichAsync(results, persist: true, trendShiftThreshold: trendShiftThreshold, earlyMin: earlyMin,
            normalMin: normalMin, strongMin: strongMin, explosiveMin: explosiveMin,
            maxActiveTradingDays: maxActiveTradingDays, ct: ct);
    }

    public async Task PreviewAndEnrichAsync(
        IEnumerable<RsiScanResult> results,
        decimal trendShiftThreshold = 0.25m,
        decimal earlyMin    = 0.25m,
        decimal normalMin   = 1.0m,
        decimal strongMin   = 5.0m,
        decimal explosiveMin = 10.0m,
        int maxActiveTradingDays = 7,
        CancellationToken ct = default)
    {
        await EnrichAsync(results, persist: false, trendShiftThreshold: trendShiftThreshold, earlyMin: earlyMin,
            normalMin: normalMin, strongMin: strongMin, explosiveMin: explosiveMin,
            maxActiveTradingDays: maxActiveTradingDays, ct: ct);
    }

    private async Task EnrichAsync(
        IEnumerable<RsiScanResult> results,
        bool persist,
        decimal trendShiftThreshold,
        decimal earlyMin,
        decimal normalMin,
        decimal strongMin,
        decimal explosiveMin,
        int maxActiveTradingDays,
        CancellationToken ct)
    {
        var resultList = results.ToList();
        if (resultList.Count == 0) return;

        var evaluatedAtUtc = DateTime.UtcNow;
        var today = GetEtToday(evaluatedAtUtc);

        var symbols = resultList.Select(r => r.Symbol).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var stagedQuery = db.StagedSignals.Where(s => s.IsActiveWatch && symbols.Contains(s.Symbol));
        var stagedRows = persist
            ? await stagedQuery.ToListAsync(ct)
            : await stagedQuery.AsNoTracking().ToListAsync(ct);
        var stagedMap = stagedRows.ToDictionary(s => (s.Symbol.ToUpperInvariant(), s.ScanType));

        foreach (var result in resultList)
        {
            var scanTypeStr = result.ScanType.ToString();
            var key = (result.Symbol.ToUpperInvariant(), scanTypeStr);
            var exists = stagedMap.TryGetValue(key, out var staged);
            var transition = CalculateTransition(
                staged,
                result,
                today,
                evaluatedAtUtc,
                trendShiftThreshold,
                earlyMin,
                normalMin,
                strongMin,
                explosiveMin,
                maxActiveTradingDays);

            if (persist)
            {
                if (exists)
                {
                    ApplyState(staged!, transition.State);
                }
                else if (transition.Created)
                {
                    db.StagedSignals.Add(transition.State);
                }
            }

            if (transition.Expired)
            {
                var elapsedDays = today.DayNumber - transition.State.StagedDate.DayNumber;
                if (persist)
                    logger.LogInformation(
                        "[StagedSignal] Expired: {Symbol} {ScanType} — {Days} trading days without promotion (max={Max})",
                        result.Symbol, scanTypeStr, elapsedDays, maxActiveTradingDays);
                else
                    logger.LogDebug(
                        "[StagedSignal] Previewed expiration: {Symbol} {ScanType} — {Days} trading days (max={Max})",
                        result.Symbol, scanTypeStr, elapsedDays, maxActiveTradingDays);
                continue;
            }

            if (transition.Created)
            {
                if (persist)
                    logger.LogInformation("[StagedSignal] Created new staged signal: {Symbol} {ScanType} RSI={Rsi}",
                        result.Symbol, scanTypeStr, result.Rsi);
                else
                    logger.LogDebug("[StagedSignal] Previewed new staged signal: {Symbol} {ScanType} RSI={Rsi}",
                        result.Symbol, scanTypeStr, result.Rsi);
            }
        }

        if (persist) await db.SaveChangesAsync(ct);
    }

    private sealed record StagedSignalTransition(StagedSignal State, bool Created, bool Expired);

    private static StagedSignalTransition CalculateTransition(
        StagedSignal? existing,
        RsiScanResult result,
        DateOnly today,
        DateTime evaluatedAtUtc,
        decimal trendShiftThreshold,
        decimal earlyMin,
        decimal normalMin,
        decimal strongMin,
        decimal explosiveMin,
        int maxActiveTradingDays)
    {
        var created = existing is null;
        var staged = existing is null
            ? new StagedSignal
            {
                Symbol = result.Symbol,
                ScanType = result.ScanType.ToString(),
                BasePrice = result.CurrentPrice,
                BaseRsi = result.Rsi,
                BaseHigh = result.DayHigh,
                BaseLow = result.DayLow,
                CurrentPrice = result.CurrentPrice,
                CurrentRsi = result.Rsi,
                ExtremeLow = result.ScanType == Models.ScanType.Oversold ? result.DayLow : null,
                ExtremeHigh = result.ScanType == Models.ScanType.Overbought ? result.DayHigh : null,
                StagedDate = today,
                LastEvaluatedDate = today,
                IsActiveWatch = true,
                CreatedAt = evaluatedAtUtc,
                UpdatedAt = evaluatedAtUtc,
            }
            : CopyState(existing);

        if (!created)
        {
            var daysSinceStaged = today.DayNumber - staged.StagedDate.DayNumber;
            if (daysSinceStaged > maxActiveTradingDays)
            {
                staged.IsActiveWatch = false;
                staged.UpdatedAt = evaluatedAtUtc;
                return new StagedSignalTransition(staged, Created: false, Expired: true);
            }

            if (staged.LastEvaluatedDate != today)
            {
                staged.PreviousRsi = staged.CurrentRsi;
                staged.PreviousPrice = staged.CurrentPrice;
                staged.LastEvaluatedDate = today;
            }

            staged.CurrentRsi = result.Rsi;
            staged.CurrentPrice = result.CurrentPrice;

            if (staged.PreviousRsi.HasValue)
                staged.RsiDelta1D = staged.CurrentRsi - staged.PreviousRsi.Value;

            if (result.ScanType == Models.ScanType.Oversold)
                staged.ExtremeLow = staged.ExtremeLow.HasValue
                    ? Math.Min(staged.ExtremeLow.Value, result.DayLow)
                    : result.DayLow;
            else
                staged.ExtremeHigh = staged.ExtremeHigh.HasValue
                    ? Math.Max(staged.ExtremeHigh.Value, result.DayHigh)
                    : result.DayHigh;

            staged.UpdatedAt = evaluatedAtUtc;
        }

        result.RsiDelta1D = staged.RsiDelta1D;
        result.TrendShift = ComputeTrendShift(staged.RsiDelta1D, result.ScanType, trendShiftThreshold);
        result.TurnStrength = ComputeTurnStrength(staged.RsiDelta1D, result.ScanType, earlyMin, normalMin, strongMin, explosiveMin);
        result.ChaseRisk = result.TurnStrength == "Explosive" ? "Elevated" : string.Empty;
        result.StageStatus = ComputeStageStatus(staged.RsiDelta1D, result.TrendShift);
        result.IsTracked = true;

        if (result.ChannelState is "THIRD_TOUCH_TEST" or "LOWER_RAIL_RETEST")
        {
            result.ChannelState = result.TrendShift.Contains("Bull Turn", StringComparison.OrdinalIgnoreCase)
                ? "BOUNCE_CONFIRMED"
                : result.TrendShift.Contains("Stabilizing", StringComparison.OrdinalIgnoreCase)
                    ? "REVERSAL_DEVELOPING"
                    : result.ChannelState;
        }

        if (result.DailyAtr > 0)
        {
            if (result.ScanType == Models.ScanType.Oversold && staged.ExtremeLow.HasValue)
                result.DynamicStopLoss = Math.Round(staged.ExtremeLow.Value - (1.5m * result.DailyAtr), 4);
            else if (result.ScanType == Models.ScanType.Overbought && staged.ExtremeHigh.HasValue)
                result.DynamicStopLoss = Math.Round(staged.ExtremeHigh.Value + (1.5m * result.DailyAtr), 4);
        }

        if (result.Sma200 > 0)
            result.TrendSetup200 = result.CurrentPrice > result.Sma200 ? "Trend-Aligned" : "Counter-Trend";

        return new StagedSignalTransition(staged, created, Expired: false);
    }

    private static StagedSignal CopyState(StagedSignal source) => new()
    {
        StagedId = source.StagedId,
        Symbol = source.Symbol,
        ScanType = source.ScanType,
        BasePrice = source.BasePrice,
        BaseRsi = source.BaseRsi,
        BaseHigh = source.BaseHigh,
        BaseLow = source.BaseLow,
        PreviousPrice = source.PreviousPrice,
        PreviousRsi = source.PreviousRsi,
        CurrentPrice = source.CurrentPrice,
        CurrentRsi = source.CurrentRsi,
        RsiDelta1D = source.RsiDelta1D,
        ExtremeLow = source.ExtremeLow,
        ExtremeHigh = source.ExtremeHigh,
        StagedDate = source.StagedDate,
        LastEvaluatedDate = source.LastEvaluatedDate,
        IsActiveWatch = source.IsActiveWatch,
        CreatedAt = source.CreatedAt,
        UpdatedAt = source.UpdatedAt,
    };

    private static void ApplyState(StagedSignal target, StagedSignal state)
    {
        target.PreviousPrice = state.PreviousPrice;
        target.PreviousRsi = state.PreviousRsi;
        target.CurrentPrice = state.CurrentPrice;
        target.CurrentRsi = state.CurrentRsi;
        target.RsiDelta1D = state.RsiDelta1D;
        target.ExtremeLow = state.ExtremeLow;
        target.ExtremeHigh = state.ExtremeHigh;
        target.LastEvaluatedDate = state.LastEvaluatedDate;
        target.IsActiveWatch = state.IsActiveWatch;
        target.UpdatedAt = state.UpdatedAt;
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

    private static DateOnly GetEtToday(DateTime utcNow)
    {
        TimeZoneInfo? tz = null;
        foreach (var id in EasternTzIds)
        {
            try { tz = TimeZoneInfo.FindSystemTimeZoneById(id); break; }
            catch { /* try next */ }
        }
        var dt = tz is not null
            ? TimeZoneInfo.ConvertTimeFromUtc(utcNow, tz)
            : utcNow;
        return DateOnly.FromDateTime(dt);
    }
}
