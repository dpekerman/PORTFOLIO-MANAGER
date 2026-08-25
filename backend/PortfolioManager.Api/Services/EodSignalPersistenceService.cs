using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

// Overnight persistence for EOD CONFIRM signals — reads/writes from DailySignals DB table.
public class EodSignalPersistenceService
{
    private readonly ILogger<EodSignalPersistenceService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    private static readonly string[] EasternTzIds = ["Eastern Standard Time", "America/New_York"];

    public EodSignalPersistenceService(
        ILogger<EodSignalPersistenceService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    // ── Write ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Persists the supplied EOD CONFIRM signals to disk, tagged with today's ET date.
    /// Overwrites any previously saved signals (only the latest EOD window is kept).
    /// Also appends to the DailySignals database table for full history tracking.
    /// </summary>
    public async Task<List<RsiScanResult>> SaveAsync(IEnumerable<RsiScanResult> eodResults, CancellationToken ct = default)
    {
        var tz = GetEasternTz();
        var etToday = tz is not null
            ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz).ToString("yyyy-MM-dd")
            : DateTime.UtcNow.ToString("yyyy-MM-dd");

        var resultList = eodResults.ToList();

        // Stage-2 promotion gate: Bull/Bear Turn + Price confirmation + Volume >= 1.5x.
        // All three must pass. Legacy scanner Status is NOT consulted here.
        var confirmed = new List<RsiScanResult>();
        foreach (var r in resultList)
        {
            bool bullBearTurnPassed = r.RsiDelta1D.HasValue
                && (r.TrendShift.Contains("Bull Turn") || r.TrendShift.Contains("Bear Turn"));
            bool eodPriceConfirmationPassed = IsEodPriceConfirmed(r);
            bool volumeConfirmationPassed = r.VolumeRatio >= 1.5m;
            bool ema9Confirmed = IsEma9Confirmed(r);
            bool promoted = bullBearTurnPassed && eodPriceConfirmationPassed && volumeConfirmationPassed;

            _logger.LogInformation(
                "[Stage2Gate] {Symbol} {ScanType} | Turn={Turn} | EodPrice={EodPrice} | Volume={Vol} ({Ratio:F2}x) | EMA9={Ema9} | Promoted={Promoted}",
                r.Symbol, r.ScanType, bullBearTurnPassed, eodPriceConfirmationPassed,
                volumeConfirmationPassed, r.VolumeRatio, ema9Confirmed, promoted);

            if (promoted)
                confirmed.Add(r);
        }

        _logger.LogInformation(
            "[EodPersistence] {Total} Stage-2 candidates: {Confirmed} promoted, {Waiting} still waiting.",
            resultList.Count, confirmed.Count, resultList.Count - confirmed.Count);

        resultList = confirmed;

        // ── Append to DailySignals DB table (full history + overnight persistence) ─
        if (resultList.Count > 0)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var accountValue = await db.PortfolioValueHistories
                    .OrderByDescending(h => h.RecordedAt)
                    .Select(h => h.TotalValue)
                    .FirstOrDefaultAsync(ct);

                // Avoid duplicates: skip symbols already recorded for this date
                var existingSymbols = await db.DailySignals
                    .Where(s => s.SignalDate == etToday)
                    .Select(s => s.Symbol)
                    .ToListAsync(ct);

                var existingSet = new HashSet<string>(existingSymbols, StringComparer.OrdinalIgnoreCase);

                var newRecords = resultList
                    .Where(r => !existingSet.Contains(r.Symbol))
                    .Select(r =>
                    {
                        decimal? stopLoss = r.DynamicStopLoss > 0 ? r.DynamicStopLoss : null;
                        decimal? entryPrice = r.CurrentPrice > 0 ? r.CurrentPrice : null;
                        decimal? riskPerShare = (entryPrice.HasValue && stopLoss.HasValue)
                            ? Math.Abs(entryPrice.Value - stopLoss.Value)
                            : null;
                        var riskBudget = accountValue > 0 ? accountValue * 0.01m : 0m;
                        var maxPositionValue = accountValue > 0 ? accountValue * 0.10m : 0m;
                        var sharesByRisk = riskPerShare is > 0 && riskBudget > 0
                            ? Math.Floor(riskBudget / riskPerShare.Value)
                            : 0m;
                        var sharesByValue = entryPrice is > 0 && maxPositionValue > 0
                            ? Math.Floor(maxPositionValue / entryPrice.Value)
                            : 0m;
                        var sizingShares = Math.Max(0m, Math.Min(sharesByRisk, sharesByValue));
                        return new DailySignal
                        {
                            Symbol             = r.Symbol,
                            CompanyName        = r.CompanyName ?? string.Empty,
                            ScanType           = r.ScanType.ToString(),
                            SignalType         = r.Status.ToString(),
                            Rsi                = Math.Round(r.Rsi, 2),
                            Price              = r.CurrentPrice,
                            TriggerDetails     = r.TriggerDetails ?? string.Empty,
                            SignalDate         = etToday,
                            RecordedAt         = r.ScannedAt,
                            RuleVersion        = r.LogicMode ?? "Legacy",
                            SignalState        = "Active",
                            Sector             = r.Sector ?? string.Empty,
                            ReversalProbability = r.ReversalProbability ?? string.Empty,
                            VolumeSignal       = r.VolumeSignal ?? string.Empty,
                            TrendShift         = r.TrendShift,
                            RsiDelta1D         = r.RsiDelta1D,
                            EntryPrice         = entryPrice,
                            StopLossPrice      = stopLoss,
                            RiskPerShare       = riskPerShare,
                            PositionSizingShares = sizingShares > 0 ? sizingShares : null,
                            PositionSizingRiskAmount = sizingShares > 0 && riskPerShare.HasValue ? sizingShares * riskPerShare.Value : null,
                            PositionSizingPositionValue = sizingShares > 0 && entryPrice.HasValue ? sizingShares * entryPrice.Value : null,
                            PositionSizingLimitingReason = accountValue <= 0 ? "No persisted account value" : riskPerShare is not > 0 ? "No valid stop loss" : sharesByRisk <= sharesByValue ? "Risk budget (1%)" : "Position limit (10%)",
                            Sma200             = r.Sma200 > 0 ? r.Sma200 : null,
                            Ema9AtEntry        = r.Ema9Price > 0 ? r.Ema9Price : null,
                            Ema9ConfirmedAtEntry = IsEma9Confirmed(r),
                            Fib61_8AtSignal    = r.Fib61_8 > 0 ? r.Fib61_8 : null,
                            FibZoneAtSignal    = !string.IsNullOrEmpty(r.FibZone) ? r.FibZone : null,
                            FibStatusAtSignal  = !string.IsNullOrEmpty(r.FibStatus) ? r.FibStatus : null,
                        };
                    })
                    .ToList();

                if (newRecords.Count > 0)
                {
                    db.DailySignals.AddRange(newRecords);
                    await db.SaveChangesAsync(ct);
                    _logger.LogInformation("Appended {Count} new EOD signal(s) to DailySignals table for {Date}",
                        newRecords.Count, etToday);

                    // Deactivate staged signals for all confirmed records
                    var stagedService = scope.ServiceProvider.GetRequiredService<IStagedSignalService>();
                    foreach (var r in newRecords)
                    {
                        try { await stagedService.DeactivateAsync(r.Symbol, r.ScanType, ct); }
                        catch (Exception deactivateEx)
                        {
                            _logger.LogWarning(deactivateEx, "Failed to deactivate staged signal for {Symbol}", r.Symbol);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist EOD signals to DailySignals table");
            }
        }

        return resultList;
    }

    // ── Helpers ─ promotion gate ─────────────────────────────────────────────

    /// <summary>
    /// EOD structural price rule: candle closed in the reversal direction and near its extreme.
    /// Oversold: close > open AND close >= high − 0.25×ATR.
    /// Overbought: close &lt; open AND close &lt;= low + 0.25×ATR.
    /// EMA9 is intentionally NOT part of this check — it is supporting context only.
    /// </summary>
    private static bool IsEodPriceConfirmed(RsiScanResult r)
    {
        if (r.DailyAtr <= 0m) return false;
        return r.ScanType == ScanType.Oversold
            ? r.CurrentPrice > r.OpenPrice && r.CurrentPrice >= r.DayHigh - (0.25m * r.DailyAtr)
            : r.CurrentPrice < r.OpenPrice && r.CurrentPrice <= r.DayLow + (0.25m * r.DailyAtr);
    }

    /// <summary>EMA9 supporting confirmation — price has crossed EMA9 in the reversal direction.
    /// This is NOT required for promotion; use for confidence scoring and email context only.</summary>
    private static bool IsEma9Confirmed(RsiScanResult r) =>
        r.ScanType == ScanType.Oversold
            ? r.CurrentPrice > r.Ema9Price
            : r.CurrentPrice < r.Ema9Price;

    // ── Read ──────────────────────────────────────────────────────────────────

    /// <summary>Returns the most recent EOD signals from the DailySignals table.</summary>
    public async Task<YesterdayEodResponse> GetYesterdayEodAsync(CancellationToken ct = default)
    {
        var tz = GetEasternTz();

        bool isMorning = false;
        if (tz is not null)
        {
            var etNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            isMorning = etNow.Hour < 12;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var latestDate = await db.DailySignals
                .OrderByDescending(s => s.SignalDate)
                .Select(s => s.SignalDate)
                .FirstOrDefaultAsync(ct);

            if (latestDate is null)
                return new YesterdayEodResponse { HasData = false, IsMorningWindow = isMorning };

            var signals = await db.DailySignals
                .Where(s => s.SignalDate == latestDate)
                .Select(s => new EodSignalRecord
                {
                    Symbol         = s.Symbol,
                    CompanyName    = s.CompanyName,
                    ScanType       = s.ScanType,
                    Rsi            = s.Rsi,
                    Price          = s.Price,
                    TriggerDetails = s.TriggerDetails,
                    ScannedAt      = s.RecordedAt,
                })
                .ToListAsync(ct);

            if (signals.Count == 0)
                return new YesterdayEodResponse { HasData = false, IsMorningWindow = isMorning };

            return new YesterdayEodResponse
            {
                HasData         = true,
                SignalDate      = latestDate,
                IsMorningWindow = isMorning,
                Signals         = signals
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read EOD signal history from DailySignals table");
            return new YesterdayEodResponse { HasData = false, IsMorningWindow = isMorning };
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static TimeZoneInfo? GetEasternTz()
    {
        foreach (var id in EasternTzIds)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch { /* try next */ }
        }
        return null;
    }
}
