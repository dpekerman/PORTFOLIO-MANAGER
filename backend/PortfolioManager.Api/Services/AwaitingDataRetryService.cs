using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services.FinalAction;

namespace PortfolioManager.Api.Services;

/// <summary>When a retry of symbols stuck on "Awaiting Data" is safe to run.</summary>
public sealed record AwaitingDataRetryWindow(bool Allowed, DateTime? AvailableAtUtc, string? Reason);

public sealed record AwaitingDataStatusDto(
    IReadOnlyList<string> Symbols,
    string? TradingDate,
    bool CanRetry,
    DateTime? AvailableAtUtc,
    string? Reason);

public sealed record AwaitingDataRetryResultDto(
    string Status, // Resolved | StillAwaiting | NotYet | AlreadyRunning | NothingToRetry
    int Published,
    IReadOnlyList<string> StillAwaiting,
    string Message);

public interface IAwaitingDataRetryService
{
    Task<AwaitingDataStatusDto> GetStatusAsync(CancellationToken ct);
    Task<AwaitingDataRetryResultDto> RetryAsync(string ownerId, CancellationToken ct);
}

/// <summary>
/// Re-evaluates Final Actions for the latest run that finished PartialSuccess because some symbols were "Awaiting Data".
/// Re-uses exactly the evaluation / publication / audit path of the EOD run, so it can only add decisions backed by
/// fresh, valid evidence; symbols that are still invalid stay Awaiting Data with their previous record preserved.
/// </summary>
public sealed class AwaitingDataRetryService(
    AppDbContext db,
    IFinalActionService finalActions,
    ISignalsAuditService signalsAudit,
    IAutomationRunCoordinator coordinator,
    ILogger<AwaitingDataRetryService> logger) : IAwaitingDataRetryService
{
    /// <summary>Extra wait after <see cref="TradingCalendar.EvidenceGrace"/>: a session only counts as completed then, and the provider needs a few minutes to settle its final bar.</summary>
    public static readonly TimeSpan SettleBuffer = TimeSpan.FromMinutes(5);

    public const string RetryTriggerType = "RetryAwaitingData";
    private const string Marker = "Awaiting Data (not evaluated): ";
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private static readonly Regex Item = new(
        @"\G(?:, )?(?<sym>[A-Za-z0-9.^=\-]+)(?: \(analysis:[^)]*\))?(?: \[[^\]]*\])?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string BuildPartialMessage(FinalActionSnapshotDto snapshot, int published)
    {
        var awaiting = snapshot.Meta?.UnknownSymbols.OrderBy(s => s, StringComparer.Ordinal).ToList() ?? [];
        var detail = string.Join(", ", awaiting.Select(s =>
        {
            var row = snapshot.Portfolio.Concat(snapshot.Watchlist)
                .FirstOrDefault(r => string.Equals(r.Symbol, s, StringComparison.OrdinalIgnoreCase)
                    && r.EvaluationState == nameof(EffectiveState.AwaitingData));
            var why = row is null ? "" : $" [{row.EvidenceHealth}{(row.ReasonCodes is { Count: > 0 } c ? ": " + string.Join("/", c) : "")}]";
            var bar = row?.RejectedBar;
            var analysis = bar is null ? "" : $" (analysis: {bar.AnalysisTicker}, session: {bar.TradingDate:yyyy-MM-dd}; recovery unverified)";
            return s + analysis + why;
        }));
        var message = $"{published} holding(s) published; {Marker}{detail}";
        return message.Length <= 2000 ? message : message[..2000];
    }

    /// <summary>Symbols named in a FinalActionPartial error message. Tolerates the 2000-character truncation.</summary>
    public static IReadOnlyList<string> ParseSymbols(string? errorMessage)
    {
        if (string.IsNullOrEmpty(errorMessage)) return [];
        var start = errorMessage.IndexOf(Marker, StringComparison.Ordinal);
        if (start < 0) return [];
        start += Marker.Length;
        var symbols = new List<string>();
        var pos = start;
        while (Item.Match(errorMessage, pos) is { Success: true } m && m.Length > 0)
        {
            symbols.Add(m.Groups["sym"].Value.ToUpperInvariant());
            pos += m.Length;
        }
        return symbols.Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// A retry is meaningful only once the latest session counts as completed (close + evidence grace) and the provider's
    /// final bar has settled. Earlier, the evaluation would just call the same unfinished session "stale" again.
    /// </summary>
    public static AwaitingDataRetryWindow EvaluateWindow(DateTime utcNow)
    {
        var et = TradingCalendar.TorontoNow(utcNow);
        var today = DateOnly.FromDateTime(et);
        foreach (var exchange in new[] { MarketExchange.Nyse, MarketExchange.Tsx })
        {
            if (!TradingCalendar.IsTradingDay(today, exchange)) continue;
            var availableUtc = TradingCalendar.SessionCloseUtc(today, exchange) + TradingCalendar.EvidenceGrace + SettleBuffer;
            if (utcNow < availableUtc)
                return new AwaitingDataRetryWindow(false, availableUtc,
                    "Today's session is still settling. Retry is available after the market data is final.");
        }
        return new AwaitingDataRetryWindow(true, null, null);
    }

    private Task<AutomationRunLog?> LatestRunAsync(CancellationToken ct) =>
        db.AutomationRunLogs.AsNoTracking()
            .Where(r => r.TriggerType != "TestWake")
            .OrderByDescending(r => r.ActualStartUtc)
            .FirstOrDefaultAsync(ct);

    public async Task<AwaitingDataStatusDto> GetStatusAsync(CancellationToken ct)
    {
        var last = await LatestRunAsync(ct);
        if (last is null || last.ErrorStep != EodAutomationOrchestratorService.PartialEvaluationStep)
            return new AwaitingDataStatusDto([], last?.TradingDate, false, null, null);

        var symbols = ParseSymbols(last.ErrorMessage);
        var window = EvaluateWindow(DateTime.UtcNow);
        if (coordinator.IsRunning)
            return new AwaitingDataStatusDto(symbols, last.TradingDate, false, null, "An automation run is in progress.");
        return new AwaitingDataStatusDto(symbols, last.TradingDate, window.Allowed, window.AvailableAtUtc, window.Reason);
    }

    public async Task<AwaitingDataRetryResultDto> RetryAsync(string ownerId, CancellationToken ct)
    {
        if (coordinator.IsRunning || !await Gate.WaitAsync(0, ct))
            return new AwaitingDataRetryResultDto("AlreadyRunning", 0, [], "An automation run or retry is already in progress.");
        try
        {
            var last = await LatestRunAsync(ct);
            if (last is null || last.ErrorStep != EodAutomationOrchestratorService.PartialEvaluationStep)
                return new AwaitingDataRetryResultDto("NothingToRetry", 0, [], "No symbols are awaiting data.");

            var window = EvaluateWindow(DateTime.UtcNow);
            if (!window.Allowed)
                return new AwaitingDataRetryResultDto("NotYet", 0, ParseSymbols(last.ErrorMessage), window.Reason!);

            var startUtc = DateTime.UtcNow;
            var snapshot = await finalActions.ComputeAsync(ownerId, fresh: true, ct);
            var published = await finalActions.PublishPortfolioActionsAsync(ownerId, snapshot, ct);
            var stillAwaiting = snapshot.Meta?.UnknownSymbols.OrderBy(s => s, StringComparer.Ordinal).ToList() ?? [];

            var complete = EodAutomationOrchestratorService.IsEvaluationComplete(last) && stillAwaiting.Count == 0;
            try
            {
                await signalsAudit.CaptureAsync(ownerId, snapshot,
                    new CaptureOptions(IsEodPass: true, EvaluationComplete: complete, RunStartedUtc: startUtc), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "[AwaitingDataRetry] Signals Audit capture failed; the retry result is still recorded.");
            }

            var resolved = stillAwaiting.Count == 0;
            db.AutomationRunLogs.Add(new AutomationRunLog
            {
                RunId = Guid.NewGuid(),
                TradingDate = last.TradingDate,
                TriggerType = RetryTriggerType,
                ActualStartUtc = startUtc,
                CompletedAtUtc = DateTime.UtcNow,
                OverallStatus = resolved ? AutomationStatuses.Success : AutomationStatuses.PartialSuccess,
                RefreshStatus = last.RefreshStatus,
                RefreshStartedAtUtc = last.RefreshStartedAtUtc,
                RefreshCompletedAtUtc = last.RefreshCompletedAtUtc,
                PortfolioSymbolCount = last.PortfolioSymbolCount,
                WatchlistSymbolCount = last.WatchlistSymbolCount,
                RsiStatus = last.RsiStatus,
                RsiCompletedAtUtc = last.RsiCompletedAtUtc,
                EodSignalsPersistedCount = last.EodSignalsPersistedCount,
                SnapshotStatus = last.SnapshotStatus,
                SnapshotCompletedAtUtc = last.SnapshotCompletedAtUtc,
                SnapshotSource = last.SnapshotSource,
                ValueScreenerStatus = last.ValueScreenerStatus,
                ValueScreenerLastRunAtUtc = last.ValueScreenerLastRunAtUtc,
                ErrorStep = resolved ? null : EodAutomationOrchestratorService.PartialEvaluationStep,
                ErrorMessage = resolved ? null : BuildPartialMessage(snapshot, published),
                OwnerUserId = ownerId,
                MachineName = Environment.MachineName,
            });
            await db.SaveChangesAsync(ct);

            return resolved
                ? new AwaitingDataRetryResultDto("Resolved", published, [], $"All symbols evaluated; {published} holding(s) published.")
                : new AwaitingDataRetryResultDto("StillAwaiting", published, stillAwaiting,
                    $"Still awaiting data: {string.Join(", ", stillAwaiting)}. Their previous recommendations are unchanged.");
        }
        finally
        {
            Gate.Release();
        }
    }
}
