using System.Net;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public interface IAutomationRunNotificationService
{
    Task SendRunSummaryAsync(AutomationRunLog run, CancellationToken ct = default);

    Task SendOperationSummaryAsync(
        string operationKey,
        string action,
        string subject,
        string htmlBody,
        CancellationToken ct = default);
}

/// <summary>Sends one durable, best-effort summary per coordinator-backed Automation run.</summary>
public sealed class AutomationRunNotificationService(
    AppDbContext db,
    EmailNotificationService email,
    ILogger<AutomationRunNotificationService> logger) : IAutomationRunNotificationService
{
    public async Task SendRunSummaryAsync(AutomationRunLog run, CancellationToken ct = default)
    {
        await SendOperationSummaryAsync(
            operationKey: $"run:{run.RunId:N}",
            action: run.TriggerType,
            subject: $"Portfolio Manager - Automation {run.OverallStatus}: {run.TriggerType} ({run.TradingDate})",
            htmlBody: BuildHtml(run),
            ct);
    }

    public async Task SendOperationSummaryAsync(
        string operationKey,
        string action,
        string subject,
        string htmlBody,
        CancellationToken ct = default)
    {
        var record = new AutomationNotificationRecord
        {
            OperationKey = operationKey,
            Action = action,
            Status = "Sending",
            AttemptCount = 1,
        };

        try
        {
            db.AutomationNotificationRecords.Add(record);
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            var existing = await db.AutomationNotificationRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.OperationKey == operationKey, ct);
            if (existing?.Status == "Sent")
                return;
            logger.LogWarning("[AutomationEmail] Notification already claimed for {OperationKey}; skipping duplicate send.", operationKey);
            return;
        }

        var result = await email.SendAutomationSummaryAsync(subject, htmlBody, ct);
        record.Status = result.Success ? "Sent" : "Failed";
        record.SentAtUtc = result.Success ? DateTime.UtcNow : null;
        record.ErrorMessage = result.Success ? null : result.Message;
        try
        {
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[AutomationEmail] Could not persist notification result for {OperationKey}.", operationKey);
        }
    }

    private static string BuildHtml(AutomationRunLog run)
    {
        static string E(string? value) => WebUtility.HtmlEncode(value ?? "-");
        static string Dt(DateTime? value) => value?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'") ?? "-";
        var duration = run.CompletedAtUtc.HasValue
            ? run.CompletedAtUtc.Value - run.ActualStartUtc
            : (TimeSpan?)null;

        return $"""
            <!DOCTYPE html>
            <html><body style="font-family:Segoe UI,Arial,sans-serif;color:#263238">
            <h2>Portfolio Manager Automation Summary</h2>
            <p><strong>Status:</strong> {E(run.OverallStatus)}<br>
            <strong>Action:</strong> {E(run.TriggerType)}<br>
            <strong>Trading date (ET):</strong> {E(run.TradingDate)}<br>
            <strong>Run ID:</strong> {E(run.RunId.ToString())}<br>
            <strong>Machine:</strong> {E(run.MachineName)}</p>
            <h3>Observed operations</h3>
            <ul>
              <li>Refresh: {E(run.RefreshStatus)} ({run.PortfolioSymbolCount} portfolio / {run.WatchlistSymbolCount} watchlist symbols)</li>
              <li>RSI/EOD Signals: {E(run.RsiStatus)} ({run.EodSignalsPersistedCount} persisted)</li>
              <li>Portfolio Snapshot: {E(run.SnapshotStatus)}; source {E(run.SnapshotSource)}</li>
              <li>Value Screener: {E(run.ValueScreenerStatus)}; last run {E(Dt(run.ValueScreenerLastRunAtUtc))}</li>
            </ul>
            <h3>Timing</h3>
            <p>Started: {E(Dt(run.ActualStartUtc))}<br>
            Completed: {E(Dt(run.CompletedAtUtc))}<br>
            Duration: {E(duration?.ToString() ?? "-")}<br>
            Keep-awake acquired: {E(Dt(run.PowerRequestAcquiredAtUtc))}<br>
            Keep-awake released: {E(Dt(run.PowerRequestReleasedAtUtc))}</p>
            <p><strong>Error step:</strong> {E(run.ErrorStep)}<br>
            <strong>Error:</strong> {E(run.ErrorMessage)}</p>
            <p style="color:#607d8b">This is an observed pipeline summary. It does not claim that every internal market-data request succeeded.</p>
            </body></html>
            """;
    }
}