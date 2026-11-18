using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using PortfolioManager.Api.Services.FinalAction;
using PortfolioManager.Api.Services.Opportunities;

namespace PortfolioManager.Tests;

/// <summary>
/// A Strategic ADD into a Waterfall / Falling Knife (or any active hard negative) needs the same reversal confirmation a new
/// Watchlist entry needs. Oversold RSI, high volume or an intraday recovery alone is not confirmation. The holding itself is never
/// turned into a sell. In-memory databases and synthetic rows only.
/// </summary>
public sealed class StrategicAddConfirmationTests
{
    private const string Wait = "REVERSAL WATCH — Wait for Confirmation";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly DateTime Stamp = new(2026, 10, 20, 21, 0, 0, DateTimeKind.Utc);
    private static DateOnly Expected => TradingCalendar.LastCompletedSession(DateTime.UtcNow, MarketExchange.Nyse);

    private static readonly PriceStructureResult NoStructure = PriceStructureResult.None;
    private static readonly PriceStructureResult Bounce =
        PriceStructureResult.None with { KeyLevelRole = "SUPPORT", KeyLevelState = "BOUNCE_CONFIRMED" };
    private static readonly PriceStructureResult BounceButBroken =
        Bounce with { PrimaryPatternState = "BREAKDOWN", PrimaryPatternType = "RISING_WEDGE" };

    private sealed class FakeScanner(params RsiScanResult[] results) : IRsiScannerService
    {
        public Task<List<RsiScanResult>> AnalyzeSymbolsAsync(IEnumerable<string> symbols, decimal oversoldThreshold = 30m, decimal overboughtThreshold = 75m,
            string logicMode = "Legacy", string? userId = null, CancellationToken ct = default)
        {
            var wanted = symbols.ToHashSet(StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(results.Where(r => wanted.Contains(r.Symbol)).ToList());
        }

        public Task<ScannerResponse> ScanAsync(IEnumerable<string>? extraSymbols = null, decimal oversoldThreshold = 30m, decimal overboughtThreshold = 75m,
            string logicMode = "Legacy", string? userId = null, CancellationToken ct = default) => throw new NotSupportedException();
    }

    /// <summary>RSI 22 below its signal, price under SMA50, high volume and an upper-half close: the NVDA situation.</summary>
    private static RsiScanResult FallingKnife(string symbol, string analysis, bool mapped, PriceStructureResult structure, string currency) => new()
    {
        Symbol = symbol, AnalysisTicker = analysis, AnalysisCurrency = currency, AnalysisMarket = "US", UsesUnderlyingSecurity = mapped,
        Rsi = 22m, RsiSignal = 26m, RsiSignalAvailable = true, CurrentPrice = 80m, ChangePercent = 0.4m, DayHigh = 82m, DayLow = 74m,
        OpenPrice = 79m, PreviousClose = 79.7m, VolumeRatio = 1.4m, MacdHistogram = -0.3m, MacdHistDelta = 0.05m,
        Ema9Price = 79m, Ema10Price = 84m, Ema20Price = 90m, Sma20Price = 92m, Sma50Price = 100m, Sma200 = 70m, Has200Dma = true,
        Status = SignalStatus.Neutral, ScanType = ScanType.Neutral, Week52High = 140m, FibZone = "Deep Pullback",
        TradingDate = Expected, ScannedAt = Stamp, PriceStructure = structure,
    };

    private static PortfolioSummaryDto Lot(string symbol, string role, decimal shares, decimal price) => new(
        new PortfolioItemDto(1, symbol, symbol, shares, price * 0.9m, "Tech", "", false, false, null, Stamp, "OPEN", "TFSA", Stamp.AddDays(-30), null, null,
            role, null, null, null, "Hold", "hold", "INFORMATIONAL", Stamp),
        new StockQuote { Symbol = symbol, CurrentPrice = price, Currency = "CAD" },
        PriceStructureResult.None);

    /// <summary>100 CAD shares at C$25 against C$55,640 of cash: a 4.3 % holding.</summary>
    private static async Task<FinalActionSnapshotDto> EvaluateAsync(
        string holding, string analysis, bool mapped, PriceStructureResult structure, string role = "Strategic", decimal limit = 8m)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var watch = mapped ? analysis : holding;
        db.WatchlistItems.Add(new WatchlistItem { UserId = "u", Symbol = watch, Role = "Strategic" });
        foreach (var r in new[] { "Strategic", "Core" }) db.SinglePositionLimits.Add(new SinglePositionLimit { Role = r, TargetPct = limit });
        foreach (var n in new[] { "MARGIN", "TFSA" }) db.AccountTypes.Add(new AccountType { Name = n, NormalizedName = n });
        db.CashItems.Add(new CashItem { UserId = "u", Amount = 55640m, AccountType = "MARGIN", CashFlowType = "Deposit" });
        db.PortfolioSnapshots.Add(new PortfolioSnapshot { UserId = "u", SnapshotJson = JsonSerializer.Serialize(new List<PortfolioSummaryDto> { Lot(holding, role, 100m, 25m) }, Json) });
        await db.SaveChangesAsync();
        var scans = new List<RsiScanResult> { FallingKnife(holding, analysis, mapped, structure, mapped ? "USD" : "CAD") };
        if (mapped) scans.Add(FallingKnife(analysis, analysis, false, structure, "USD"));
        var service = new FinalActionService(db, new FakeScanner([.. scans]),
            new PortfolioSnapshotService(db, NullLogger<PortfolioSnapshotService>.Instance), new MemoryCache(new MemoryCacheOptions()), NullLogger<FinalActionService>.Instance);
        return await service.ComputeAsync("u", fresh: true);
    }

    [Theory]
    [InlineData("NVDA.TO", "NVDA", true)]
    [InlineData("ABC.TO", "ABC.TO", false)]
    public async Task UnconfirmedReversal_WaitsForConfirmation_ForNormalStocksAndMappedCdrs(string holding, string analysis, bool mapped)
    {
        var snapshot = await EvaluateAsync(holding, analysis, mapped, NoStructure);
        var portfolio = Assert.Single(snapshot.Portfolio);
        var watch = Assert.Single(snapshot.Watchlist);

        Assert.Equal(Wait, portfolio.FinalAction);
        Assert.Equal("Add / Staged Entry", portfolio.OriginalAction);               // what the unchanged role rule produced
        Assert.Equal("Waterfall / Falling Knife", portfolio.TrendSetup);
        Assert.Contains("ADD_BLOCKED_REVERSAL_UNCONFIRMED", portfolio.ReasonCodes!);
        Assert.DoesNotContain("ADD_ELIGIBLE", portfolio.ReasonCodes!);
        Assert.Equal("Advisory", portfolio.Category);                               // never a Trim / Exit
        Assert.Equal("Neutral", portfolio.Direction);
        Assert.False(portfolio.IsActionable);
        Assert.Contains("not a sell or exit", portfolio.Reason!);
        Assert.InRange(portfolio.Ownership!.ExposurePct!.Value, 4.2, 4.4);          // the holding is untouched
        Assert.Equal(analysis, portfolio.AnalysisTicker);
        Assert.Equal(mapped ? "USD" : "CAD", portfolio.AnalysisCurrency);

        Assert.Equal("REVERSAL WATCH", watch.FinalAction);                         // the Watchlist already waited
        Assert.False(watch.IsActionable);
    }

    [Theory]
    [InlineData("NVDA.TO", "NVDA", true)]
    [InlineData("ABC.TO", "ABC.TO", false)]
    public async Task ConfirmedReversal_AllowsTheAdd_WithTheReasonCodesThatExplainIt(string holding, string analysis, bool mapped)
    {
        var snapshot = await EvaluateAsync(holding, analysis, mapped, Bounce);
        var portfolio = Assert.Single(snapshot.Portfolio);

        Assert.Equal("Add / Staged Entry", portfolio.FinalAction);
        Assert.Null(portfolio.OriginalAction);
        Assert.Contains("ADD_ELIGIBLE", portfolio.ReasonCodes!);
        Assert.Contains("ADD_REVERSAL_CONFIRMED", portfolio.ReasonCodes!);
        Assert.DoesNotContain("ADD_BLOCKED_REVERSAL_UNCONFIRMED", portfolio.ReasonCodes!);
        Assert.True(portfolio.IsActionable);
        Assert.Equal("ENTRY CANDIDATE", Assert.Single(snapshot.Watchlist).FinalAction);   // the same evidence reads as an entry on the Watchlist
    }

    [Fact]
    public async Task ActiveHardNegative_IsNotAReversal_EvenWhenALevelBounceIsConfirmed()
    {
        var portfolio = Assert.Single((await EvaluateAsync("NVDA.TO", "NVDA", true, BounceButBroken)).Portfolio);

        Assert.Equal(Wait, portfolio.FinalAction);
        Assert.Contains("ADD_BLOCKED_REVERSAL_UNCONFIRMED", portfolio.ReasonCodes!);
        Assert.Contains("hard structural negative", portfolio.Reason!);
    }

    [Fact]
    public async Task AllocationLimit_KeepsItsOwnOutcome_AndTheReversalReasonIsAddedToIt()
    {
        var unconfirmed = Assert.Single((await EvaluateAsync("NVDA.TO", "NVDA", true, NoStructure, limit: 4m)).Portfolio);
        Assert.Equal("Hold / No Add", unconfirmed.FinalAction);
        Assert.Equal("Add / Staged Entry", unconfirmed.OriginalAction);
        Assert.Contains("ADD_BLOCKED_POSITION_LIMIT", unconfirmed.ReasonCodes!);
        Assert.Contains("ADD_BLOCKED_REVERSAL_UNCONFIRMED", unconfirmed.ReasonCodes!);

        var confirmed = Assert.Single((await EvaluateAsync("NVDA.TO", "NVDA", true, Bounce, limit: 4m)).Portfolio);
        Assert.Equal("Hold / No Add", confirmed.FinalAction);
        Assert.Contains("ADD_BLOCKED_POSITION_LIMIT", confirmed.ReasonCodes!);
        Assert.DoesNotContain("ADD_BLOCKED_REVERSAL_UNCONFIRMED", confirmed.ReasonCodes!);
    }

    [Fact]
    public async Task OtherRoles_KeepTheirExistingAddRule()
    {
        var core = Assert.Single((await EvaluateAsync("ABC.TO", "ABC.TO", false, NoStructure, role: "Core")).Portfolio);

        Assert.Equal("Add to Position", core.FinalAction);
        Assert.DoesNotContain("ADD_BLOCKED_REVERSAL_UNCONFIRMED", core.ReasonCodes ?? []);
    }

    [Fact]
    public async Task TradingOpportunities_ShowsTheSameDecisionAsPortfolio()
    {
        var snapshot = await EvaluateAsync("NVDA.TO", "NVDA", true, NoStructure);
        var portfolio = Assert.Single(snapshot.Portfolio);

        var row = OpportunityService.SummarizeDecisions([portfolio], owned: true);
        Assert.Equal(Wait, row.Label);
    }

    // ── Gate-level cases that do not depend on a scan ────────────────────────

    private static FinalActionDecision Add(string trend, string momentum = "Active Buy Trigger") => new(
        "Add / Staged Entry", trend, "", momentum, "", "", "", 0, 0, "", "ma-confirmed-buy", "", "", null, null);

    private static PortfolioEvidence Evidence(ReversalEvidence? reversal, bool hardNegative = false, string? role = "Strategic") =>
        new(null, null, new DateOnly(2026, 11, 12), DateTime.UnixEpoch, 4.3, 8, false, hardNegative, null)
        { TargetRole = role, Reversal = reversal };

    [Fact]
    public void EquivalentHardNegative_WithoutAFallingKnife_AlsoNeedsConfirmation()
    {
        var o = PortfolioEvidenceGate.Apply(Add("Oversold Reversal Watch"), "Strategic", Evidence(new ReversalEvidence(true, false, false), hardNegative: true));

        Assert.Equal(Wait, o.Decision.FinalAction);
        Assert.Equal(["ADD_BLOCKED_REVERSAL_UNCONFIRMED"], o.ReasonCodes);
    }

    [Fact]
    public void NoAdverseStructure_LeavesAStrategicAddUntouched()
    {
        var o = PortfolioEvidenceGate.Apply(Add("Oversold Reversal Watch"), "Strategic", Evidence(new ReversalEvidence(false, false, false)));

        Assert.Equal("Add / Staged Entry", o.Decision.FinalAction);
        Assert.Empty(o.ReasonCodes);
    }

    [Fact]
    public void MissingReversalEvidence_CannotConfirm()
    {
        var o = PortfolioEvidenceGate.Apply(Add("Waterfall / Falling Knife"), "Strategic", Evidence(null));

        Assert.Equal(Wait, o.Decision.FinalAction);
    }

    [Theory]
    [InlineData("BOUNCE_CONFIRMED", "NONE", false, true)]
    [InlineData("SUPPORT_RECLAIM", "NONE", false, true)]
    [InlineData("NONE", "BREAKOUT", false, true)]
    [InlineData("SUPPORT_TEST", "NONE", false, false)]
    [InlineData("BOUNCE_CONFIRMED", "BREAKDOWN", false, false)]
    [InlineData("BOUNCE_CONFIRMED", "NONE", true, false)]
    public void ReversalConfirmation_ReusesTheWatchlistEntryStructureFacts(string level, string pattern, bool trendDamage, bool confirmed)
    {
        var input = new FinalActionInput
        {
            CurrentPrice = 80, Sma50 = 100, Sma200 = 70, FibZone = trendDamage ? "Trend Damage" : "Deep Pullback",
            PriceStructure = new FinalActionPriceStructure(false, "SUPPORT", level, pattern, "RISING_WEDGE"),
        };

        Assert.Equal(confirmed, FinalActionEngine.ReversalConfirmation(input).Confirmed);
    }

    [Fact]
    public void NewAction_IsAdvisoryAndBlocksFurtherAddsOnTheWatchlist()
    {
        var def = FinalActionCatalog.Describe(Wait);

        Assert.Equal(("Advisory", "Neutral", "Portfolio"), (def.Category, def.Direction, def.Group));
        Assert.True(PortfolioEvidenceGate.BlocksAdd(Wait));
        Assert.Equal(DecisionBucket.None, DecisionEligibility.Classify(Wait, "Portfolio", 10m));
        var entry = Add("Waterfall / Falling Knife") with { FinalAction = "ENTRY CANDIDATE" };
        Assert.Equal(Wait, PortfolioEvidenceGate.BlockOwnedEntryByPortfolioAction(entry, Wait).Decision.FinalAction);
    }
}
