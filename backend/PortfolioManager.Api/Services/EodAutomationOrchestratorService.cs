using System.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

/// <summary>
/// Orchestrates one EOD automation run: acquires a system-awake lease, resolves the single
/// owner/admin account, guards against non-trading days, calls the EXISTING
/// DataRefreshService.RefreshAllAsync (never duplicated), then observes — without ever calling —
/// the three independent, pre-existing background services (RSI/EOD signals, portfolio snapshot,
/// Value Screener) to report an honest per-step status. "Run Automation Now" and "Test Wake" use
/// the same coordinator but a Run Now uses this exact path (no bypass of business-time gates);
/// Test Wake is a separate, non-destructive path (see TestWakeAsync) that never reaches here.
/// </summary>
public interface IEodAutomationOrchestratorService
{
    Task RunAsync(Guid runId, string triggerType, string? triggerCorrelationId, CancellationToken ct);

    /// <summary>Non-destructive infra check: keep-awake + DB connectivity + one cheap read-only quote
    /// call. Never calls RefreshAllAsync, never touches RSI/signals/snapshot/cash/transactions.</summary>
    Task RunTestWakeAsync(Guid runId, string? triggerCorrelationId, CancellationToken ct);
}

public sealed class EodAutomationOrchestratorService(
    AppDbContext db,
    IDataRefreshService dataRefresh,
    ISystemAwakeService systemAwake,
    ITradingSessionGuard tradingSessionGuard,
    IMarketDataProvider marketData,
    UserManager<ApplicationUser> userManager,
    AutomationRuntimeConfig automationConfig,
    ScannerRuntimeConfig scannerConfig,
    ValueScreenerPersistenceService valueScreenerPersistence,
    ILogger<EodAutomationOrchestratorService> logger) : IEodAutomationOrchestratorService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan SnapshotWindowStart = new(16, 30, 0); // matches PortfolioValueEodBackgroundService

    public async Task RunAsync(Guid runId, string triggerType, string? triggerCorrelationId, CancellationToken ct)
    {
        var tz = MarketHoursGate.GetEasternTimeZone();
        var startUtc = DateTime.UtcNow;
        var nowEt = tz is null ? DateTime.UtcNow : TimeZoneInfo.ConvertTimeFromUtc(startUtc, tz);

        var log = new AutomationRunLog
        {
            RunId = runId,
            TradingDate = nowEt.ToString("yyyy-MM-dd"),
            TriggerType = triggerType,
            TriggerCorrelationId = triggerCorrelationId,
            ActualStartUtc = startUtc,
            OverallStatus = AutomationStatuses.Running,
            RefreshStatus = "",
            RsiStatus = "",
            SnapshotStatus = "",
            ValueScreenerStatus = "",
            MachineName = Environment.MachineName,
        };
        db.AutomationRunLogs.Add(log);
        await db.SaveChangesAsync(ct);
        await RecordHeartbeatAsync(log, "RunLogPersisted", ct);

        await using var lease = await systemAwake.AcquireAsync(CancellationToken.None);
        log.PowerRequestAcquiredAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await RecordHeartbeatAsync(log, "PowerRequestAcquired", ct);

        try
        {
            if (tz is null)
            {
                await FailAsync(log, "TimeZone", "Eastern time zone unavailable on this machine.", ct);
                return;
            }

            if (!await tradingSessionGuard.IsTodayATradingDayAsync(ct))
            {
                log.OverallStatus = AutomationStatuses.SkippedNonTradingDay;
                log.CompletedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                logger.LogInformation("[EodAutomation] Run {RunId} skipped — not a trading day ({Date}).", runId, log.TradingDate);
                return;
            }
            await RecordHeartbeatAsync(log, "TradingDayValidated", ct);

            var admins = await userManager.GetUsersInRoleAsync("Admin");
            var owner = admins.FirstOrDefault();
            if (owner is null)
            {
                await FailAsync(log, "ResolveOwner", "No user found in role 'Admin'.", ct);
                return;
            }
            if (admins.Count > 1)
                logger.LogWarning(
                    "[EodAutomation] {Count} users found in role 'Admin'; using the first ({UserId}) as the single automation owner.",
                    admins.Count, owner.Id);
            log.OwnerUserId = owner.Id;
            await db.SaveChangesAsync(ct);
            await RecordHeartbeatAsync(log, "OwnerResolved", ct);

            await RecordHeartbeatAsync(log, "RefreshStarting", ct);
            await RunRefreshAsync(log, owner.Id, ct);
            await RecordHeartbeatAsync(log, "RefreshCompleted", ct);

            var pollDeadlineUtc = ComputePollDeadlineUtc(startUtc, tz);
            await PollForCompletionAsync(log, tz, pollDeadlineUtc, ct);
            await RecordHeartbeatAsync(log, "CompletionObserved", ct);

            log.CompletedAtUtc = DateTime.UtcNow;
            log.OverallStatus = DeriveOverallStatus(log);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("[EodAutomation] Run {RunId} finished with status {Status}.", runId, log.OverallStatus);
        }
        catch (OperationCanceledException)
        {
            await CancelAsync(log, ct);
        }
        catch (Exception ex)
        {
            await FailAsync(log, log.ErrorStep ?? "Unknown", Truncate(ex.Message, 2000), ct, ex);
        }
        finally
        {
            log.PowerRequestReleasedAtUtc = DateTime.UtcNow;
            try { await db.SaveChangesAsync(CancellationToken.None); } catch { /* best effort */ }
        }
    }

    public async Task RunTestWakeAsync(Guid runId, string? triggerCorrelationId, CancellationToken ct)
    {
        var tz = MarketHoursGate.GetEasternTimeZone();
        var startUtc = DateTime.UtcNow;
        var nowEt = tz is null ? DateTime.UtcNow : TimeZoneInfo.ConvertTimeFromUtc(startUtc, tz);

        var log = new AutomationRunLog
        {
            RunId = runId,
            TradingDate = nowEt.ToString("yyyy-MM-dd"),
            TriggerType = "TestWake",
            TriggerCorrelationId = triggerCorrelationId,
            ActualStartUtc = startUtc,
            OverallStatus = AutomationStatuses.Running,
            RefreshStatus = "",
            RsiStatus = "",
            SnapshotStatus = "",
            ValueScreenerStatus = "",
            MachineName = Environment.MachineName,
        };
        db.AutomationRunLogs.Add(log);
        await db.SaveChangesAsync(ct);
        await RecordHeartbeatAsync(log, "RunLogPersisted", ct);

        await using var lease = await systemAwake.AcquireAsync(CancellationToken.None);
        log.PowerRequestAcquiredAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await RecordHeartbeatAsync(log, "PowerRequestAcquired", ct);

        try
        {
            var admins = await userManager.GetUsersInRoleAsync("Admin");
            log.OwnerUserId = admins.FirstOrDefault()?.Id ?? "";

            var canConnect = await db.Database.CanConnectAsync(ct);
            if (!canConnect)
            {
                await FailAsync(log, "DbConnectivity", "db.Database.CanConnectAsync() returned false.", ct);
                return;
            }
            await RecordHeartbeatAsync(log, "DatabaseConnected", ct);

            // Cheap, read-only reachability check — reuses the same reference symbol as the
            // trading-day guard; never writes anything market-data-related.
            var quote = await marketData.GetQuoteAsync("^GSPC", ct);
            if (quote is null)
            {
                await FailAsync(log, "MarketDataConnectivity", "GetQuoteAsync(^GSPC) returned null.", ct);
                return;
            }
            await RecordHeartbeatAsync(log, "MarketDataConnected", ct);

            log.RefreshStatus = AutomationStatuses.Succeeded; // repurposed here as "infra checks passed"
            log.OverallStatus = AutomationStatuses.Success;
            log.CompletedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException)
        {
            await CancelAsync(log, ct);
        }
        catch (Exception ex)
        {
            await FailAsync(log, log.ErrorStep ?? "TestWake", Truncate(ex.Message, 2000), ct, ex);
        }
        finally
        {
            log.PowerRequestReleasedAtUtc = DateTime.UtcNow;
            try { await db.SaveChangesAsync(CancellationToken.None); } catch { /* best effort */ }
        }
    }

    private async Task RunRefreshAsync(AutomationRunLog log, string ownerId, CancellationToken ct)
    {
        log.RefreshStartedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        try
        {
            var result = await dataRefresh.RefreshAllAsync(ownerId, ct);
            log.RefreshStatus = AutomationStatuses.Succeeded;
            log.RefreshCompletedAtUtc = DateTime.UtcNow;
            log.PortfolioSymbolCount = result.PortfolioSymbolCount;
            log.WatchlistSymbolCount = result.WatchlistSymbolCount;
        }
        catch (OperationCanceledException)
        {
            // Let the caller record a distinct Cancelled status rather than a misleading Refresh failure.
            throw;
        }
        catch (Exception ex)
        {
            log.RefreshStatus = AutomationStatuses.Failed;
            log.ErrorStep = "Refresh";
            log.ErrorMessage = Truncate(ex.Message, 2000);
            logger.LogError(ex, "[EodAutomation] RefreshAllAsync failed for run {RunId}.", log.RunId);
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task RecordHeartbeatAsync(AutomationRunLog log, string step, CancellationToken ct)
    {
        log.LastHeartbeatAtUtc = DateTime.UtcNow;
        log.LastHeartbeatStep = step;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Bounded by both AutomationKeepAwakeUntil (business-time driven) and MaxPollMinutes
    /// (safety cap) so a manual run fired hours before the business windows finalizes quickly with
    /// NotEligible rather than blocking for the rest of the trading day.</summary>
    private DateTime ComputePollDeadlineUtc(DateTime startUtc, TimeZoneInfo tz)
    {
        var keepAwakeUntilEt = automationConfig.ComputeKeepAwakeUntilEt(scannerConfig.EodWindowEnd, GetValueScreenerTimeEtSafe());
        var nowEt = TimeZoneInfo.ConvertTimeFromUtc(startUtc, tz);
        var keepAwakeUntilTimeOfDay = TimeSpan.TryParse(keepAwakeUntilEt, out var t) ? t : new TimeSpan(16, 45, 0);
        var keepAwakeUntilEtDateTime = nowEt.Date.Add(keepAwakeUntilTimeOfDay);
        if (keepAwakeUntilEtDateTime < nowEt) keepAwakeUntilEtDateTime = nowEt; // already past — don't poll into the past

        var keepAwakeUntilUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(keepAwakeUntilEtDateTime, DateTimeKind.Unspecified), tz);
        var maxCapUtc = startUtc.AddMinutes(automationConfig.MaxPollMinutes);
        return keepAwakeUntilUtc < maxCapUtc ? keepAwakeUntilUtc : maxCapUtc;
    }

    private string GetValueScreenerTimeEtSafe()
    {
        try
        {
            var cfg = valueScreenerPersistence.GetOrCreateScheduleConfigAsync().GetAwaiter().GetResult();
            return cfg.ScheduledTimeEt;
        }
        catch { return "17:00"; }
    }

    private async Task PollForCompletionAsync(AutomationRunLog log, TimeZoneInfo tz, DateTime pollDeadlineUtc, CancellationToken ct)
    {
        while (true)
        {
            var nowEt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);

            await EvaluateRsiAsync(log, nowEt, ct);
            await EvaluateSnapshotAsync(log, nowEt, ct);
            await EvaluateValueScreenerAsync(log, nowEt, tz, ct);
            await db.SaveChangesAsync(ct);
            await RecordHeartbeatAsync(log, "CompletionPolling", ct);

            if (log.SnapshotStatus == AutomationStatuses.Succeeded && log.RsiStatus == AutomationStatuses.Succeeded)
                break; // both fully observed as good outcomes — release the awake lease early

            if (DateTime.UtcNow >= pollDeadlineUtc) break;

            try { await Task.Delay(PollInterval, ct); }
            catch (OperationCanceledException)
            {
                // App shutting down or user-cancelled mid-run: mark NotObserved (not NotEligible) if
                // the window had already been reached, since our observation was genuinely cut short.
                // Rethrow so the caller records a distinct Cancelled status rather than silently
                // reporting whatever partial Success/PartialSuccess DeriveOverallStatus would compute.
                var cutoffEt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
                if (log.RsiStatus == AutomationStatuses.NotEligible && HasReachedEodWindow(cutoffEt))
                    log.RsiStatus = AutomationStatuses.NotObserved;
                await db.SaveChangesAsync(CancellationToken.None);
                throw;
            }
        }
    }

    private bool HasReachedEodWindow(DateTime nowEt) =>
        TimeSpan.TryParse(scannerConfig.EodWindowStart, out var start) && nowEt.TimeOfDay >= start;

    private Task EvaluateRsiAsync(AutomationRunLog log, DateTime nowEt, CancellationToken ct)
    {
        if (!HasReachedEodWindow(nowEt))
        {
            log.RsiStatus = AutomationStatuses.NotEligible;
            return Task.CompletedTask;
        }

        log.RsiStatus = AutomationStatuses.Succeeded; // window reached; zero new signals today is a valid outcome
        log.RsiCompletedAtUtc = DateTime.UtcNow;
        return CountTodaysEodSignalsAsync(log, ct);
    }

    private async Task CountTodaysEodSignalsAsync(AutomationRunLog log, CancellationToken ct)
    {
        log.EodSignalsPersistedCount = await db.DailySignals
            .Where(s => (s.TradingDate ?? s.SignalDate) == log.TradingDate)
            .CountAsync(ct);
    }

    private async Task EvaluateSnapshotAsync(AutomationRunLog log, DateTime nowEt, CancellationToken ct)
    {
        if (nowEt.TimeOfDay < SnapshotWindowStart)
        {
            log.SnapshotStatus = AutomationStatuses.NotEligible;
            return;
        }

        var row = await db.PortfolioValueHistories
            .Where(h => h.RecordedDate == log.TradingDate)
            .OrderByDescending(h => h.RecordedAt)
            .FirstOrDefaultAsync(ct);

        if (row is not null)
        {
            log.SnapshotStatus = AutomationStatuses.Succeeded;
            log.SnapshotCompletedAtUtc = DateTime.UtcNow;
            log.SnapshotSource = row.Source.ToString();
        }
        else
        {
            // Window reached but no row yet — pending, distinct from NotEligible (never reached).
            // Only becomes a real Failed if this is still true once the poll loop's deadline is
            // reached (see DeriveOverallStatus) — RSI's NotObserved stays informational (zero
            // signals is valid) but Snapshot's does not, since exactly one row/day is expected.
            log.SnapshotStatus = AutomationStatuses.NotObserved;
        }
    }

    private async Task EvaluateValueScreenerAsync(AutomationRunLog log, DateTime nowEt, TimeZoneInfo tz, CancellationToken ct)
    {
        var cfg = await valueScreenerPersistence.GetOrCreateScheduleConfigAsync(ct);
        if (!cfg.Enabled)
        {
            log.ValueScreenerStatus = AutomationStatuses.NotScheduled;
            return;
        }

        var lastRun = new[] { cfg.LastPortfolioRunAt, cfg.LastWatchlistRunAt }
            .Where(d => d.HasValue)
            .Select(d => d!.Value)
            .DefaultIfEmpty()
            .Max();

        // LastPortfolioRunAt/LastWatchlistRunAt are stored as UTC (matches ValueScreenerSchedulerService's
        // own comparison) — convert to ET before comparing dates, not raw UTC date.
        if (lastRun != default && TimeZoneInfo.ConvertTimeFromUtc(lastRun, tz).Date == nowEt.Date)
        {
            log.ValueScreenerStatus = AutomationStatuses.Succeeded;
            log.ValueScreenerLastRunAtUtc = lastRun;
        }
        else
        {
            log.ValueScreenerStatus = AutomationStatuses.NotObserved;
        }
    }

    /// <summary>Applied once, after the poll loop exits. Snapshot's NotObserved (window reached, no row
    /// by deadline) becomes a real Failed — unlike RSI's NotObserved, which stays informational because
    /// zero confirmed signals is a valid outcome, exactly one PortfolioValueHistory row/day is expected
    /// by design, so its absence after the window fully closed is a genuine problem.
    /// Internal (not private) so PortfolioManager.Tests can exercise it directly — see InternalsVisibleTo
    /// in PortfolioManager.Api.csproj (same pattern already used elsewhere in this project).</summary>
    internal static string DeriveOverallStatus(AutomationRunLog log)
    {
        if (log.SnapshotStatus == AutomationStatuses.NotObserved)
            log.SnapshotStatus = AutomationStatuses.Failed;

        if (log.RefreshStatus == AutomationStatuses.Failed || log.SnapshotStatus == AutomationStatuses.Failed)
            return AutomationStatuses.Failed;
        if (log.RsiStatus == AutomationStatuses.NotObserved)
            return AutomationStatuses.PartialSuccess;
        return AutomationStatuses.Success;
    }

    private async Task CancelAsync(AutomationRunLog log, CancellationToken ct)
    {
        log.OverallStatus = AutomationStatuses.Cancelled;
        log.ErrorStep = null;
        log.ErrorMessage = "Cancelled by admin.";
        log.CompletedAtUtc = DateTime.UtcNow;
        logger.LogInformation("[EodAutomation] Run {RunId} was cancelled.", log.RunId);
        try { await db.SaveChangesAsync(CancellationToken.None); } catch { /* best effort */ }
    }

    private async Task FailAsync(AutomationRunLog log, string step, string message, CancellationToken ct, Exception? ex = null)
    {
        log.OverallStatus = AutomationStatuses.Failed;
        log.ErrorStep = step;
        log.ErrorMessage = message;
        log.CompletedAtUtc = DateTime.UtcNow;
        if (ex is not null) logger.LogError(ex, "[EodAutomation] Run {RunId} failed at step {Step}.", log.RunId, step);
        else logger.LogError("[EodAutomation] Run {RunId} failed at step {Step}: {Message}", log.RunId, step, message);
        try { await db.SaveChangesAsync(CancellationToken.None); } catch { /* best effort */ }
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
