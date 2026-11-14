using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using PortfolioManager.Api.Services.FinalAction;

namespace PortfolioManager.Tests;

/// <summary>
/// PM-024 Stage 1: market-data integrity. A bar is complete or it is not used: a session whose close is still null must never
/// lend its open/high/low/volume to the previous session's close. Synthetic provider payloads only.
/// </summary>
public sealed class Pm024Stage1MarketDataTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // ── Synthetic Yahoo chart payload ────────────────────────────────────────

    private sealed record Bar(DateOnly Date, decimal? O, decimal? H, decimal? L, decimal? C, long? V);

    private static List<Bar> Bars(int count, DateOnly last)
    {
        var dates = new List<DateOnly>();
        for (var d = last; dates.Count < count; d = d.AddDays(-1))
            if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) dates.Insert(0, d);
        return dates.Select((d, i) =>
        {
            var close = 100m + (i % 7) * 0.2m;
            return new Bar(d, close - 0.1m, close + 0.8m, close - 0.8m, close, 1_000_000 + i * 100);
        }).ToList();
    }

    private static string ChartJson(IReadOnlyList<Bar> bars) => JsonSerializer.Serialize(new
    {
        chart = new
        {
            result = new[]
            {
                new
                {
                    meta = new { regularMarketPrice = 105m, regularMarketTime = 1_793_826_000L },
                    timestamp = bars.Select(b => new DateTimeOffset(b.Date.ToDateTime(new TimeOnly(14, 30)), TimeSpan.Zero).ToUnixTimeSeconds()).ToArray(),
                    indicators = new { quote = new[] { new
                    {
                        open = bars.Select(b => b.O).ToArray(), high = bars.Select(b => b.H).ToArray(), low = bars.Select(b => b.L).ToArray(),
                        close = bars.Select(b => b.C).ToArray(), volume = bars.Select(b => b.V).ToArray(),
                    } } },
                },
            },
        },
    }, Json);

    private sealed class StaticHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(body) });
    }

    private static Task<RsiScanResult?> ScanAsync(IReadOnlyList<Bar> bars)
    {
        var http = new HttpClient(new StaticHandler(ChartJson(bars))) { BaseAddress = new Uri("https://query1.finance.yahoo.com/") };
        var scanner = new RsiScannerService(http, NullLogger<RsiScannerService>.Instance, null!, new ScannerRuntimeConfig(), null!,
            new ChannelAnalysisService(), null!, null!);
        return scanner.AnalyzeSymbolAsync("SYN", 30m, 75m, "Enhanced", CancellationToken.None);
    }

    private static readonly DateOnly Wed = new(2026, 11, 4);

    /// <summary>The Nov-4 bar exists with open/high/low/volume (a gap up) but no close yet — BIDU/AMZN on 2026-11-04.</summary>
    private static List<Bar> OpenSession()
    {
        var bars = Bars(260, Wed);
        bars[^1] = bars[^1] with { O = 104m, H = 106m, L = 103.5m, C = null, V = 3_000_000 };
        return bars;
    }

    // ── Scanner: whole bars only ─────────────────────────────────────────────

    [Fact]
    public async Task SessionWithoutAClose_IsIgnored_AndTheLastCompleteSessionIsUsedConsistently()
    {
        var bars = OpenSession();
        var prior = bars[^2];
        var s = await ScanAsync(bars);

        Assert.NotNull(s);
        Assert.Equal(prior.Date, s!.TradingDate);                       // freshness is judged on the bar actually used
        Assert.Equal(prior.C, s.CurrentPrice);
        Assert.Equal(prior.O, s.OpenPrice);
        Assert.Equal(prior.H, s.DayHigh);
        Assert.Equal(prior.L, s.DayLow);
        Assert.Equal(bars[^3].C, s.PreviousClose);
        Assert.Equal((decimal)prior.V!.Value, s.Volume);
        Assert.InRange(s.CurrentPrice, s.DayLow, s.DayHigh);            // one session's close sits inside that session's range
    }

    [Fact]
    public async Task SessionWithoutAClose_NeverCreatesAGapFromAnotherSessionsRange()
    {
        var s = await ScanAsync(OpenSession());
        var gap = FinalActionEngine.CalcGapStatus((double)s!.OpenPrice, (double)s.PreviousClose, (double)s.CurrentPrice, (double)s.DayHigh, (double)s.DayLow);
        Assert.Equal("No Gap", gap.Status);                            // the 104-vs-100 "gap" belonged to the unfinished session
    }

    [Fact]
    public async Task CompleteLastSession_IsUsedAsIs()
    {
        var bars = Bars(260, Wed);
        var s = await ScanAsync(bars);
        Assert.Equal(Wed, s!.TradingDate);
        Assert.Equal(bars[^1].C, s.CurrentPrice);
        Assert.Equal(bars[^1].H, s.DayHigh);
        Assert.Equal((decimal)bars[^1].V!.Value, s.Volume);
    }

    [Fact]
    public async Task IncompleteBarInTheMiddle_IsDroppedWhole_SoLaterBarsStayAligned()
    {
        var bars = Bars(260, Wed);
        bars[100] = bars[100] with { C = null };                        // O/H/L/V present, close missing
        bars[101] = bars[101] with { O = null, H = null, L = null, C = null, V = null };
        var s = await ScanAsync(bars);
        Assert.Equal(Wed, s!.TradingDate);
        Assert.Equal(bars[^1].O, s.OpenPrice);
        Assert.Equal(bars[^1].C, s.CurrentPrice);
        Assert.Equal(bars[^2].C, s.PreviousClose);
        Assert.Equal((decimal)bars[^1].V!.Value, s.Volume);
    }

    [Fact]
    public async Task InconsistentLastSession_IsRejected_NotEvaluated()
    {
        var bars = Bars(260, Wed);
        bars[^1] = bars[^1] with { C = 120m };                          // close far above the same bar's high
        Assert.Null(await ScanAsync(bars));
    }

    [Theory]
    [InlineData(100, 101, 99, 100.5, true)]
    [InlineData(100, 101, 99, 101.05, true)]       // rounding tolerance
    [InlineData(100, 101, 99, 103, false)]
    [InlineData(100, 101, 99, 97, false)]
    [InlineData(104, 101, 99, 100, false)]         // open outside the range
    [InlineData(100, 99, 101, 100, false)]         // high below low
    public void SessionConsistency(double open, double high, double low, double close, bool expected) =>
        Assert.Equal(expected, RsiScannerService.IsSessionConsistent((decimal)open, (decimal)high, (decimal)low, (decimal)close));

    private static YahooChartResult Intraday(DateOnly day)
    {
        var start = new DateTimeOffset(day.ToDateTime(new TimeOnly(14, 30)), TimeSpan.Zero);
        var quote = new YahooQuoteData();
        var chart = new YahooChartResult { Meta = new YahooMeta { Symbol = "SYN" }, Timestamp = [], Indicators = new YahooIndicators { Quote = [quote] } };
        for (var i = 0; i < 390; i++)
        {
            chart.Timestamp.Add(start.AddMinutes(i).ToUnixTimeSeconds());
            quote.Open.Add(i == 0 ? 102m : 100m);
            quote.High.Add(i == 0 ? 102m : 101m);
            quote.Low.Add(99m);
            quote.Close.Add(100m);
        }
        return chart;
    }

    [Fact]
    public void IntradayRecovery_UsesFridaySessionOnMonday_OnlyAfterClose()
    {
        var friday = new DateOnly(2026, 11, 13);
        var data = Intraday(friday);
        var monday = new DateTime(2026, 11, 16, 15, 0, 0, DateTimeKind.Utc);
        var recovered = RsiScannerService.ReconstructSessionRange(data, "SYN", friday, 102m, 101m, 99m, 100m, monday);
        Assert.Equal((102m, 99m), recovered);
        Assert.Null(RsiScannerService.ReconstructSessionRange(data, "SYN", friday, 102m, 101m, 99m, 100m,
            new DateTime(2026, 11, 13, 20, 59, 0, DateTimeKind.Utc)));
        Assert.Equal(friday, TradingCalendar.LastCompletedSession(monday, MarketExchange.Nyse));
        var mondaySession = new DateOnly(2026, 11, 16);
        Assert.NotNull(RsiScannerService.ReconstructSessionRange(Intraday(mondaySession), "SYN", mondaySession, 102m, 101m, 99m, 100m,
            new DateTime(2026, 11, 16, 21, 0, 0, DateTimeKind.Utc)));
        Assert.Equal(mondaySession, TradingCalendar.LastCompletedSession(
            new DateTime(2026, 11, 16, 23, 0, 0, DateTimeKind.Utc), MarketExchange.Nyse));
    }

    [Fact]
    public void IntradayRecovery_RejectsMissingOrMismatchedEvidence()
    {
        var friday = new DateOnly(2026, 11, 13);
        var now = new DateTime(2026, 11, 16, 15, 0, 0, DateTimeKind.Utc);
        var data = Intraday(friday);
        Assert.Null(RsiScannerService.ReconstructSessionRange(data, "SYN", friday, 103m, 101m, 99m, 100m, now));
        Assert.Null(RsiScannerService.ReconstructSessionRange(data, "SYN", friday, 102m, 101m, 99m, 103m, now));
        Assert.Null(RsiScannerService.ReconstructSessionRange(data, "SYN", friday.AddDays(3), 102m, 101m, 99m, 100m, now));
        Assert.Null(RsiScannerService.ReconstructSessionRange(data, "OTHER", friday, 102m, 101m, 99m, 100m, now));
        data.Timestamp!.RemoveAt(0);
        Assert.Null(RsiScannerService.ReconstructSessionRange(data, "SYN", friday, 102m, 101m, 99m, 100m, now));
    }

    [Theory]
    [InlineData(109.82, 111.58, 110.105, 110.34)]
    [InlineData(611.93, 611.1948, 590.2601, 600.20)]
    [InlineData(236.17, 235.855, 219.3545, 226.61)]
    public void IntradayRecovery_CoversObservedOpenOutsideRange(double o, double h, double l, double c)
    {
        var open = (decimal)o;
        var high = (decimal)h;
        var low = (decimal)l;
        var close = (decimal)c;
        var friday = new DateOnly(2026, 11, 13);
        var data = Intraday(friday);
        var bars = data.Indicators!.Quote![0];
        for (var i = 0; i < 390; i++)
        {
            bars.Open[i] = i == 0 ? open : close;
            bars.High[i] = Math.Max(high, open);
            bars.Low[i] = Math.Min(low, open);
            bars.Close[i] = close;
        }
        var recovered = RsiScannerService.ReconstructSessionRange(data, "SYN", friday, open, high, low, close,
            new DateTime(2026, 11, 16, 15, 0, 0, DateTimeKind.Utc));
        Assert.Equal((Math.Max(high, open), Math.Min(low, open)), recovered);
    }

    [Fact]
    public void IntradayRecovery_RejectsGapsAndInvalidCandles()
    {
        var friday = new DateOnly(2026, 11, 13);
        var now = new DateTime(2026, 11, 16, 15, 0, 0, DateTimeKind.Utc);
        var gap = Intraday(friday);
        for (var i = 200; i < 203; i++)
            gap.Indicators!.Quote![0].Close[i] = null;
        Assert.Null(RsiScannerService.ReconstructSessionRange(gap, "SYN", friday, 102m, 101m, 99m, 100m, now));
        var invalid = Intraday(friday);
        invalid.Indicators!.Quote![0].High[100] = 90m;
        Assert.Null(RsiScannerService.ReconstructSessionRange(invalid, "SYN", friday, 102m, 101m, 99m, 100m, now));
        var extreme = Intraday(friday);
        extreme.Indicators!.Quote![0].High[100] = 150m;
        Assert.Null(RsiScannerService.ReconstructSessionRange(extreme, "SYN", friday, 102m, 101m, 99m, 100m, now));
        var singleMissing = Intraday(friday);
        singleMissing.Timestamp![100] += 60;
        Assert.Null(RsiScannerService.ReconstructSessionRange(singleMissing, "SYN", friday, 102m, 101m, 99m, 100m, now));
    }

    private sealed class CdrResolver : ISecurityAnalysisResolver
    {
        public Task<ResolvedSecurityAnalysis> ResolveAsync(string tradingTicker, string? userId, CancellationToken ct = default) =>
            Task.FromResult(new ResolvedSecurityAnalysis(tradingTicker, "SYN", "US", "USD", true, UnderlyingResolutionStatus.Resolved, SecurityAnalysisMappingSource.USER));
        public Task<bool> ValidateUnderlyingTickerAsync(string underlyingTicker, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ResolvedSecurityAnalysis> SaveUserMappingAsync(string tradingTicker, string underlyingTicker, string userId, bool useUnderlyingForAnalysis, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> RemoveUserMappingAsync(string tradingTicker, string userId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class DailyAndIntradayHandler(string daily, string intraday) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(request.RequestUri!.Query.Contains("interval=1m", StringComparison.Ordinal) ? intraday : daily),
            });
    }

    [Fact]
    public async Task Scanner_UsesVerifiedIntradayRange_WithoutChangingDailyOpenOrClose()
    {
        var friday = new DateOnly(2020, 11, 6);
        var bars = Bars(260, friday);
        bars[^1] = bars[^1] with { O = 102m, H = 101m, L = 99m, C = 100m };
        var intraday = JsonSerializer.Serialize(new { chart = new { result = new[] { Intraday(friday) } } }, Json);
        var http = new HttpClient(new DailyAndIntradayHandler(ChartJson(bars), intraday)) { BaseAddress = new Uri("https://query1.finance.yahoo.com/") };
        var scanner = new RsiScannerService(http, NullLogger<RsiScannerService>.Instance, null!, new ScannerRuntimeConfig(), null!,
            new ChannelAnalysisService(), null!, null!);
        var scan = await scanner.AnalyzeSymbolAsync("SYN", 30m, 75m, "Enhanced", CancellationToken.None);
        Assert.NotNull(scan);
        Assert.True(scan.IntradayRangeReconstructed);
        Assert.Equal(102m, scan.OpenPrice);
        Assert.Equal(102m, scan.DayHigh);
        Assert.Equal(99m, scan.DayLow);
        Assert.Equal(100m, scan.CurrentPrice);
        Assert.Contains("reconstructed from same-provider intraday bars", scan.TriggerDetails);
    }

    [Fact]
    public async Task Scanner_MapsRejectedUnderlyingEvidenceToCdrTicker()
    {
        var friday = new DateOnly(2020, 11, 6);
        var bars = Bars(260, friday);
        bars[^1] = bars[^1] with { O = 102m, H = 101m, L = 99m, C = 100m };
        var incomplete = Intraday(friday);
        incomplete.Timestamp!.RemoveAt(0);
        var intraday = JsonSerializer.Serialize(new { chart = new { result = new[] { incomplete } } }, Json);
        var http = new HttpClient(new DailyAndIntradayHandler(ChartJson(bars), intraday)) { BaseAddress = new Uri("https://query1.finance.yahoo.com/") };
        var scanner = new RsiScannerService(http, NullLogger<RsiScannerService>.Instance, null!, new ScannerRuntimeConfig(), null!,
            new ChannelAnalysisService(), null!, new CdrResolver());
        var scans = await scanner.AnalyzeSymbolsAsync(["SYN.TO"], userId: "u");
        Assert.Empty(scans);
        Assert.Equal(ScanRejection.InconsistentSession, scanner.LastRejections["SYN.TO"]);
        Assert.Equal("SYN", scanner.LastRejectedBars["SYN.TO"].AnalysisTicker);
        Assert.Equal(friday, scanner.LastRejectedBars["SYN.TO"].TradingDate);
    }

    [Fact]
    public async Task Scanner_PropagatesCancellationDuringRecovery()
    {
        var bars = Bars(260, new DateOnly(2020, 11, 6));
        bars[^1] = bars[^1] with { O = 102m, H = 101m, L = 99m, C = 100m };
        var http = new HttpClient(new StaticHandler(ChartJson(bars))) { BaseAddress = new Uri("https://query1.finance.yahoo.com/") };
        var scanner = new RsiScannerService(http, NullLogger<RsiScannerService>.Instance, null!, new ScannerRuntimeConfig(), null!,
            new ChannelAnalysisService(), null!, null!);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scanner.AnalyzeSymbolAsync("SYN", 30m, 75m, "Enhanced", cancellation.Token));
    }

    // ── Freshness is judged on the bar used: pre-close, 4:30 PM ET EOD, grace, after-hours ──

    private static DateTime Et(int hour, int minute) => new DateTime(2026, 11, 4, 0, 0, 0, DateTimeKind.Utc).AddHours(hour + 5).AddMinutes(minute);   // EST, UTC-5

    private static EvidenceAssessment Assess(DateOnly bar, DateTime utc) =>
        FinalActionStatusResolver.AssessEvidence(new EvidenceInput(bar, null, 100m, false, null), MarketExchange.Nyse, utc);

    [Fact]
    public void PreClose_PriorSessionBar_IsFresh_AndClaimsOnlyThatSession()
    {
        var a = Assess(Wed.AddDays(-1), Et(10, 0));
        Assert.Equal(EvidenceHealth.Fresh, a.Health);
        Assert.Equal(Wed.AddDays(-1), a.DataAsOf);
    }

    [Fact]
    public void EodAt430pm_CompleteTodayBar_IsFresh()
    {
        var a = Assess(Wed, Et(16, 30));
        Assert.Equal(EvidenceHealth.Fresh, a.Health);
        Assert.Equal(Wed, a.DataAsOf);
    }

    [Fact]
    public void EodAt430pm_WithTheUnfinishedSessionIgnored_UsesYesterdayAndSaysSo()
    {
        var a = Assess(Wed.AddDays(-1), Et(16, 30));                  // inside the 2h evidence grace
        Assert.Equal(EvidenceHealth.Fresh, a.Health);
        Assert.Equal(Wed.AddDays(-1), a.DataAsOf);                    // never presented as Nov 4 evidence
        Assert.Equal(Wed.AddDays(-1), a.ExpectedSession);
    }

    [Theory]
    [InlineData(17, 59, EvidenceHealth.Fresh)]
    [InlineData(18, 0, EvidenceHealth.Stale)]
    [InlineData(21, 0, EvidenceHealth.Stale)]
    public void PriorSessionBar_AcrossTheGraceBoundaryAndAfterHours(int hour, int minute, EvidenceHealth expected) =>
        Assert.Equal(expected, Assess(Wed.AddDays(-1), Et(hour, minute)).Health);

    [Fact]
    public void AfterHours_CompleteTodayBar_IsFresh() => Assert.Equal(EvidenceHealth.Fresh, Assess(Wed, Et(21, 0)).Health);

    [Fact]
    public async Task EndToEnd_UnfinishedSession_AfterTheGrace_IsAwaitingData_NotAMixedDecision()
    {
        var s = await ScanAsync(OpenSession());
        Assert.Equal(EvidenceHealth.Stale, Assess(s!.TradingDate, Et(20, 55)).Health);
    }

    // ── Price provenance: provider time, never fetch time ────────────────────

    private static readonly DateOnly Expected = TradingCalendar.LastCompletedSession(DateTime.UtcNow, MarketExchange.Nyse);

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

    private static RsiScanResult FreshScan(string symbol) => new()
    {
        Symbol = symbol, AnalysisTicker = symbol, AnalysisCurrency = "USD", Rsi = 58m, CurrentPrice = 100m, ChangePercent = 0.5m,
        DayHigh = 101m, DayLow = 98m, OpenPrice = 99.5m, PreviousClose = 99.4m, VolumeRatio = 1.6m, RsiSignal = 54m, RsiSignalAvailable = true,
        MacdHistogram = 0.4m, MacdHistDelta = 0.1m, Ema9Price = 99m, Ema10Price = 98.5m, Ema20Price = 96m, Sma20Price = 95m, Sma50Price = 90m,
        Sma200 = 80m, Status = SignalStatus.Neutral, ScanType = ScanType.Neutral, Week52High = 110m, TradingDate = Expected,
    };

    private static async Task<WatchlistRow> WatchWithQuoteAsync(StockQuote quote)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.WatchlistItems.Add(new WatchlistItem { UserId = "u", Symbol = "WL", Role = "Core" });
        var rows = new[] { new WatchlistSummaryDto(new WatchlistItemDto(5, "WL", "", DateTime.UtcNow, "Core", false, null, "Active"), quote) };
        db.WatchlistSnapshots.Add(new WatchlistSnapshot { UserId = "u", SnapshotJson = JsonSerializer.Serialize(rows, Json) });
        await db.SaveChangesAsync();
        var service = new FinalActionService(db, new FakeScanner(FreshScan("WL")),
            new PortfolioSnapshotService(db, NullLogger<PortfolioSnapshotService>.Instance), new MemoryCache(new MemoryCacheOptions()), NullLogger<FinalActionService>.Instance);
        return new WatchlistRow(Assert.Single((await service.ComputeAsync("u")).Watchlist));
    }

    private sealed record WatchlistRow(FinalActionResultDto R);

    private static long Unix(DateTime utc) => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeSeconds();

    [Fact]
    public async Task OldProviderPriceTime_IsStale_EvenWhenTheQuoteWasFetchedJustNow()
    {
        var fetched = DateTime.UtcNow;
        var source = DateTime.UtcNow.AddDays(-9);
        var r = (await WatchWithQuoteAsync(new StockQuote { Symbol = "WL", CurrentPrice = 100m, Timestamp = Unix(fetched), SourcePriceTimestamp = Unix(source) })).R;

        Assert.Equal("Awaiting Data", r.FinalAction);
        Assert.Contains("QUOTE_OLDER_THAN_SCAN", r.ReasonCodes!);
        Assert.Equal(Unix(source), Unix(r.QuoteTimestampUtc!.Value));      // provenance is the provider's price time
        Assert.Equal(Unix(fetched), Unix(r.QuoteFetchedAtUtc!.Value));     // fetch time is preserved, separately
        Assert.NotEqual(r.QuoteTimestampUtc, r.QuoteFetchedAtUtc);
    }

    [Fact]
    public async Task FreshProviderPriceTime_IsVerified_AndFetchTimeStaysSeparate()
    {
        var source = TradingCalendar.LastCompletedSession(DateTime.UtcNow, MarketExchange.Nyse).ToDateTime(new TimeOnly(20, 0));
        var fetched = DateTime.UtcNow;
        var r = (await WatchWithQuoteAsync(new StockQuote { Symbol = "WL", CurrentPrice = 100m, Timestamp = Unix(fetched), SourcePriceTimestamp = Unix(source) })).R;

        Assert.Equal("CurrentValid", r.EvaluationState);
        Assert.Equal("Verified", r.QuoteVerification);
        Assert.Equal(Unix(source), Unix(r.QuoteTimestampUtc!.Value));
        Assert.Equal(Unix(fetched), Unix(r.QuoteFetchedAtUtc!.Value));
    }

    [Fact]
    public async Task FetchTimeAlone_IsNotProvenance_SoQuoteVerificationIsUnavailable()
    {
        var r = (await WatchWithQuoteAsync(new StockQuote { Symbol = "WL", CurrentPrice = 100m, Timestamp = Unix(DateTime.UtcNow), SourcePriceTimestamp = null })).R;

        Assert.Null(r.QuoteTimestampUtc);
        Assert.Equal("Unavailable", r.QuoteVerification);
        Assert.NotNull(r.QuoteFetchedAtUtc);
        Assert.Equal("Fresh", r.EvidenceHealth);                              // by bar date only, and it says so
    }
}
