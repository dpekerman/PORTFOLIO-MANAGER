using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
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
        var controller = new OpportunitiesController(service, null!, lifetime, new OpportunityScheduleConfig())
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
        var controller = new OpportunitiesController(service, null!, new Lifetime(), new OpportunityScheduleConfig())
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext() },
        };

        var result = await controller.Refresh();

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ObjectResult>(result.Result).StatusCode);
    }

    [Fact]
    public async Task RestoreConstituent_UndoesARetire_ButNotWhenTheSymbolIsAlreadyCurrent()
    {
        await using var db = new PortfolioManager.Api.Data.AppDbContext(new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<PortfolioManager.Api.Data.AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var retired = new IndexConstituent { IndexCode = "TSX60", Symbol = "WSP.TO", Source = "s", EffectiveFrom = new DateTime(2026, 11, 12), EffectiveTo = new DateTime(2026, 11, 16) };
        db.IndexConstituents.Add(retired);
        await db.SaveChangesAsync();
        var controller = new OpportunitiesController(new CapturingService(), db, new Lifetime(), new OpportunityScheduleConfig());

        var restored = Assert.IsType<OkObjectResult>((await controller.RestoreConstituent(retired.Id, default)).Result);
        Assert.Null(Assert.IsType<IndexConstituent>(restored.Value).EffectiveTo);

        retired.EffectiveTo = new DateTime(2026, 11, 16);
        db.IndexConstituents.Add(new IndexConstituent { IndexCode = "TSX60", Symbol = "WSP.TO", Source = "s", EffectiveFrom = new DateTime(2026, 11, 17) });
        await db.SaveChangesAsync();
        var conflict = await controller.RestoreConstituent(retired.Id, default);
        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ObjectResult>(conflict.Result).StatusCode);
    }
}
