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

    public bool IsRunning
    {
        get { lock (_lock) { return _inFlightTask is { IsCompleted: false }; } }
    }

    public Guid StartRun(string triggerType) => Start(triggerType, isTestWake: false);

    public Guid StartTestWake() => Start("TestWake", isTestWake: true);

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
            _inFlightRunId = runId;
            _inFlightTask = Task.Run(() => ExecuteInNewScopeAsync(runId, triggerType, isTestWake, appLifetime.ApplicationStopping));
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
        }
        catch (Exception ex)
        {
            // Last-resort safety net — the orchestrator itself is responsible for writing a Failed
            // AutomationRunLog row on error; this only fires if even that couldn't happen (e.g. DB down).
            logger.LogError(ex, "[AutomationRunCoordinator] Run {RunId} ({TriggerType}) threw unhandled exception.", runId, triggerType);
        }
        finally
        {
            lock (_lock)
            {
                if (_inFlightRunId == runId) { _inFlightRunId = null; _inFlightTask = null; }
            }
        }
    }
}
