using FluentAssertions;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using Xunit;

namespace PortfolioManager.Tests;

public class PreMarketTests
{
    [Fact]
    public void Rsi_NeedsEnoughData() =>
        PreMarketMath.Rsi([1m, 2m, 3m], 14).Should().BeNull();

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
    public void Renderer_ListsEverySymbol_AndEscapesHtml()
    {
        StockAuditDto Row(string s, string kind) => new(s, "<b>Co</b>", kind, "", 1, 1, 1, null, null, 50, 50, 0,
            "COVERAGE_INCOMPLETE", [], "HOLD", "LOW", "ok", []);
        var report = new PreMarketReportDto(new DateOnly(2026, 10, 20), DateTime.UtcNow, false, true, true, "n/a",
            [Row("AAA", "HOLDING")], [Row("BBB", "WATCHLIST")], ["note"]);
        var html = PreMarketReportRenderer.Render(report);
        html.Should().Contain("AAA").And.Contain("BBB").And.NotContain("<b>Co</b>");
        PreMarketReportRenderer.Subject(report).Should().Contain("2026-10-20").And.Contain("Complete");
    }
}
