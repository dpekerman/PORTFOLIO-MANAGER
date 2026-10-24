using System.Net;
using FluentAssertions;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using Xunit;

namespace PortfolioManager.Tests;

public class PreMarketTests
{
    private static NewsItemDto News(string id, string headline, string summary = "") =>
        new(id, headline, "Pub", "https://example.com/" + id, DateTime.UtcNow, DateTime.UtcNow, summary, false);

    [Fact]
    public void Rsi_NeedsEnoughData() => PreMarketMath.Rsi([1m, 2m, 3m], 14).Should().BeNull();

    [Fact]
    public void Rsi_AllGains_Is100() =>
        PreMarketMath.Rsi(Enumerable.Range(1, 20).Select(i => (decimal)i).ToList(), 14).Should().Be(100m);

    [Theory]
    [InlineData(true, 75, "REDUCE_CANDIDATE")]
    [InlineData(true, 25, "ADD_CANDIDATE")]
    [InlineData(true, 50, "HOLD")]
    [InlineData(false, 25, "BUY_CANDIDATE")]
    [InlineData(false, 50, "WATCH")]
    public void Decide_MapsRsiToAction(bool held, int rsi, string expected) =>
        PreMarketMath.Decide(held, rsi, 0, true).Action.Should().Be(expected);

    [Fact]
    public void Decide_NoPrice_IsInsufficientData() =>
        PreMarketMath.Decide(true, null, null, false).Action.Should().Be("INSUFFICIENT_DATA");

    [Fact]
    public void Escalate_MaterialNewsOnHolding_OneIsMediumTwoIsHigh()
    {
        var one = PreMarketMath.Escalate(true, "HOLD", "LOW", "x", [News("A1", "ACME cuts guidance after earnings miss")], 0);
        one.Action.Should().Be("INVESTIGATE"); one.Priority.Should().Be("MEDIUM");
        var two = PreMarketMath.Escalate(true, "HOLD", "LOW", "x", [News("A1", "ACME cuts guidance"), News("A2", "ACME faces lawsuit")], 0);
        two.Priority.Should().Be("HIGH");
    }

    [Fact]
    public void Escalate_NoNews_KeepsDecision() =>
        PreMarketMath.Escalate(true, "HOLD", "LOW", "x", [], 0).Should().Be(("HOLD", "LOW", "x"));

    [Fact]
    public void Escalate_EventExposure_RaisesToMedium() =>
        PreMarketMath.Escalate(true, "HOLD", "LOW", "x", [], 2).Priority.Should().Be("MEDIUM");

    [Theory]
    [InlineData("http://example.com/feed")]
    [InlineData("https://localhost/feed")]
    [InlineData("https://user:pw@example.com/feed")]
    [InlineData("https://10.0.0.5/feed")]
    [InlineData("https://192.168.1.10/feed")]
    [InlineData("https://169.254.169.254/latest")]
    [InlineData("https://example.com:8443/feed")]
    [InlineData("file:///c:/windows/win.ini")]
    [InlineData("https://intranet/feed")]
    public void UrlGuard_RejectsUnsafeUrls(string url) => UrlGuard.TryValidate(url, out _, out _).Should().BeFalse();

    [Fact]
    public void UrlGuard_AcceptsPublicHttpsWithSymbolTemplate() =>
        UrlGuard.TryValidate("https://feeds.example.com/rss?s={symbol}", out _, out _).Should().BeTrue();

    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("10.1.2.3", true)]
    [InlineData("172.20.0.1", true)]
    [InlineData("::1", true)]
    [InlineData("fe80::1", true)]
    [InlineData("8.8.8.8", false)]
    public void UrlGuard_IsPrivate(string ip, bool expected) => UrlGuard.IsPrivate(IPAddress.Parse(ip)).Should().Be(expected);

    [Fact]
    public void FeedParser_ParsesRss_StripsHtml_AndRejectsDtd()
    {
        const string rss = "<rss><channel><item><title>Oil jumps</title><link>https://ex.com/a</link><pubDate>Mon, 20 Oct 2026 10:00:00 GMT</pubDate><description>&lt;b&gt;Big&lt;/b&gt; move</description></item>" +
                           "<item><title>Bad</title><link>javascript:alert(1)</link></item></channel></rss>";
        var items = FeedParser.Parse(rss, 10);
        items.Should().ContainSingle();
        items[0].Summary.Should().Be("Big move");
        var xxe = "<?xml version=\"1.0\"?><!DOCTYPE x [<!ENTITY e SYSTEM \"file:///c:/windows/win.ini\">]><rss><channel><item><title>&e;</title><link>https://ex.com</link></item></channel></rss>";
        FluentActions.Invoking(() => FeedParser.Parse(xxe, 10)).Should().Throw<Exception>();
    }

    [Fact]
    public void FeedParser_ToleratesBomAndLeadingWhitespace() =>
        FeedParser.Parse("\uFEFF \n<rss><channel><item><title>T</title><link>https://ex.com/x</link></item></channel></rss>", 5)
            .Should().ContainSingle();

    [Fact]
    public void FeedParser_ParsesAtom() =>
        FeedParser.Parse("<feed xmlns=\"http://www.w3.org/2005/Atom\"><entry><title>T</title><link href=\"https://ex.com/x\"/><updated>2026-10-20T10:00:00Z</updated></entry></feed>", 5)
            .Should().ContainSingle();

    [Fact]
    public void Fingerprint_IgnoresQueryAndTrailingSlash() =>
        FeedParser.Fingerprint("https://ex.com/a/?utm=1").Should().Be(FeedParser.Fingerprint("https://EX.com/a"));

    [Fact]
    public void Classifier_MapsOilEventToEnergyHoldingsOnly()
    {
        var events = GlobalEventClassifier.Classify(
            [News("A1", "Crude oil surges as OPEC cuts output")],
            [("XOM", "Energy"), ("AAPL", "Technology")]);
        events.Should().ContainSingle(e => e.Category == "ENERGY_SUPPLY");
        events[0].AffectedSymbols.Should().Contain("XOM").And.NotContain("AAPL");
        events[0].Counterargument.Should().NotBeEmpty();
    }

    [Fact]
    public void Classifier_NoKeywords_NoEvents() =>
        GlobalEventClassifier.Classify([News("A1", "Local bakery wins award")], [("XOM", "Energy")]).Should().BeEmpty();

    [Fact]
    public void AiValidation_RejectsHallucinatedEvidenceAndUrls()
    {
        var allowed = new HashSet<string> { "A1" };
        var json = """{"bullish":["Strong quarter, see https://evil.com"],"bearish":["Margin pressure"],"risks":["Oil"],"evidenceIds":["A1","A999"]}""";
        var r = AiAnalysis.ParseStock(json, allowed)!;
        r.EvidenceIds.Should().Equal("A1");
        r.Bullish.Should().BeEmpty();
        r.Bearish.Should().Equal("Margin pressure");
    }

    [Fact]
    public void AiValidation_ClaimsWithoutValidEvidenceAreDropped()
    {
        var json = """{"bullish":["Great"],"bearish":["Bad"],"risks":["Risk"],"evidenceIds":["X1"]}""";
        var r = AiAnalysis.ParseStock(json, new HashSet<string> { "A1" })!;
        r.Bullish.Should().BeEmpty(); r.Bearish.Should().BeEmpty(); r.Risks.Should().Equal("Risk");
    }

    [Fact]
    public void AiValidation_InvalidJson_IsNull() =>
        AiAnalysis.ParseStock("not json", new HashSet<string>()).Should().BeNull();

    [Fact]
    public void AiValidation_EventSummaryNeedsEvidence() =>
        AiAnalysis.ParseEvent("""{"summary":"S","portfolioNote":"N","evidenceIds":["Z"]}""", new HashSet<string> { "A1" })!.Value.Summary.Should().BeNull();

    private static PreMarketAgentConfig Cfg() => new() { ScheduleEnabled = true, StartTimeLocal = "07:00", FirstRunDate = new DateOnly(2026, 10, 20) };

    [Fact]
    public void Scheduler_RunsAfterStartTime_TorontoTime_DuringEdt()
    {
        // 07:00 EDT == 11:00 UTC on 2026-10-21.
        PreMarketSchedulerService.ShouldRun(Cfg(), new DateTime(2026, 10, 21, 11, 1, 0, DateTimeKind.Utc), false, true).Should().BeTrue();
        PreMarketSchedulerService.ShouldRun(Cfg(), new DateTime(2026, 10, 21, 10, 59, 0, DateTimeKind.Utc), false, true).Should().BeFalse();
    }

    [Fact]
    public void Scheduler_UsesEstAfterDstEnds()
    {
        // 07:00 EST == 12:00 UTC on 2026-11-10 (DST ended Nov 1).
        PreMarketSchedulerService.ShouldRun(Cfg(), new DateTime(2026, 11, 10, 12, 1, 0, DateTimeKind.Utc), false, true).Should().BeTrue();
        PreMarketSchedulerService.ShouldRun(Cfg(), new DateTime(2026, 11, 10, 11, 30, 0, DateTimeKind.Utc), false, true).Should().BeFalse();
    }

    [Fact]
    public void Scheduler_Guards()
    {
        var t = new DateTime(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);
        PreMarketSchedulerService.ShouldRun(Cfg(), t, true, true).Should().BeFalse("already ran today");
        PreMarketSchedulerService.ShouldRun(Cfg(), t, false, false).Should().BeFalse("not a trading day");
        PreMarketSchedulerService.ShouldRun(new PreMarketAgentConfig { ScheduleEnabled = false }, t, false, true).Should().BeFalse();
        PreMarketSchedulerService.ShouldRun(new PreMarketAgentConfig { ScheduleEnabled = true, FirstRunDate = new DateOnly(2026, 11, 1) }, t, false, true).Should().BeFalse("before first run date");
        PreMarketSchedulerService.ShouldRun(new PreMarketAgentConfig { ScheduleEnabled = true, AsOfDate = new DateOnly(2026, 10, 1) }, t, false, true).Should().BeFalse("test as-of override never auto-runs");
    }

    [Fact]
    public void Deadline_IsNineTwentyFiveToronto()
    {
        var cfg = new PreMarketAgentConfig();
        var d = new DateOnly(2026, 10, 21);
        PreMarketAgentService.DeadlinePassed(cfg, d, new DateTime(2026, 10, 21, 13, 24, 0, DateTimeKind.Utc)).Should().BeFalse();
        PreMarketAgentService.DeadlinePassed(cfg, d, new DateTime(2026, 10, 21, 13, 26, 0, DateTimeKind.Utc)).Should().BeTrue();
    }

    [Theory]
    [InlineData("2026-08-03", false, true)]   // Civic Holiday: TSX closed, US open
    [InlineData("2026-02-16", false, false)]  // Family Day / Presidents' Day
    [InlineData("2026-04-03", false, false)]  // Good Friday
    [InlineData("2026-10-12", false, true)]   // Canadian Thanksgiving
    [InlineData("2026-11-26", true, false)]   // US Thanksgiving
    [InlineData("2026-12-25", false, false)]
    [InlineData("2026-12-28", false, true)]   // Boxing Day observed (TSX only)
    [InlineData("2026-06-19", true, false)]   // Juneteenth
    [InlineData("2026-07-03", true, false)]   // Independence Day observed
    [InlineData("2026-07-01", false, true)]   // Canada Day
    [InlineData("2026-05-18", false, true)]   // Victoria Day
    [InlineData("2026-05-25", true, false)]   // Memorial Day
    [InlineData("2026-10-21", true, true)]    // ordinary Wednesday
    [InlineData("2026-10-24", false, false)]  // Saturday
    public void Calendar_ExchangesAreIndependent(string date, bool tsxOpen, bool usOpen)
    {
        var d = DateOnly.Parse(date);
        // The tuple order in the data is (TSX, US): Thanksgiving-type rows are written accordingly.
        var tsx = TradingCalendar.IsOpen(MarketExchange.Tsx, d);
        var us = TradingCalendar.IsOpen(MarketExchange.Us, d);
        (tsx, us).Should().Be((tsxOpen, usOpen));
    }

    [Theory]
    [InlineData("SHOP.TO", MarketExchange.Tsx)]
    [InlineData("ABC.V", MarketExchange.Tsx)]
    [InlineData("AAPL", MarketExchange.Us)]
    public void Calendar_ExchangeOfSymbol(string sym, MarketExchange expected) =>
        TradingCalendar.ExchangeOf(sym).Should().Be(expected);

    [Fact]
    public void UrlGuard_AllowsQueryTemplate() =>
        UrlGuard.TryValidate("https://news.google.com/rss/search?q={query}&hl=en", out _, out _).Should().BeTrue();

    [Fact]
    public void RenderText_ListsEverySymbolAndPriorities()
    {
        StockAuditDto Row(string s, string kind, string pr) => new(s, "Co", kind, "", 1, 1, 1, null, null, 50, 50, 0, "NEWS_FOUND", [], "INVESTIGATE", pr, "why", []);
        var r = new PreMarketReportDto(new DateOnly(2026, 10, 20), DateTime.UtcNow, false, true, true, "n/a",
            [Row("AAA", "HOLDING", "HIGH")], [Row("BBB", "WATCHLIST", "LOW")], ["note"]);
        var t = PreMarketReportRenderer.RenderText(r);
        t.Should().Contain("AAA [HIGH]").And.Contain("BBB").And.Contain("DATA QUALITY").And.NotContain("<");
    }

    [Fact]
    public void Priority_RankOrdering() =>
        (PreMarketMath.PriorityRank("HIGH") > PreMarketMath.PriorityRank("MEDIUM") && PreMarketMath.PriorityRank("LOW") > PreMarketMath.PriorityRank("NONE")).Should().BeTrue();

    [Fact]
    public void Evaluation_UsesOnlyBarsFromAsOfDate_AndNullUntilEnoughSessions()
    {
        var asOf = new DateOnly(2026, 10, 20);
        MarketDailyClose C(int d, decimal p) => new(asOf.AddDays(d), p);
        var closes = new List<MarketDailyClose> { C(-1, 999m), C(0, 110m), C(1, 121m) };
        PreMarketEvaluationService.ReturnAfter(100m, asOf, closes, 1).Should().Be(10m);
        PreMarketEvaluationService.ReturnAfter(100m, asOf, closes, 2).Should().Be(21m);
        PreMarketEvaluationService.ReturnAfter(100m, asOf, closes, 5).Should().BeNull();
        PreMarketEvaluationService.ReturnAfter(0m, asOf, closes, 1).Should().BeNull();
    }

    [Fact]
    public void Renderer_ListsEverySymbol_EscapesHtml_AndLabelsMock()
    {
        StockAuditDto Row(string s, string kind) => new(s, "<b>Co</b>", kind, "", 1, 1, 1, null, null, 50, 50, 0,
            "NEWS_FOUND", [new NewsItemDto("MOCK-1", "Headline", "Mock", "https://example.com/m", DateTime.UtcNow, DateTime.UtcNow, "", true)],
            "HOLD", "HIGH", "ok", []);
        var report = new PreMarketReportDto(new DateOnly(2026, 10, 20), DateTime.UtcNow, false, true, true, "n/a",
            [Row("AAA", "HOLDING")], [Row("BBB", "WATCHLIST")], ["note"]);
        var html = PreMarketReportRenderer.Render(report);
        html.Should().Contain("AAA").And.Contain("BBB").And.Contain("MOCK").And.NotContain("<b>Co</b>");
        PreMarketReportRenderer.Subject(report).Should().Contain("2026-10-20").And.Contain("Complete");
    }
}
