using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using PortfolioManager.Api.Services.FinalAction;

namespace PortfolioManager.Tests;

/// <summary>The Action Center / Dashboard action rows can never recreate a decision from legacy rules.</summary>
public sealed class PortfolioActionsServiceTests
{
    private const string User = "u";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // Legacy DeriveAction would have turned these into "ENTRY CANDIDATE" (REQUIRED) and "TRIM" (REQUIRED).
    private static RsiScanResult LegacyBullish(string symbol) => new()
    {
        Symbol = symbol, CompanyName = symbol, ScanType = ScanType.Oversold, Status = SignalStatus.EodConfirm, Rsi = 25m,
        TrendShift = "🟢 Bull Turn", CurrentPrice = 10m,
    };

    private static RsiScanResult LegacyBearish(string symbol) => new()
    {
        Symbol = symbol, CompanyName = symbol, ScanType = ScanType.Overbought, Status = SignalStatus.Confirmed, Rsi = 82m,
        TrendShift = "🟢 Bear Turn", CurrentPrice = 10m,
    };

    private static PortfolioSummaryDto Holding(string symbol, string role, string? published = null, DateTime? publishedAt = null) => new(
        new PortfolioItemDto(1, symbol, symbol, 10m, 9m, "Tech", "", false, false, null, DateTime.UtcNow.AddDays(-60), "OPEN", "TFSA",
            DateTime.UtcNow.AddDays(-60), null, null, role, null, null, null, published, null, null, publishedAt),
        new StockQuote { Symbol = symbol, CurrentPrice = 10m }, PriceStructureResult.None);

    private static async Task<(AppDbContext Db, PortfolioActionsService Svc)> SetUpAsync(
        PortfolioSummaryDto? holding, string? watchSymbol, FinalActionSnapshotDto? cached, params RsiScanResult[] scans)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.PortfolioSnapshots.Add(new PortfolioSnapshot { UserId = User, SnapshotJson = JsonSerializer.Serialize(holding is null ? [] : new[] { holding }, Json) });
        var wl = watchSymbol is null ? [] : new[] { new WatchlistSummaryDto(
            new WatchlistItemDto(5, watchSymbol, "", DateTime.UtcNow, "Core", false, null, "Active"),
            new StockQuote { Symbol = watchSymbol, CurrentPrice = 10m }) };
        db.WatchlistSnapshots.Add(new WatchlistSnapshot { UserId = User, SnapshotJson = JsonSerializer.Serialize(wl, Json) });
        db.RsiScanSnapshots.Add(new RsiScanSnapshot
        {
            Id = 1,
            SnapshotJson = JsonSerializer.Serialize(new ScannerResponse
            {
                OversoldChain = scans.Where(s => s.ScanType == ScanType.Oversold).ToList(),
                OverboughtChain = scans.Where(s => s.ScanType == ScanType.Overbought).ToList(),
            }, Json),
        });
        await db.SaveChangesAsync();
        return (db, new PortfolioActionsService(db, cached is null ? null : new CachedService(cached)));
    }

    private sealed class CachedService(FinalActionSnapshotDto snapshot) : IFinalActionService
    {
        public Task<FinalActionSnapshotDto> ComputeAsync(string userId, bool fresh = false, CancellationToken ct = default) => throw new NotSupportedException("a read must never scan");
        public Task<int> PublishPortfolioActionsAsync(string userId, FinalActionSnapshotDto snapshot, CancellationToken ct = default) => throw new NotSupportedException();
        public FinalActionSnapshotDto? TryGetCached(string userId) => snapshot;
    }

    private static FinalActionResultDto Canonical(string symbol, string source, string action, string state, bool actionable = false,
        string? lastValid = null, DateTime? lastEval = null, string severity = "hold", string priority = "INFORMATIONAL") =>
        new(symbol, source, 1, "Swing", action, FinalActionCatalog.CanonicalId(action), FinalActionCatalog.Describe(action).Category, "Neutral",
            severity, priority, "ma-hold", "", "", "", "", "", null, false, 10m, symbol, null,
            IsActionable: actionable, EvaluationState: state, LastValidAction: lastValid, LastSuccessfulEvaluationUtc: lastEval,
            ReasonCodes: state == "AwaitingData" ? ["AWAITING_STALE_DATA"] : []);

    private static readonly string[] LegacyLabels =
        ["ENTRY CANDIDATE", "STARTER ENTRY", "TRIM", "TRIM WATCH", "EXIT REVIEW", "RISK REVIEW", "ADD WATCH", "ADD CANDIDATE", "BUY WATCH",
         "DO NOT CHASE", "AVOID", "AVOID — TREND DAMAGE", "TECHNICAL REVIEW", "HOLD — EXTENDED", "HOLD — WEAKNESS"];

    [Fact]
    public async Task NoCanonicalAction_NoPublishedAction_IsAwaitingData_NeverALegacyBuyOrSell()          // release 3 / acceptance 2
    {
        var (_, svc) = await SetUpAsync(Holding("HELD", "Swing"), "WL", null, LegacyBearish("HELD"), LegacyBullish("WL"));
        var rows = await svc.GetActionsAsync(User);
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r =>
        {
            Assert.Equal("Awaiting Data", r.ActionLabel);
            Assert.Equal("AwaitingData", r.EvaluationState);
            Assert.False(r.IsActionable);
            Assert.Equal("INFORMATIONAL", r.ActionPriority);
            Assert.DoesNotContain(r.ActionLabel, LegacyLabels);
        });
    }

    [Fact]
    public async Task ValidPriorSessionPublishedAction_IsRetained_WithoutASameDayEvaluation()               // acceptance 1
    {
        var stamp = DateTime.UtcNow.AddMinutes(-5);   // validated in the latest completed session
        var (_, svc) = await SetUpAsync(Holding("HELD", "Swing", "Hold Swing / Watch", stamp), null, null, LegacyBearish("HELD"));
        var row = Assert.Single(await svc.GetActionsAsync(User));
        Assert.Equal("Hold Swing / Watch", row.ActionLabel);
        Assert.Equal("PriorSessionValid", row.EvaluationState);
    }

    [Fact]
    public async Task PublishedActionBeyondItsLifecycle_BecomesAwaitingData_AndKeepsTheLastValidActionSeparately()   // acceptance 5
    {
        var (_, svc) = await SetUpAsync(Holding("HELD", "Swing", "Exit / Cut Loss", DateTime.UtcNow.AddDays(-30)), null, null, LegacyBearish("HELD"));
        var row = Assert.Single(await svc.GetActionsAsync(User));
        Assert.Equal("Awaiting Data", row.ActionLabel);
        Assert.Equal("Exit / Cut Loss", row.LastValidAction);
        Assert.False(row.IsActionable);
    }

    [Fact]
    public async Task FreshCanonicalResult_WinsOverEverything_AndAwaitingCanonicalNeverFallsBackToLegacy()
    {
        var fresh = new FinalActionSnapshotDto([Canonical("WL", "Watchlist", "STARTER ENTRY", "CurrentValid", actionable: true, severity: "buy", priority: "REQUIRED")],
            [Canonical("HELD", "Portfolio", "Hold / Monitor", "CurrentValid")], DateTime.UtcNow);
        var (_, svc) = await SetUpAsync(Holding("HELD", "Swing"), "WL", fresh, LegacyBearish("HELD"), LegacyBullish("WL"));
        var rows = (await svc.GetActionsAsync(User)).ToDictionary(r => r.Symbol);
        Assert.Equal("Hold / Monitor", rows["HELD"].ActionLabel);                      // legacy would have said TRIM
        Assert.Equal("STARTER ENTRY", rows["WL"].ActionLabel);
        Assert.True(rows["WL"].IsActionable);

        var failed = new FinalActionSnapshotDto([Canonical("WL", "Watchlist", "Awaiting Data", "AwaitingData")],
            [Canonical("HELD", "Portfolio", "Awaiting Data", "AwaitingData", lastValid: "Add to Trade", lastEval: DateTime.UtcNow.AddDays(-2))], DateTime.UtcNow);
        var (_, svc2) = await SetUpAsync(Holding("HELD", "Swing", "Add to Trade", DateTime.UtcNow.AddMinutes(-5)), "WL", failed, LegacyBearish("HELD"), LegacyBullish("WL"));
        var rows2 = (await svc2.GetActionsAsync(User)).ToDictionary(r => r.Symbol);
        Assert.All(rows2.Values, r => { Assert.Equal("Awaiting Data", r.ActionLabel); Assert.False(r.IsActionable); });
        Assert.Equal("Add to Trade", rows2["HELD"].LastValidAction);
    }

    [Fact]
    public async Task ReadingActionRows_NeverScans_AndNeverChangesPersistedRecommendations()
    {
        var published = DateTime.UtcNow.AddHours(-3);
        var (db, svc) = await SetUpAsync(Holding("HELD", "Swing", "Add to Trade", published), "WL", null, LegacyBullish("WL"));
        var before = (await db.PortfolioSnapshots.AsNoTracking().SingleAsync()).SnapshotJson;
        await svc.GetActionsAsync(User);
        await svc.GetActionsAsync(User);
        Assert.Equal(before, (await db.PortfolioSnapshots.AsNoTracking().SingleAsync()).SnapshotJson);   // no timestamp renewed
    }

    [Fact]
    public async Task DashboardCounts_Use_TheEffectiveState_AwaitingDataIsNotActionRequired()
    {
        var (_, svc) = await SetUpAsync(Holding("HELD", "Swing"), "WL", null, LegacyBearish("HELD"), LegacyBullish("WL"));
        var rows = await svc.GetActionsAsync(User);
        Assert.Equal(0, rows.Count(r => r.IsActionable));
        Assert.Equal(0, rows.Count(r => r.ActionPriority == "REQUIRED"));
    }
}
