using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PortfolioManager.Api.Services.FinalAction;
using static PortfolioManager.Tests.Pm029FinalActionGoldenTests;

namespace PortfolioManager.Tests;

/// <summary>PM-029 Phase 4a: evaluation bundle, single-flight and the side-effect-free universe evaluation.</summary>
public sealed class Pm029FinalActionRefactorTests
{
    private static JsonNode WithoutSource(FinalActionResultDto row)
    {
        var node = JsonNode.Parse(Normalise(new FinalActionSnapshotDto([row], [], DateTime.UnixEpoch)))!["watchlist"]![0]!.AsObject();
        node.Remove("source");
        return node;
    }

    [Fact]
    public async Task UniverseRow_IsIdenticalToTheWatchlistRow_ForTheSameUnownedSymbol()
    {
        var (service, db, _) = await SetUpGoldenAsync();
        // The universe path judges with role Strategic; give the watchlist item the same role so both paths see identical input.
        foreach (var w in db.WatchlistItems.Where(w => w.Symbol == "AAA.TO" || w.Symbol == "DDD")) w.Role = "Strategic";
        await db.SaveChangesAsync();

        var snapshot = await service.ComputeAsync("u", fresh: true);
        var universe = await service.EvaluateUniverseAsync("u", ["AAA.TO", "DDD"]);

        foreach (var symbol in new[] { "AAA.TO", "DDD" })
        {
            var watch = snapshot.Watchlist.Single(r => r.Symbol == symbol);
            var uni = universe.Rows.Single(r => r.Symbol == symbol);
            Assert.Equal(FinalActionService.UniverseSource, uni.Source);
            Assert.Equal(WithoutSource(watch).ToJsonString(), WithoutSource(uni).ToJsonString());
        }
    }

    [Fact]
    public async Task UniverseEvaluation_ScansReadOnly_AndNeverTouchesTheUsersFinalActions()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var (service, db, scanner) = await SetUpGoldenAsync(cache);
        var portfolioBefore = (await db.PortfolioSnapshots.AsNoTracking().SingleAsync()).SnapshotJson;

        var universe = await service.EvaluateUniverseAsync("u", ["AAA.TO", "ZZZ.TO"]);

        Assert.Equal(0, scanner.PersistingCalls);
        Assert.True(scanner.ReadOnlyCalls > 0);
        Assert.False(cache.TryGetValue("final-actions:u", out _));
        Assert.False(cache.TryGetValue("final-actions-bundle:u", out _));
        Assert.Null(service.TryGetCached("u"));
        Assert.Equal(portfolioBefore, (await db.PortfolioSnapshots.AsNoTracking().SingleAsync()).SnapshotJson);
        // A symbol without data is Awaiting Data, never a decision.
        var missing = universe.Rows.Single(r => r.Symbol == "ZZZ.TO");
        Assert.Equal("AwaitingData", missing.EvaluationState);
        Assert.Contains("ZZZ.TO", universe.Unknown);
    }

    [Fact]
    public async Task ConcurrentFreshEvaluations_ShareOneScan_AndOneBundle()
    {
        var (service, _, scanner) = await SetUpGoldenAsync();
        await service.EvaluateAsync("u", fresh: true);
        var callsForOneEvaluation = scanner.PersistingCalls;

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => service.EvaluateAsync("u", fresh: true)));

        // Every concurrent caller arrived while the first was computing: they all reuse that result.
        Assert.Single(results.Select(r => r.EvaluationId).Distinct());
        Assert.True(scanner.PersistingCalls <= callsForOneEvaluation * 2,
            $"expected at most one extra evaluation, got {scanner.PersistingCalls - callsForOneEvaluation} extra scan call(s) for {callsForOneEvaluation} per evaluation");
    }

    [Fact]
    public async Task Bundle_CarriesTheExactScansAndSnapshot_ThatComputeReturns()
    {
        var (service, _, _) = await SetUpGoldenAsync();
        var bundle = await service.EvaluateAsync("u", fresh: true);
        var snapshot = await service.ComputeAsync("u");

        Assert.Same(bundle.Snapshot, snapshot);
        foreach (var row in snapshot.Watchlist.Concat(snapshot.Portfolio).Where(r => r.EvaluationState == "CurrentValid"))
        {
            Assert.True(bundle.Scans.TryGetValue(row.Symbol, out var scan));
            Assert.Equal(row.AnalysisPrice, scan!.CurrentPrice);
        }
        Assert.Equal(snapshot.Portfolio.Select(p => p.ItemId).Distinct().Count(), bundle.Portfolio.OpenHoldings.Count);
        Assert.True(bundle.Portfolio.GrandTotal > 0);
    }
}
