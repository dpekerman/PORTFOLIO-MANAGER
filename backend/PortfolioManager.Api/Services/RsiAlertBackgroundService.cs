using Microsoft.Extensions.Options;
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

        logger.LogDebug("[RsiAlertBg] Running RSI scan (OS<{OS} OB>{OB})...",
            Settings.OversoldThreshold, Settings.OverboughtThreshold);

        var result = await scanner.ScanAsync(
            null,                 // extraSymbols: background service scans default TSX universe only
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

        await notifier.NotifyNewConfirmedSignalsAsync(result);

        // ── EOD Confirm notifications (only during the configured EOD window) ──
        bool inEodWindow = runtimeConfig.IsEodWindowActive();
        if (inEodWindow)
        {
            var totalEod =
                (result.OversoldChain?.Count(r => r.Status == SignalStatus.EodConfirm) ?? 0) +
                (result.OverboughtChain?.Count(r => r.Status == SignalStatus.EodConfirm) ?? 0);

            logger.LogInformation(
                "[RsiAlertBg] EOD Window active ({Start}–{End} ET). {EodCount} EOD CONFIRM signal(s) found.",
                runtimeConfig.EodWindowStart, runtimeConfig.EodWindowEnd, totalEod);

            if (totalEod > 0)
                await notifier.NotifyNewEodConfirmedSignalsAsync(result);
        }
    }
}
