using Microsoft.EntityFrameworkCore;

namespace PortfolioManager.Api.Services;

/// <summary>
/// Enforces single-flight execution of automation runs and decouples the run's lifetime from the
/// HTTP request that started it. A second trigger/run-now/test-wake call while a run is already in
/// flight returns the SAME in-flight runId rather than starting a duplicate. The actual work executes
/// in its own DI scope (via IServiceScopeFactory) using a token linked to application lifetime — never
/// the caller's request-scoped services or RequestAborted token, both of which would be torn down the
/// instant the controller returns 202 Accepted.
/// </summary>
public interface IAutomationRunCoordinator
{
    /// <summary>Starts a new run (or returns the id of one already in flight). Returns immediately.</summary>
    Guid StartRun(string triggerType);

    /// <summary>Starts the non-destructive Test Wake path (or returns the id of one already in flight).</summary>
    Guid StartTestWake();

    /// <summary>Requests cancellation of the in-flight run, if any. Returns false if nothing is running.</summary>
    bool CancelCurrent();

    /// <summary>True if a run is currently executing.</summary>
    bool IsRunning { get; }
}

public sealed class AutomationRunCoordinator(
    IServiceScopeFactory scopeFactory,
    IHostApplicationLifetime appLifetime,
    ILogger<AutomationRunCoordinator> logger) : IAutomationRunCoordinator
{
    private readonly object _lock = new();
    private Guid? _inFlightRunId;
    private Task? _inFlightTask;
    private CancellationTokenSource? _inFlightCts;

    public bool IsRunning
    {
        get { lock (_lock) { return _inFlightTask is { IsCompleted: false }; } }
    }

    public Guid StartRun(string triggerType) => Start(triggerType, isTestWake: false);

    public Guid StartTestWake() => Start("TestWake", isTestWake: true);

    public bool CancelCurrent()
    {
        lock (_lock)
        {
            if (_inFlightRunId is null || _inFlightTask is not { IsCompleted: false } || _inFlightCts is null)
                return false;

            logger.LogInformation("[AutomationRunCoordinator] Cancelling in-flight run {RunId}.", _inFlightRunId);
            _inFlightCts.Cancel();
            return true;
        }
    }

    private Guid Start(string triggerType, bool isTestWake)
    {
        lock (_lock)
        {
            if (_inFlightRunId is { } existingRunId && _inFlightTask is { IsCompleted: false })
            {
                logger.LogInformation(
                    "[AutomationRunCoordinator] Run {RunId} already in flight; ignoring duplicate {TriggerType} request.",
                    existingRunId, triggerType);
                return existingRunId;
            }

            var runId = Guid.NewGuid();
            // Linked (not raw ApplicationStopping) so a user-initiated cancel doesn't affect app shutdown,
            // and app shutdown still cancels the run — both paths flow through the same token.
            var cts = CancellationTokenSource.CreateLinkedTokenSource(appLifetime.ApplicationStopping);
            _inFlightRunId = runId;
            _inFlightCts = cts;
            _inFlightTask = Task.Run(() => ExecuteInNewScopeAsync(runId, triggerType, isTestWake, cts.Token));
            return runId;
        }
    }

    private async Task ExecuteInNewScopeAsync(Guid runId, string triggerType, bool isTestWake, CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var orchestrator = scope.ServiceProvider.GetRequiredService<IEodAutomationOrchestratorService>();
            if (isTestWake)
                await orchestrator.RunTestWakeAsync(runId, ct);
            else
                await orchestrator.RunAsync(runId, triggerType, ct);

            // The orchestrator has returned only after its keep-awake lease was disposed. Email
            // delivery is deliberately outside that lifecycle so SMTP delays cannot hold the PC awake.
            var db = scope.ServiceProvider.GetRequiredService<Data.AppDbContext>();
            var run = await db.AutomationRunLogs.FirstOrDefaultAsync(x => x.RunId == runId, CancellationToken.None);
            if (run is not null)
            {
                var notifications = scope.ServiceProvider.GetRequiredService<IAutomationRunNotificationService>();
                await notifications.SendRunSummaryAsync(run, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            // Last-resort safety net. Normally the orchestrator writes its own terminal
            // AutomationRunLog row and this block is never reached; it also catches the rare case
            // where the post-run notification lookup/send itself throws unexpectedly (e.g. DB down),
            // so a notification failure can never crash the coordinator or leak an unobserved run.
            logger.LogError(ex, "[AutomationRunCoordinator] Run {RunId} ({TriggerType}) threw unhandled exception.", runId, triggerType);
        }
        finally
        {
            lock (_lock)
            {
                if (_inFlightRunId == runId)
                {
                    _inFlightRunId = null;
                    _inFlightTask = null;
                    _inFlightCts?.Dispose();
                    _inFlightCts = null;
                }
            }
        }
    }
}
