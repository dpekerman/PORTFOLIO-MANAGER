using Microsoft.EntityFrameworkCore;
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
    ILogger<RsiAlertBackgroundService> logger) : BackgroundService
{
    private EmailSettings Settings => settingsMonitor.CurrentValue;

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
                await RunScanCycleAsync(stoppingToken);
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

    private async Task RunScanCycleAsync(CancellationToken ct)
    {
        // Scoped services (IRsiScannerService uses typed HttpClient, which is transient-per-scope)
        using var scope = scopeFactory.CreateScope();
        var scanner = scope.ServiceProvider.GetRequiredService<IRsiScannerService>();
        var notifier = scope.ServiceProvider.GetRequiredService<EmailNotificationService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

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

            // Signals eligible for the Stage-2 promotion gate (EodConfirm or Confirmed status)
            var allQualified = allResults
                .Where(r => r.Status == SignalStatus.EodConfirm || r.Status == SignalStatus.Confirmed)
                .ToList();

            // Signals with a Bull/Bear Turn — candidates for the Awaiting section
            var bullBearTurns = allResults
                .Where(r => r.TrendShift.Contains("Bull Turn") || r.TrendShift.Contains("Bear Turn"))
                .ToList();

            if (bullBearTurns.Count > 0 || allQualified.Count > 0)
            {
                logger.LogInformation(
                    "[RsiAlertBg] EOD Window active ({Start}–{End} ET). " +
                    "{Qualified} qualified, {Turns} Bull/Bear Turn signal(s).",
                    runtimeConfig.EodWindowStart, runtimeConfig.EodWindowEnd,
                    allQualified.Count, bullBearTurns.Count);

                // Save qualified signals — Stage-2 gate (TrendShift + Price + Volume) applied inside.
                // Returns only the signals that were actually promoted to DailySignals.
                var promoted = await eodPersistence.SaveAsync(allQualified, ct);

                // Awaiting = Bull/Bear Turn signals that did NOT pass the Stage-2 gate
                var promotedSymbols = new HashSet<string>(
                    promoted.Select(r => r.Symbol), StringComparer.OrdinalIgnoreCase);
                var awaiting = bullBearTurns
                    .Where(r => !promotedSymbols.Contains(r.Symbol))
                    .ToList();

                // Send the 2-stage EOD report (fires only for newly seen signals)
                await notifier.NotifyEodReportAsync(promoted, awaiting, result.ScannedAt);
            }
        }
    }
}
