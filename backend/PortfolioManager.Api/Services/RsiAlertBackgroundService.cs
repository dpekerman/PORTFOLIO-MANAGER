using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

/// <summary>
/// A long-running background service that periodically runs the RSI scanner
/// and fires email notifications whenever a new CONFIRMED signal is detected.
///
/// Additionally, during the configured EOD window (default 3:30–4:00 PM Eastern),
/// it evaluates the EOD CONFIRM rules and sends a separate email for new EOD signals.
///
/// This runs independently of the frontend — emails go out as long as the
/// backend process is alive, regardless of which page the user has open.
/// </summary>
public sealed class RsiAlertBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<EmailSettings> settingsMonitor,
    ScannerRuntimeConfig runtimeConfig,
    EodSignalPersistenceService eodPersistence,
    IHostEnvironment env,
    ILogger<RsiAlertBackgroundService> logger) : BackgroundService
{
    private EmailSettings Settings => settingsMonitor.CurrentValue;
    private DateOnly _lastCleanupDate = DateOnly.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Brief startup delay so all services are fully initialized
        logger.LogInformation("[RsiAlertBg] Background RSI alert scanner starting. " +
            "Interval: {Interval}s, Oversold<{OS} Overbought>{OB}",
            Settings.ScanIntervalSeconds,
            Settings.OversoldThreshold,
            Settings.OverboughtThreshold);

        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var easternNow = MarketHoursGate.GetEasternNow();

                // Dev environment bypasses the gate entirely so local testing works at any time.
                // In Production this keeps the DB idle (and free to auto-pause) outside market hours.
                if (env.IsDevelopment() || (easternNow is not null && MarketHoursGate.IsMarketHours(easternNow.Value)))
                {
                    await RunScanCycleAsync(stoppingToken);
                }
                else
                {
                    logger.LogDebug("[RsiAlertBg] Outside market hours (9:00-16:30 ET, Mon-Fri) — skipping scan cycle.");
                }

                await MaybeRunDailyCleanupAsync(easternNow, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[RsiAlertBg] Scan cycle failed. Will retry in {Interval}s.",
                    Settings.ScanIntervalSeconds);
            }

            var interval = Math.Clamp(Settings.ScanIntervalSeconds, 60, 3600);
            logger.LogDebug("[RsiAlertBg] Next scan in {Interval}s.", interval);
            await Task.Delay(TimeSpan.FromSeconds(interval), stoppingToken);
        }

        logger.LogInformation("[RsiAlertBg] Background RSI alert scanner stopped.");
    }

    /// <summary>Purges deactivated StagedSignals once per Eastern calendar day (they're temporary tracking rows).</summary>
    private async Task MaybeRunDailyCleanupAsync(DateTime? easternNow, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(easternNow ?? DateTime.UtcNow);
        if (today == _lastCleanupDate) return;
        _lastCleanupDate = today;

        using var scope = scopeFactory.CreateScope();
        var staged = scope.ServiceProvider.GetRequiredService<IStagedSignalService>();
        await staged.CleanupStaleAsync(ct: ct);
    }

    private async Task RunScanCycleAsync(CancellationToken ct)
    {
        // Scoped services (IRsiScannerService uses typed HttpClient, which is transient-per-scope)
        using var scope = scopeFactory.CreateScope();
        var scanner = scope.ServiceProvider.GetRequiredService<IRsiScannerService>();
        var notifier = scope.ServiceProvider.GetRequiredService<EmailNotificationService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var snapshotSvc = scope.ServiceProvider.GetRequiredService<IRsiSnapshotService>();

        // Mirror what ScannerController does: include all user-defined symbols from the
        // portfolio and watchlist so that non-TSX stocks (e.g. BABA, US-listed holdings)
        // are scanned by the background service exactly as they are on the frontend.
        var portfolioSymbols = await db.PortfolioItems
            .Where(p => !p.IsManual)
            .Select(p => p.Symbol)
            .ToListAsync(ct);
        var watchlistSymbols = await db.WatchlistItems
            .Select(w => w.Symbol)
            .ToListAsync(ct);
        var extraSymbols = portfolioSymbols
            .Concat(watchlistSymbols)
            .Select(s => s.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        logger.LogDebug("[RsiAlertBg] Running RSI scan (OS<{OS} OB>{OB})...",
            Settings.OversoldThreshold, Settings.OverboughtThreshold);

        var result = await scanner.ScanAsync(
            extraSymbols,         // include portfolio + watchlist symbols (mirrors ScannerController)
            Settings.OversoldThreshold,
            Settings.OverboughtThreshold,
            "Enhanced",   // must match the UI logic mode so email status == displayed status
            ct);

        if (result.IsDemo)
        {
            logger.LogDebug("[RsiAlertBg] Scan returned demo data — skipping notification check.");
            return;
        }

        // Persist scan snapshot so the frontend displays fresh data on next load
        await snapshotSvc.SaveAsync(result, ct);

        // ── Standard Confirmed signal notifications ───────────────────────────
        var totalConfirmed =
            (result.OversoldChain?.Count(r => r.Status == SignalStatus.Confirmed) ?? 0) +
            (result.OverboughtChain?.Count(r => r.Status == SignalStatus.Confirmed) ?? 0);

        logger.LogDebug("[RsiAlertBg] Scan complete. {TotalConfirmed} CONFIRMED signal(s) found.", totalConfirmed);

        // Standard "Confirmed" email notifications are intentionally suppressed.
        // Only EOD Confirm signals trigger email alerts (see EOD window below).
        // await notifier.NotifyNewConfirmedSignalsAsync(result);

        // ── EOD window: persist confirmed signals and send 2-stage report email ──
        bool inEodWindow = runtimeConfig.IsEodWindowActive();
        if (inEodWindow)
        {
            var allResults = (result.OversoldChain ?? [])
                .Concat(result.OverboughtChain ?? [])
                .ToList();

            // Stage-2 candidates: all signals with a Bull/Bear Turn, regardless of legacy Status.
            // The Stage-2 gate inside SaveAsync (TrendShift + Price + Volume >= 1.5x) decides
            // whether to promote. Legacy Status is intentionally ignored here.
            var bullBearTurns = allResults
                .Where(r => r.TrendShift.Contains("Bull Turn") || r.TrendShift.Contains("Bear Turn"))
                .ToList();

            if (bullBearTurns.Count > 0)
            {
                logger.LogInformation(
                    "[RsiAlertBg] EOD Window active ({Start}\u2013{End} ET). " +
                    "{Turns} Bull/Bear Turn signal(s) queued for Stage-2 gate.",
                    runtimeConfig.EodWindowStart, runtimeConfig.EodWindowEnd,
                    bullBearTurns.Count);

                // SaveAsync applies the Stage-2 gate and returns only promoted signals.
                var promoted = await eodPersistence.SaveAsync(bullBearTurns, ct);

                // Awaiting = Bull/Bear Turn signals that did NOT pass the Stage-2 gate.
                var promotedSymbols = new HashSet<string>(
                    promoted.Select(r => r.Symbol), StringComparer.OrdinalIgnoreCase);
                var awaiting = bullBearTurns
                    .Where(r => !promotedSymbols.Contains(r.Symbol))
                    .ToList();

                // Send the 2-stage EOD report (fires only for newly seen signals).
                await notifier.NotifyEodReportAsync(promoted, awaiting, result.ScannedAt);
            }
        }
    }
}
