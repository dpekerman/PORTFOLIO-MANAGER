using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

/// <summary>
/// One-click "Fix Missing Data" recovery — replays the exact three writes the daily EOD automation
/// would have produced (EOD signals, portfolio value snapshot, Value Screener), for use when the
/// scheduled automation failed to run (e.g. machine asleep through the whole EOD window).
/// Every underlying write is already upsert/dedupe-by-day (see EodSignalPersistenceService.SaveAsync,
/// PortfolioValueHistoryService.RecordCurrentValueAsync), so calling this any number of times in a
/// day only ever leaves ONE persisted data set per step — never duplicates.
/// </summary>
public interface IMissedDataRecoveryService
{
    Task<MissedDataRecoveryResult> RecoverTodayAsync(CancellationToken ct);
}

public sealed class MissedDataRecoveryService(
    AppDbContext db,
    IRsiScannerService scanner,
    EodSignalPersistenceService eodPersistence,
    IPortfolioValueHistoryService historyService,
    ITradingSessionGuard tradingSessionGuard,
    ValueScreenerService valueScreener,
    ValueScreenerPersistenceService valueScreenerPersistence,
    ILogger<MissedDataRecoveryService> logger) : IMissedDataRecoveryService
{
    // Process-wide single-flight guard: a second concurrent click while a recovery is already
    // running returns immediately with AlreadyRunning instead of firing a duplicate set of
    // Yahoo Finance scans (the per-step writes are idempotent regardless, but this avoids
    // wasteful redundant external calls and log noise).
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task<MissedDataRecoveryResult> RecoverTodayAsync(CancellationToken ct)
    {
        if (!await Gate.WaitAsync(0, ct))
        {
            logger.LogInformation("[MissedDataRecovery] Ignored — a recovery run is already in flight.");
            return new MissedDataRecoveryResult(false, "AlreadyRunning", null, null, null);
        }

        try
        {
            logger.LogInformation("[MissedDataRecovery] Starting recovery run.");
            var eodStep = await RunEodSignalsStepAsync(ct);
            var snapshotStep = await RunSnapshotStepAsync(ct);
            var screenerStep = await RunValueScreenerStepAsync(ct);
            logger.LogInformation("[MissedDataRecovery] Recovery run finished.");
            return new MissedDataRecoveryResult(true, "Completed", eodStep, snapshotStep, screenerStep);
        }
        finally
        {
            Gate.Release();
        }
    }

    private async Task<RecoveryStepResult> RunEodSignalsStepAsync(CancellationToken ct)
    {
        try
        {
            // Mirrors EodSignalsController.PersistNow exactly (same symbol universe, thresholds, gate).
            var portfolioSymbols = await db.PortfolioItems
                .Where(p => !p.IsManual).Select(p => p.Symbol).ToListAsync(ct);
            var watchlistSymbols = await db.WatchlistItems.Select(w => w.Symbol).ToListAsync(ct);
            var extraSymbols = portfolioSymbols.Concat(watchlistSymbols)
                .Select(s => s.Trim().ToUpperInvariant()).Distinct().ToList();

            var result = await scanner.ScanAsync(
                extraSymbols, oversoldThreshold: 30m, overboughtThreshold: 75m,
                logicMode: "Enhanced", userId: null, ct: ct);

            var candidates = (result.OversoldChain ?? [])
                .Concat(result.OverboughtChain ?? [])
                .Where(r => r.TrendShift.Contains("Bull Turn") || r.TrendShift.Contains("Bear Turn"))
                .ToList();

            var promoted = candidates.Count > 0 ? await eodPersistence.SaveAsync(candidates, ct) : [];
            return RecoveryStepResult.Ok(
                $"{promoted.Count} signal(s) persisted ({candidates.Count} candidate(s) scanned)");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[MissedDataRecovery] EOD signals step failed.");
            return RecoveryStepResult.Fail(ex.Message);
        }
    }

    private async Task<RecoveryStepResult> RunSnapshotStepAsync(CancellationToken ct)
    {
        try
        {
            // Stamp against the actual last-completed trading day, not "today ET" — if this is
            // clicked after that day has already rolled over (e.g. the next morning from a timezone
            // ahead of Eastern Time), "today" would be wrong and the missed day would never get a
            // snapshot at all (it would silently insert under the wrong date instead).
            var tradingDate = await tradingSessionGuard.GetLatestTradingDateAsync(ct);
            var dto = await historyService.RecordCurrentValueAsync(ct, PortfolioValueSource.ManualRecordNow, tradingDate);
            return RecoveryStepResult.Ok($"Recorded {dto.RecordedDate}: ${dto.TotalValue:N2}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[MissedDataRecovery] Snapshot step failed.");
            return RecoveryStepResult.Fail(ex.Message);
        }
    }

    private async Task<RecoveryStepResult> RunValueScreenerStepAsync(CancellationToken ct)
    {
        try
        {
            var portfolioResults = await valueScreener.RunAsync(
                new ValueScreenerRequest { IncludePortfolio = true, IncludeWatchlist = false }, ct);
            await valueScreenerPersistence.SaveAsync("Portfolio", portfolioResults, ct);

            var watchlistResults = await valueScreener.RunAsync(
                new ValueScreenerRequest { IncludePortfolio = false, IncludeWatchlist = true }, ct);
            await valueScreenerPersistence.SaveAsync("Watchlist", watchlistResults, ct);

            return RecoveryStepResult.Ok(
                $"{portfolioResults.Count} portfolio + {watchlistResults.Count} watchlist result(s) persisted");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[MissedDataRecovery] Value screener step failed.");
            return RecoveryStepResult.Fail(ex.Message);
        }
    }
}

public record RecoveryStepResult(bool Success, string Message)
{
    public static RecoveryStepResult Ok(string message) => new(true, message);
    public static RecoveryStepResult Fail(string message) => new(false, message);
}

public record MissedDataRecoveryResult(
    bool Started,
    string Status, // "Completed" | "AlreadyRunning"
    RecoveryStepResult? EodSignals,
    RecoveryStepResult? Snapshot,
    RecoveryStepResult? ValueScreener);
