using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Tests;

public sealed class AutomationRuntimeConfigTests
{
    [Fact]
    public void ComputeKeepAwakeUntilEt_UsesLaterOfEodEndOrValueScreenerTime_PlusGrace()
    {
        var cfg = new AutomationRuntimeConfig { CompletionGraceMinutes = 15 };

        cfg.ComputeKeepAwakeUntilEt(eodWindowEndEt: "16:30", valueScreenerTimeEt: "17:00").Should().Be("17:15");
    }

    [Fact]
    public void ComputeKeepAwakeUntilEt_EodEndLaterThanValueScreener_StillUsesTheLaterOne()
    {
        var cfg = new AutomationRuntimeConfig { CompletionGraceMinutes = 10 };

        cfg.ComputeKeepAwakeUntilEt(eodWindowEndEt: "18:00", valueScreenerTimeEt: "17:00").Should().Be("18:10");
    }

    [Fact]
    public void ComputeKeepAwakeUntilEt_ExplicitOverride_TakesPrecedenceOverComputedDefault()
    {
        var cfg = new AutomationRuntimeConfig { KeepAwakeUntilEtOverride = "20:00", CompletionGraceMinutes = 15 };

        cfg.ComputeKeepAwakeUntilEt(eodWindowEndEt: "16:30", valueScreenerTimeEt: "17:00").Should().Be("20:00");
    }
}

public sealed class TradingSessionGuardTests
{
    [Fact]
    public async Task IsTodayATradingDayAsync_LatestBarMatchesToday_ReturnsTrue()
    {
        var todayEt = TodayEtDate();
        var guard = new TradingSessionGuard(new StubMarketData(todayEt), NullLogger<TradingSessionGuard>.Instance);

        (await guard.IsTodayATradingDayAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task IsTodayATradingDayAsync_LatestBarIsStale_ReturnsFalse()
    {
        // Simulates a weekday market holiday: the reference symbol's last bar is still Friday's,
        // not today — this is exactly the pre-existing gap this guard was added to close.
        var staleDate = TodayEtDate().AddDays(-3);
        var guard = new TradingSessionGuard(new StubMarketData(staleDate), NullLogger<TradingSessionGuard>.Instance);

        (await guard.IsTodayATradingDayAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task IsTodayATradingDayAsync_NoDataReturned_FailsOpen()
    {
        var guard = new TradingSessionGuard(new StubMarketData(bar: null), NullLogger<TradingSessionGuard>.Instance);

        (await guard.IsTodayATradingDayAsync()).Should().BeTrue();
    }

    private static DateOnly TodayEtDate()
    {
        var tz = MarketHoursGate.GetEasternTimeZone()!;
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz));
    }

    private sealed class StubMarketData(DateOnly? bar) : IMarketDataProvider
    {
        public Task<StockQuote?> GetQuoteAsync(string symbol, CancellationToken ct = default) =>
            Task.FromResult<StockQuote?>(null);

        public Task<IReadOnlyList<MarketDailyClose>?> GetDailyClosesAsync(string symbol, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<MarketDailyClose>?>(bar is null ? null : [new MarketDailyClose(bar.Value, 100m)]);

        public Task<Dictionary<string, StockQuote>> GetBatchQuotesAsync(IEnumerable<string> symbols, CancellationToken ct = default) =>
            Task.FromResult(new Dictionary<string, StockQuote>());

        public Task<(string sector, string industry)> GetSectorAsync(string symbol, CancellationToken ct = default) =>
            Task.FromResult(("", ""));

        public Task<IReadOnlyList<SymbolSearchResult>> SearchSymbolAsync(string query, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SymbolSearchResult>>([]);

        public Task<Dictionary<string, decimal>> GetAnalystTargetsAsync(IEnumerable<string> symbols, CancellationToken ct = default) =>
            Task.FromResult(new Dictionary<string, decimal>());

        public Task<FundamentalsSnapshot?> GetFundamentalsAsync(string symbol, CancellationToken ct = default) =>
            Task.FromResult<FundamentalsSnapshot?>(null);

        public Task<Dictionary<string, DateTime>> GetEarningsDatesAsync(IEnumerable<string> symbols, CancellationToken ct = default) =>
            Task.FromResult(new Dictionary<string, DateTime>());

        public Task<Dictionary<string, decimal>> GetHistoricalClosingPricesAsync(string dateStr, IEnumerable<string> symbols, CancellationToken ct = default) =>
            Task.FromResult(new Dictionary<string, decimal>());
    }
}

public sealed class EodAutomationOverallStatusTests
{
    [Fact]
    public void DeriveOverallStatus_RefreshFailed_IsFailed()
    {
        var entity = MakeLog(refreshStatus: AutomationStatuses.Failed, snapshotStatus: AutomationStatuses.Succeeded, rsiStatus: AutomationStatuses.Succeeded);

        EodAutomationOrchestratorService.DeriveOverallStatus(entity).Should().Be(AutomationStatuses.Failed);
    }

    [Fact]
    public void DeriveOverallStatus_SnapshotNotObservedAfterWindowClosed_BecomesFailed()
    {
        var entity = MakeLog(refreshStatus: AutomationStatuses.Succeeded, snapshotStatus: AutomationStatuses.NotObserved, rsiStatus: AutomationStatuses.Succeeded);

        EodAutomationOrchestratorService.DeriveOverallStatus(entity).Should().Be(AutomationStatuses.Failed);
        entity.SnapshotStatus.Should().Be(AutomationStatuses.Failed); // mutated in place for persistence
    }

    [Fact]
    public void DeriveOverallStatus_RsiNotObserved_DowngradesToPartialSuccess_NeverHardFailed()
    {
        var entity = MakeLog(refreshStatus: AutomationStatuses.Succeeded, snapshotStatus: AutomationStatuses.Succeeded, rsiStatus: AutomationStatuses.NotObserved);

        EodAutomationOrchestratorService.DeriveOverallStatus(entity).Should().Be(AutomationStatuses.PartialSuccess);
    }

    [Fact]
    public void DeriveOverallStatus_ManualRunOutsideBusinessWindows_ReportsSuccessNotFailed()
    {
        // A "Run Automation Now" fired at 10am: RSI/Snapshot are legitimately NotEligible (window
        // never reached), not a failure — this must never be misreported as Failed.
        var entity = MakeLog(refreshStatus: AutomationStatuses.Succeeded, snapshotStatus: AutomationStatuses.NotEligible, rsiStatus: AutomationStatuses.NotEligible);

        EodAutomationOrchestratorService.DeriveOverallStatus(entity).Should().Be(AutomationStatuses.Success);
    }

    private static AutomationRunLog MakeLog(string refreshStatus, string snapshotStatus, string rsiStatus) => new()
    {
        RunId = Guid.NewGuid(),
        TradingDate = "2026-09-07",
        TriggerType = "ManualRunNow",
        ActualStartUtc = DateTime.UtcNow,
        RefreshStatus = refreshStatus,
        SnapshotStatus = snapshotStatus,
        RsiStatus = rsiStatus,
        ValueScreenerStatus = AutomationStatuses.NotScheduled,
        OwnerUserId = "admin-1",
        MachineName = "test",
    };
}

public sealed class AutomationRunCoordinatorTests
{
    [Fact]
    public async Task StartRun_WhileAlreadyInFlight_ReturnsTheSameRunId_NeverStartsADuplicate()
    {
        var orchestrator = new BlockingOrchestrator();
        var provider = new ServiceCollection()
            .AddScoped<IEodAutomationOrchestratorService>(_ => orchestrator)
            .BuildServiceProvider();
        var coordinator = new AutomationRunCoordinator(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new FakeHostLifetime(),
            NullLogger<AutomationRunCoordinator>.Instance);

        var first = coordinator.StartRun("Scheduled");
        var second = coordinator.StartRun("ManualRunNow");

        second.Should().Be(first);
        coordinator.IsRunning.Should().BeTrue();
        await WaitUntil(() => orchestrator.CallCount == 1); // Task.Run start is async — bounded wait, not a fixed sleep
        orchestrator.CallCount.Should().Be(1);

        orchestrator.Release();
        await WaitUntil(() => !coordinator.IsRunning);

        var third = coordinator.StartRun("Scheduled");
        third.Should().NotBe(first);
        orchestrator.Release();
    }

    [Fact]
    public async Task CancelCurrent_WhileRunInFlight_CancelsTheTokenAndReturnsTrue()
    {
        var orchestrator = new BlockingOrchestrator();
        var provider = new ServiceCollection()
            .AddScoped<IEodAutomationOrchestratorService>(_ => orchestrator)
            .BuildServiceProvider();
        var coordinator = new AutomationRunCoordinator(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new FakeHostLifetime(),
            NullLogger<AutomationRunCoordinator>.Instance);

        coordinator.StartRun("ManualRunNow");
        await WaitUntil(() => orchestrator.CallCount == 1);

        var cancelled = coordinator.CancelCurrent();

        cancelled.Should().BeTrue();
        orchestrator.LastToken.IsCancellationRequested.Should().BeTrue();

        orchestrator.Release();
    }

    [Fact]
    public void CancelCurrent_WithNoRunInFlight_ReturnsFalse()
    {
        var provider = new ServiceCollection()
            .AddScoped<IEodAutomationOrchestratorService>(_ => new BlockingOrchestrator())
            .BuildServiceProvider();
        var coordinator = new AutomationRunCoordinator(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new FakeHostLifetime(),
            NullLogger<AutomationRunCoordinator>.Instance);

        coordinator.CancelCurrent().Should().BeFalse();
    }

    private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(10);
        condition().Should().BeTrue("condition should become true within the timeout");
    }

    private sealed class BlockingOrchestrator : IEodAutomationOrchestratorService
    {
        private TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int CallCount;
        public CancellationToken LastToken;

        public Task RunAsync(Guid runId, string triggerType, string? triggerCorrelationId, CancellationToken ct)
        {
            Interlocked.Increment(ref CallCount);
            LastToken = ct;
            return _gate.Task;
        }

        public Task RunTestWakeAsync(Guid runId, string? triggerCorrelationId, CancellationToken ct)
        {
            LastToken = ct;
            return _gate.Task;
        }

        public void Release()
        {
            var old = _gate;
            _gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            old.TrySetResult();
        }
    }

    private sealed class FakeHostLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() { }
    }
}

public sealed class SystemAwakeServiceTests
{
    [Fact]
    public async Task AcquireAsync_LeaseSurvivesThreadPoolHop_AndDisposesWithoutThrowing()
    {
        var service = new SystemAwakeService(NullLogger<SystemAwakeService>.Instance);

        var lease = await service.AcquireAsync();
        // Force the continuation onto a different thread-pool thread — this is exactly the scenario
        // that made SetThreadExecutionState unsafe (thread-affine); the handle-based API must not care.
        await Task.Run(() => Task.Delay(1));

        var act = async () => await lease.DisposeAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task AcquireAsync_ThrowsIfAlreadyCancelled()
    {
        var service = new SystemAwakeService(NullLogger<SystemAwakeService>.Instance);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await service.AcquireAsync(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
