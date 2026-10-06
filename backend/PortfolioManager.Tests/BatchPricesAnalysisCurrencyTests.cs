using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Api.Controllers;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Tests;

/// <summary>EOD Signals last-price must be on the same basis (analysis security/currency) as the stored signal price.</summary>
public class BatchPricesAnalysisCurrencyTests
{
    [Fact]
    public async Task GetBatchPrices_ReturnsUnderlyingPriceForCdr_AndOwnPriceForOrdinarySymbol()
    {
        var market = new FakeMarketData(new Dictionary<string, decimal>
        {
            ["NVDA"] = 237.10m,    // USD underlying
            ["NVDA.TO"] = 52.82m,  // CAD CDR quote (must NOT be returned)
            ["RY.TO"] = 180.00m,
        });
        var resolver = new FakeResolver(new Dictionary<string, string> { ["NVDA.TO"] = "NVDA" });
        var controller = CreateController(market, resolver);

        var action = await controller.GetBatchPrices(["nvda.to", "ry.to"], CancellationToken.None);

        var rows = ToRows(action);
        Assert.Equal(237.10m, rows["NVDA.TO"]);
        Assert.Equal(180.00m, rows["RY.TO"]);
        Assert.Equal(2, rows.Count);
        Assert.Contains("NVDA", market.RequestedSymbols);
        Assert.DoesNotContain("NVDA.TO", market.RequestedSymbols);
    }

    [Fact]
    public async Task GetBatchPrices_SkipsSymbolsWithoutAQuote()
    {
        var controller = CreateController(new FakeMarketData([]), new FakeResolver([]));

        var action = await controller.GetBatchPrices(["ZZZ"], CancellationToken.None);

        Assert.Empty(ToRows(action));
    }

    private static StocksController CreateController(FakeMarketData market, FakeResolver resolver) =>
        new(market, null!, null!, null!, null!, resolver)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

    private static Dictionary<string, decimal> ToRows(IActionResult action)
    {
        var ok = Assert.IsType<OkObjectResult>(action);
        var result = new Dictionary<string, decimal>();
        foreach (var row in (System.Collections.IEnumerable)ok.Value!)
        {
            var type = row.GetType();
            result[(string)type.GetProperty("symbol")!.GetValue(row)!] = (decimal)type.GetProperty("price")!.GetValue(row)!;
        }
        return result;
    }

    private sealed class FakeResolver(Dictionary<string, string> underlyingByTrading) : ISecurityAnalysisResolver
    {
        public Task<ResolvedSecurityAnalysis> ResolveAsync(string tradingTicker, string? userId, CancellationToken ct = default)
        {
            var uses = underlyingByTrading.TryGetValue(tradingTicker, out var underlying);
            return Task.FromResult(new ResolvedSecurityAnalysis(
                tradingTicker, uses ? underlying! : tradingTicker, uses ? "US" : "CA", uses ? "USD" : "CAD",
                uses, uses ? UnderlyingResolutionStatus.Resolved : UnderlyingResolutionStatus.NotApplicable, null));
        }

        public Task<bool> ValidateUnderlyingTickerAsync(string underlyingTicker, CancellationToken ct = default) => Task.FromResult(true);
        public Task<ResolvedSecurityAnalysis> SaveUserMappingAsync(string tradingTicker, string underlyingTicker, string userId, bool useUnderlyingForAnalysis, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> RemoveUserMappingAsync(string tradingTicker, string userId, CancellationToken ct = default) => Task.FromResult(false);
    }

    private sealed class FakeMarketData(Dictionary<string, decimal> prices) : IMarketDataProvider
    {
        public List<string> RequestedSymbols { get; } = [];

        public Task<Dictionary<string, StockQuote>> GetBatchQuotesAsync(IEnumerable<string> symbols, CancellationToken ct = default)
        {
            var list = symbols.ToList();
            RequestedSymbols.AddRange(list);
            return Task.FromResult(list.Where(prices.ContainsKey)
                .ToDictionary(s => s, s => new StockQuote { Symbol = s, CurrentPrice = prices[s] }));
        }

        public Task<StockQuote?> GetQuoteAsync(string symbol, CancellationToken ct = default) => Task.FromResult<StockQuote?>(null);
        public Task<IReadOnlyList<MarketDailyClose>?> GetDailyClosesAsync(string symbol, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<MarketDailyClose>?>(null);
        public Task<(string sector, string industry)> GetSectorAsync(string symbol, CancellationToken ct = default) => Task.FromResult(("", ""));
        public Task<IReadOnlyList<SymbolSearchResult>> SearchSymbolAsync(string query, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SymbolSearchResult>>([]);
        public Task<Dictionary<string, decimal>> GetAnalystTargetsAsync(IEnumerable<string> symbols, CancellationToken ct = default) => Task.FromResult(new Dictionary<string, decimal>());
        public Task<FundamentalsSnapshot?> GetFundamentalsAsync(string symbol, CancellationToken ct = default) => Task.FromResult<FundamentalsSnapshot?>(null);
        public Task<Dictionary<string, DateTime>> GetEarningsDatesAsync(IEnumerable<string> symbols, CancellationToken ct = default) => Task.FromResult(new Dictionary<string, DateTime>());
        public Task<Dictionary<string, decimal>> GetHistoricalClosingPricesAsync(string dateStr, IEnumerable<string> symbols, CancellationToken ct = default) => Task.FromResult(new Dictionary<string, decimal>());
    }
}
