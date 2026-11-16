using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using PortfolioManager.Api.Controllers;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services.Opportunities;

namespace PortfolioManager.Tests;

public sealed class OpportunitiesControllerTests
{
    private sealed class CapturingService : IOpportunityService
    {
        public CancellationToken RefreshToken;
        public Exception? Throw;

        public Task<OpportunitySnapshotDto> GetLatestAsync(string userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<OpportunityDetailDto?> GetDetailAsync(string userId, string symbol, int? snapshotId, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<OpportunitySnapshotDto> RefreshAsync(string userId, CancellationToken ct = default)
        {
            RefreshToken = ct;
            return Throw is null ? Task.FromResult<OpportunitySnapshotDto>(null!) : Task.FromException<OpportunitySnapshotDto>(Throw);
        }
    }

    private sealed class Lifetime : IHostApplicationLifetime
    {
        public CancellationTokenSource Stopping { get; } = new();
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => Stopping.Token;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() => Stopping.Cancel();
    }

    [Fact]
    public async Task Refresh_IsNotCancelledByTheClosedBrowserRequest_OnlyByApplicationShutdown()
    {
        var service = new CapturingService();
        var lifetime = new Lifetime();
        using var requestAborted = new CancellationTokenSource();
        var controller = new OpportunitiesController(service, null!, lifetime)
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext { RequestAborted = requestAborted.Token } },
        };

        await controller.Refresh();
        requestAborted.Cancel();

        Assert.False(service.RefreshToken.IsCancellationRequested);
        lifetime.StopApplication();
        Assert.True(service.RefreshToken.IsCancellationRequested);
    }

    [Fact]
    public async Task Refresh_ReportsConflict_WhenOneIsAlreadyRunning()
    {
        var service = new CapturingService { Throw = new OpportunityRefreshInProgressException() };
        var controller = new OpportunitiesController(service, null!, new Lifetime())
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext() },
        };

        var result = await controller.Refresh();

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ObjectResult>(result.Result).StatusCode);
    }
}
