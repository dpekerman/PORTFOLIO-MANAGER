using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using PortfolioManager.Api.Services.FinalAction;

namespace PortfolioManager.Tests;

/// <summary>
/// PM-024 release-blocker review: owned Watchlist ADD versus the Portfolio's own decision, OptionService bar alignment,
/// failure-versus-inconsistency classification, cache currency, advisory wording, and independent fixed-expectation
/// fixtures for the five UAT tickers. Expectations below are literals, not derived from the golden corpus.
/// </summary>
public sealed class Pm024ReleaseBlockerTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly DateTime Stamp = new(2026, 10, 20, 21, 0, 0, DateTimeKind.Utc);
    private static DateOnly Expected => TradingCalendar.LastCompletedSession(DateTime.UtcNow, MarketExchange.Nyse);

    private sealed class FakeScanner(IReadOnlyDictionary<string, string>? rejections, params RsiScanResult[] results) : IRsiScannerService
    {
        public IReadOnlyDictionary<string, string> LastRejections { get; } = rejections ?? new Dictionary<string, string>();

        public Task<List<RsiScanResult>> AnalyzeSymbolsAsync(IEnumerable<string> symbols, decimal oversoldThreshold = 30m, decimal overboughtThreshold = 75m,
            string logicMode = "Legacy", string? userId = null, CancellationToken ct = default)
        {
            var wanted = symbols.ToHashSet(StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(results.Where(r => wanted.Contains(r.Symbol)).ToList());
        }

        public Task<ScannerResponse> ScanAsync(IEnumerable<string>? extraSymbols = null, decimal oversoldThreshold = 30m, decimal overboughtThreshold = 75m,
            string logicMode = "Legacy", string? userId = null, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private static RsiScanResult EntryScan(string symbol) => new()
    {
        Symbol = symbol, AnalysisTicker = symbol, AnalysisCurrency = "CAD", Rsi = 57.5m, CurrentPrice = 100m, ChangePercent = 0.5m, DayHigh = 101m, DayLow = 98m,
        OpenPrice = 99.5m, PreviousClose = 99.5m, VolumeRatio = 1.8m, RsiSignal = 53m, RsiSignalAvailable = true, MacdHistogram = 0.4m, MacdHistDelta = 0.1m,
        Ema9Price = 99m, Ema10Price = 98.5m, Ema20Price = 97m, Sma20Price = 96m, Sma50Price = 94m, Sma200 = 95m, Status = SignalStatus.Neutral,
        ScanType = ScanType.Neutral, Week52High = 101m, TradingDate = Expected,
        PriceStructure = PriceStructureResult.None with { KeyLevelRole = "SUPPORT", KeyLevelState = "SUPPORT_RECLAIM" },
    };

    private static PortfolioSummaryDto Lot(int id, string symbol, string role, decimal shares, decimal cost) => new(
        new PortfolioItemDto(id, symbol, symbol, shares, cost, "Tech", "", false, false, null, Stamp, "OPEN", "TFSA", Stamp.AddDays(-30), null, null,
            role, null, null, null, "Hold", "hold", "INFORMATIONAL", Stamp),
        new StockQuote { Symbol = symbol, CurrentPrice = 100m }, PriceStructureResult.None);

    private static async Task<FinalActionSnapshotDto> ComputeAsync(IEnumerable<PortfolioSummaryDto> lots, IEnumerable<string> watch,
        IReadOnlyDictionary<string, string>? rejections = null, params RsiScanResult[] scans)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        foreach (var w in watch) db.WatchlistItems.Add(new WatchlistItem { UserId = "u", Symbol = w, Role = "Strategic" });
        db.AccountTypes.Add(new AccountType { Name = "TFSA", NormalizedName = "TFSA" });
        db.PortfolioSnapshots.Add(new PortfolioSnapshot { UserId = "u", SnapshotJson = JsonSerializer.Serialize(lots.ToList(), Json) });
        await db.SaveChangesAsync();
        var service = new FinalActionService(db, new FakeScanner(rejections, scans), new PortfolioSnapshotService(db, NullLogger<PortfolioSnapshotService>.Instance),
            new MemoryCache(new MemoryCacheOptions()), NullLogger<FinalActionService>.Instance);
        return await service.ComputeAsync("u", fresh: true);
    }

    // ── 1. Owned Watchlist entry versus the Portfolio's own decision ─────────

    [Fact]
    public async Task OwnedEntry_IsBlocked_WhenThePortfolioDecisionForTheLotForbidsAdding_AndSaysWhy()
    {
        var snap = await ComputeAsync([Lot(1, "OWN.TO", "Strategic", 100m, 80m)], ["OWN.TO"], null, EntryScan("OWN.TO"));
        var lot = Assert.Single(snap.Portfolio);
        var wl = Assert.Single(snap.Watchlist);

        Assert.True(PortfolioEvidenceGate.BlocksAdd(lot.FinalAction), $"portfolio action was '{lot.FinalAction}'");
        Assert.Equal("Hold / No Add", wl.FinalAction);
        Assert.Equal("STARTER ENTRY", wl.OriginalAction);
        Assert.Contains("ADD_BLOCKED_PORTFOLIO_ACTION", wl.ReasonCodes!);
        Assert.Contains(lot.FinalAction, wl.Reason!);
        Assert.False(wl.IsActionable);
    }

    [Fact]
    public async Task OwnedEntry_Stands_WhenThePortfolioDecisionPermitsAnAdd()
    {
        var snap = await ComputeAsync([Lot(1, "OWN.TO", "Core", 100m, 92m)], ["OWN.TO"], null, EntryScan("OWN.TO"));
        Assert.Equal("Hold / Add If Underweight", Assert.Single(snap.Portfolio).FinalAction);
        var wl = Assert.Single(snap.Watchlist);
        Assert.Equal("STARTER ENTRY", wl.FinalAction);
        Assert.DoesNotContain("ADD_BLOCKED_PORTFOLIO_ACTION", wl.ReasonCodes!);
    }

    [Fact]
    public async Task PortfolioRows_AreUnchangedByTheSharedLotEvaluation()
    {
        var snap = await ComputeAsync([Lot(1, "OWN.TO", "Strategic", 100m, 80m)], [], null, EntryScan("OWN.TO"));
        var lot = Assert.Single(snap.Portfolio);
        Assert.Equal("CurrentValid", lot.EvaluationState);
        Assert.Equal("Hold / Profit Watch — No Add", lot.FinalAction);    // +25% gain near the high, no independent trigger: advisory
    }

    [Theory]
    [InlineData("Hold / No Add", true)]
    [InlineData("Hold / Profit Watch — No Add", true)]
    [InlineData("Hold / Trail Stop — No Add", true)]
    [InlineData("Hold / No Chase", true)]
    [InlineData("Hold / Technical Caution", true)]
    [InlineData("Hold / Risk Review", true)]
    [InlineData("Hold / Review", true)]
    [InlineData("Review Stop Breach", true)]
    [InlineData("Add / Needs Review", true)]
    [InlineData("Trim / Review", true)]
    [InlineData("Exit / Cut Loss", true)]
    [InlineData("Hold / Add If Underweight", false)]
    [InlineData("Hold / Watch for Add", false)]
    [InlineData("Hold / Add on Pullback", false)]
    [InlineData("Add to Position", false)]
    [InlineData("Hold / Trail Stop", false)]
    [InlineData("Hold", false)]
    public void BlocksAdd_FollowsThePortfolioCatalogDecision(string action, bool blocks) =>
        Assert.Equal(blocks, PortfolioEvidenceGate.BlocksAdd(action));

    // ── 2. OptionService bar alignment ───────────────────────────────────────

    private sealed class StaticHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(body) });
    }

    private sealed record Bar(decimal? H, decimal? L, decimal? C);

    private static List<Bar> OptionBars(int n)
    {
        var bars = new List<Bar>();
        for (var i = 0; i < n; i++)
        {
            var c = 50m + (i % 9) * 0.4m + i * 0.05m;
            bars.Add(new Bar(c + 1m + (i % 3) * 0.1m, c - 1m - (i % 4) * 0.1m, c));
        }
        return bars;
    }

    private static string OptionChart(IReadOnlyList<Bar> bars) => JsonSerializer.Serialize(new
    {
        chart = new { result = new[] { new
        {
            meta = new { regularMarketPrice = 60m },
            timestamp = bars.Select((_, i) => 1_760_000_000L + i * 86_400L).ToArray(),
            indicators = new { quote = new[] { new
            {
                open = bars.Select(b => b.C).ToArray(), high = bars.Select(b => b.H).ToArray(), low = bars.Select(b => b.L).ToArray(),
                close = bars.Select(b => b.C).ToArray(), volume = bars.Select(_ => (long?)1000).ToArray(),
            } } },
        } } },
    }, Json);

    private static async Task<OptionTechnicalDataDto?> OptionTechnicalsAsync(IReadOnlyList<Bar> bars)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var http = new HttpClient(new StaticHandler(OptionChart(bars))) { BaseAddress = new Uri("https://query1.finance.yahoo.com/") };
        var service = new OptionService(db, http, NullLogger<OptionService>.Instance, new MutationClock(), new HttpContextAccessor());
        return await service.GetTechnicalDataAsync("SYN");
    }

    [Fact]
    public async Task OptionTechnicals_IgnoreAnUnfinishedSession_SoNoFieldMixesSessions()
    {
        var complete = OptionBars(80);
        var withOpenSession = complete.Append(new Bar(75m, 72m, null)).ToList();       // today: high/low exist, close not yet

        var a = await OptionTechnicalsAsync(complete);
        var b = await OptionTechnicalsAsync(withOpenSession);

        Assert.NotNull(a);
        Assert.Equal(a, b);                                                            // identical: nothing from the open session leaks in
        Assert.Equal(Math.Round(complete[^1].C!.Value, 4), b!.CurrentPrice);
        Assert.Equal(Math.Round(complete[^2].H!.Value, 4), b.YesterdayHigh);
        Assert.Equal(Math.Round(complete[^2].L!.Value, 4), b.YesterdayLow);
        Assert.Equal(Math.Round(complete[^2].C!.Value, 4), b.PreviousClose);
    }

    [Fact]
    public async Task OptionTechnicals_RejectAnInconsistentLastSession()
    {
        var bars = OptionBars(80);
        bars[^1] = new Bar(61m, 59m, 90m);                                             // close far above the same bar's high
        Assert.Null(await OptionTechnicalsAsync(bars));
    }

    // ── 3. Failure versus inconsistent or incomplete evidence ────────────────

    [Fact]
    public async Task RejectedData_IsClassifiedSeparately_FromATransportFailure()
    {
        var rejections = new Dictionary<string, string> { ["BAD.TO"] = ScanRejection.InconsistentSession, ["THIN.TO"] = ScanRejection.InsufficientHistory };
        var snap = await ComputeAsync([], ["BAD.TO", "THIN.TO", "GONE.TO"], rejections, EntryScan("OK.TO"));
        FinalActionResultDto Row(string s) => snap.Watchlist.Single(w => w.Symbol == s);

        Assert.Equal("Invalid", Row("BAD.TO").EvidenceHealth);
        Assert.Contains("AWAITING_INCONSISTENT_DATA", Row("BAD.TO").ReasonCodes!);
        Assert.Contains("internally inconsistent", Row("BAD.TO").Reason!);

        Assert.Equal("Missing", Row("THIN.TO").EvidenceHealth);
        Assert.Contains("AWAITING_INSUFFICIENT_HISTORY", Row("THIN.TO").ReasonCodes!);

        Assert.Equal("Failed", Row("GONE.TO").EvidenceHealth);                         // no rejection recorded: a refresh failure
        Assert.Contains("AWAITING_REFRESH_FAILED", Row("GONE.TO").ReasonCodes!);

        foreach (var s in new[] { "BAD.TO", "THIN.TO", "GONE.TO" })
        {
            Assert.Equal("Awaiting Data", Row(s).FinalAction);
            Assert.False(Row(s).IsActionable);
            Assert.Null(Row(s).LastValidAction);                                       // a stale action is never presented as current
        }
    }

    [Fact]
    public async Task AwaitingHoldings_KeepTheirPriorActionOnlyAsLabelledHistory_NeverAsTheCurrentAction()
    {
        var holding = Lot(1, "HELD.TO", "Swing", 100m, 80m);
        var stale = EntryScan("HELD.TO");
        stale.TradingDate = Expected.AddDays(-6);
        var snap = await ComputeAsync([holding with { Item = holding.Item with { FinalAction = "Add to Trade" } }], [], null, stale);
        var r = Assert.Single(snap.Portfolio);
        Assert.Equal("Awaiting Data", r.FinalAction);
        Assert.Equal("Add to Trade", r.LastValidAction);
        Assert.Contains("Last valid action: Add to Trade", r.Reason!);
        Assert.False(r.IsActionable);
    }

    // ── Cache currency ───────────────────────────────────────────────────────

    private static FinalActionResultDto Result(string symbol, DateOnly? asOf, string state = "CurrentValid") =>
        new FinalActionResultDto(symbol, "Watchlist", null, "Core", "STARTER ENTRY", "STARTER ENTRY", "Entry", "Buy", "buy", "DEVELOPING",
            "ma", "ts", "ms", "ba", "No Gap", "h", "r", false, 10m, symbol, null) with { EvaluationState = state, DataAsOfDate = asOf };

    private static FinalActionSnapshotDto Snapshot(params FinalActionResultDto[] rows) => new(rows, [], DateTime.UtcNow);

    [Theory]
    [InlineData(15, 0, true)]      // 10:00 ET Nov 4: the Nov 3 session is still the last completed one
    [InlineData(22, 59, true)]     // 17:59 ET: inside the 2h evidence grace
    [InlineData(23, 0, false)]     // 18:00 ET: Nov 4 is now the last completed session; a Nov 3 decision is no longer current
    public void CachedSnapshot_IsOnlyServed_WhileItsDecisionsAreStillOnTheLastCompletedSession(int utcHour, int minute, bool current)
    {
        var now = new DateTime(2026, 11, 4, utcHour, minute, 0, DateTimeKind.Utc);
        Assert.Equal(current, FinalActionService.IsStillCurrent(Snapshot(Result("AMZN", new DateOnly(2026, 11, 3))), now));
    }

    [Fact]
    public void CachedSnapshot_IgnoresAwaitingRows_AndRowsWithoutAnAsOfDate()
    {
        var now = new DateTime(2026, 11, 5, 2, 0, 0, DateTimeKind.Utc);
        Assert.True(FinalActionService.IsStillCurrent(Snapshot(Result("A", new DateOnly(2026, 11, 3), "AwaitingData"), Result("B", null)), now));
        Assert.True(FinalActionService.IsStillCurrent(Snapshot(Result("C", new DateOnly(2026, 11, 4))), now));
    }

    // ── 4. The trailing-stop advisory never implies a broker order ───────────

    [Fact]
    public void StopAdvisory_NeverClaimsAnOrderExistsOrWasPlaced()
    {
        var d = FinalActionEngine.TranslateForPortfolio(
            new FinalActionInput
            {
                Symbol = "NOVO.TO", Rsi = 65.49, RsiSignal = 66, RsiSignalAvailable = true, CurrentPrice = 18.28, OpenPrice = 18.46, PreviousClose = 17.45, DayHigh = 18.69,
                DayLow = 18.24, VolumeRatio = 6.29, MacdHistDelta = 0.0569, Ema9 = 17.33, Ema10 = 17.30, Ema20 = 17.155, Sma20 = 17.4, Sma50 = 17.58, Sma200 = 18.6,
                Status = "Neutral", ScanType = "Neutral",
            }, "Swing", new PortfolioItemContext("TFSA", 7.53, 20, null, null));
        var risk = new StopRiskEvidence("Clear", 16.2m, null, false, false, 18.28m, DateTime.UtcNow, "EOD", "Unknown", "None", [], true, "TFSA", null);
        var o = PortfolioEvidenceGate.Apply(d, "Swing", new PortfolioEvidence(7.53, new DateOnly(2026, 10, 15), new DateOnly(2026, 11, 4), DateTime.UtcNow, null, null, false, false, "Manual", Risk: risk));

        Assert.Equal("Hold / Trail Stop — No Add", o.Decision.FinalAction);
        var text = o.Decision.FinalActionReason + " " + o.Decision.HoverDescription;
        Assert.Contains("not a confirmed broker order", text);
        Assert.Contains("places no orders", text);
        Assert.DoesNotContain("stop is in place", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("order placed", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("protected by", text, StringComparison.OrdinalIgnoreCase);
    }

    // ── 5. Independent fixed-expectation fixtures (literals, not golden-derived) ──
    // Indicator values are rounded from the UAT payloads of 2026-11-03/04. Expected results are written by hand.

    private static FinalActionInput Fixture(string symbol, Func<FinalActionInput, FinalActionInput> f) => f(new FinalActionInput
    {
        Symbol = symbol, Status = "Neutral", ScanType = "Neutral", FibZone = "", FibStatus = "", TrendShift = "Waiting", RsiSignalAvailable = true,
        PriceStructure = new FinalActionPriceStructure(false, "TRANSITION", "NONE", "NONE", "NONE"),
    });

    private static readonly FinalActionInput Bidu = Fixture("BIDU", i => i with
    {
        Rsi = 45.92, RsiSignal = 34.71, CurrentPrice = 80.43, OpenPrice = 79.62, PreviousClose = 76.97, ChangePercent = 4.5, DayHigh = 81.70, DayLow = 79.50, VolumeRatio = 1.42,
        MacdHistDelta = 0.2655, Ema9 = 78.53, Ema10 = 78.64, Ema20 = 80.33, Sma20 = 80.30, Sma50 = 86.50, Sma200 = 113.38, FibZone = "Trend Damage", FibStatus = "Below 78.6",
    });

    private static readonly FinalActionInput Amzn = Fixture("AMZN", i => i with
    {
        Rsi = 59.87, RsiSignal = 48.75, CurrentPrice = 266.03, OpenPrice = 264.815, PreviousClose = 250.69, ChangePercent = 6.12, DayHigh = 271.49, DayLow = 263.85, VolumeRatio = 2.17,
        MacdHistDelta = 1.333, Ema9 = 252.54, Ema10 = 252.49, Ema20 = 252.85, Sma20 = 253.80, Sma50 = 253.89, Sma200 = 243.67, FibZone = "Shallow Pullback", FibStatus = "Reclaimed 61.8",
    });

    private static readonly FinalActionInput Bip = Fixture("BIP-UN.TO", i => i with
    {
        Rsi = 57.55, RsiSignal = 53.79, CurrentPrice = 53.19, OpenPrice = 52.72, PreviousClose = 52.45, ChangePercent = 1.41, DayHigh = 53.45, DayLow = 51.84, VolumeRatio = 1.83,
        MacdHistDelta = 0.0388, Ema9 = 52.43, Ema10 = 52.39, Ema20 = 52.11, Sma20 = 52.26, Sma50 = 51.50, Sma200 = 52.08, FibZone = "Shallow Pullback", FibStatus = "Above 61.8",
        PriceStructure = new FinalActionPriceStructure(false, "SUPPORT", "SUPPORT_RECLAIM", "LOWER_RAIL_APPROACHING", "RISING_CHANNEL"),
    });

    private static readonly FinalActionInput DisTo = Fixture("DIS.TO", i => i with
    {
        Rsi = 74.44, RsiSignal = 63.68, CurrentPrice = 13.20, OpenPrice = 12.89, PreviousClose = 12.68, ChangePercent = 4.10, DayHigh = 13.30, DayLow = 12.89, VolumeRatio = 3.58,
        MacdHistDelta = 0.0391, Ema9 = 12.56, Ema10 = 12.53, Ema20 = 12.33, Sma20 = 12.30, Sma50 = 11.99, Sma200 = 11.61,
    });

    private static readonly FinalActionInput NovoTo = Fixture("NOVO.TO", i => i with
    {
        Rsi = 65.49, RsiSignal = 52.64, CurrentPrice = 18.28, OpenPrice = 18.46, PreviousClose = 17.45, ChangePercent = 4.76, DayHigh = 18.69, DayLow = 18.24, VolumeRatio = 6.29,
        MacdHistDelta = 0.0569, Ema9 = 17.33, Ema10 = 17.30, Ema20 = 17.155, Sma20 = 17.40, Sma50 = 17.58, Sma200 = 18.60, FibZone = "Normal Pullback", FibStatus = "Reclaimed 61.8",
    });

    [Fact]
    public void Fixture_BIDU_ConsistentEvidence_IsADamagedTrend_NotAPullback()
    {
        var d = FinalActionEngine.TranslateForWatchlist(Bidu, "Swing");
        Assert.Equal("Technical Caution", d.TrendSetup);
        Assert.Equal("Neutral", d.MomentumShift);
        Assert.Equal("Gap Up - Weak", d.GapStatus);
        Assert.Equal("WAIT FOR RECLAIM", d.FinalAction);
        Assert.Equal("Damaged trend or MA structure requires a confirmed recovery before entry.", d.FinalActionReason);
    }

    [Fact]
    public void Fixture_AMZN_ConsistentEvidence_WaitsForPullback_BecauseTheGapIsHolding()
    {
        var d = FinalActionEngine.TranslateForWatchlist(Amzn, "Strategic");
        Assert.Equal("Overbought-Weak", d.TrendSetup);
        Assert.Equal("Uptrend", d.MomentumShift);
        Assert.Equal("Gap Up - Weak", d.GapStatus);
        Assert.Equal("WAIT FOR PULLBACK", d.FinalAction);
        Assert.Equal("Price gapped up and the gap is holding; do not chase it for a new entry.", d.FinalActionReason);
    }

    [Fact]
    public void Fixture_BIPUNTO_IsAStarterEntry()
    {
        var d = FinalActionEngine.TranslateForWatchlist(Bip, "Strategic");
        Assert.Equal("Quality Trend Entry", d.TrendSetup);
        Assert.Equal("Uptrend", d.MomentumShift);
        Assert.Equal("No Gap", d.GapStatus);
        Assert.Equal(5, d.WatchlistDiagnostics!.BuyScore);
        Assert.Equal("STARTER ENTRY", d.FinalAction);
    }

    [Fact]
    public void Fixture_DISTO_Swing_IsAnExtendedHold_WithTheStopAdvisory()
    {
        var d = FinalActionEngine.TranslateForPortfolio(DisTo, "Swing", new PortfolioItemContext("TFSA", 13.2, 83, null, null));
        Assert.Equal("Constructive Extended", d.TrendSetup);
        Assert.Equal("Warning — Overbought Run", d.MomentumShift);
        Assert.Equal("Hold / Trail Stop — No Add", d.FinalAction);
        Assert.Equal("WAIT FOR PULLBACK", FinalActionEngine.TranslateForWatchlist(DisTo, "Swing").FinalAction);
    }

    [Fact]
    public void Fixture_NOVOTO_Swing_IsATrailStopHold_UntilARealSellConfirmationAppears()
    {
        var ctx = new PortfolioItemContext("TFSA", 7.53, 20, null, null);
        var d = FinalActionEngine.TranslateForPortfolio(NovoTo, "Swing", ctx);
        Assert.Equal("Confirmed Constructive", d.TrendSetup);
        Assert.Equal("Warning — Overbought Run", d.MomentumShift);
        Assert.Equal("Hold / Trail Stop — No Add", d.FinalAction);

        // The same chart with RSI at/below its signal line is a confirmed sell: the exit stands unless a verified-clear stop manages the profit.
        var confirmed = FinalActionEngine.TranslateForPortfolio(NovoTo with { RsiSignal = 66.0 }, "Swing", ctx);
        Assert.Equal("Active Sell Trigger", confirmed.MomentumShift);
        Assert.Equal("Exit / Take Profit", confirmed.FinalAction);
        var ev = new PortfolioEvidence(7.53, new DateOnly(2026, 10, 15), new DateOnly(2026, 11, 4), DateTime.UtcNow, null, null, false, false, "Manual");
        Assert.Equal("Exit / Take Profit", PortfolioEvidenceGate.Apply(confirmed, "Swing", ev).Decision.FinalAction);
        var structural = ev with { HardStructuralNegative = true, Risk = new StopRiskEvidence("Clear", 16.2m, null, false, false, 18.28m, DateTime.UtcNow, "EOD", "Unknown", "None", [], true, "TFSA", null) };
        Assert.Equal("Exit / Take Profit", PortfolioEvidenceGate.Apply(confirmed, "Swing", structural).Decision.FinalAction);
    }
}
