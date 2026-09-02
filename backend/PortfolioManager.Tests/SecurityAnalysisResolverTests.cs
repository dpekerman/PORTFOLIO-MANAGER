using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Tests;

public class SecurityAnalysisResolverTests
{
    [Theory]
    [InlineData("SPGI.TO", "SPGI")]
    [InlineData("DIS.TO", "DIS")]
    [InlineData("MU.TO", "MU")]
    public async Task ResolveAsync_UsesManagedMappingForConfirmedCdr(string tradingTicker, string underlyingTicker)
    {
        await using var db = CreateDb();
        db.SecurityAnalysisMappings.Add(new SecurityAnalysisMapping
        {
            TradingTicker = tradingTicker,
            UnderlyingTicker = underlyingTicker,
            UnderlyingMarket = "US",
            UseUnderlyingForAnalysis = true,
            ResolutionStatus = UnderlyingResolutionStatus.Resolved,
            MappingSource = SecurityAnalysisMappingSource.AUTO,
        });
        await db.SaveChangesAsync();

        var result = await new SecurityAnalysisResolver(db, new FakeMarketData()).ResolveAsync(tradingTicker, "user-1");

        Assert.Equal(tradingTicker, result.TradingTicker);
        Assert.Equal(underlyingTicker, result.AnalysisTicker);
        Assert.True(result.UsesUnderlyingSecurity);
        Assert.Equal("USD", result.AnalysisCurrency);
    }

    [Theory]
    [InlineData("RY.TO", "CA", "CAD")]
    [InlineData("MSFT", "US", "USD")]
    public async Task ResolveAsync_KeepsOrdinarySecurityAsItsOwnAnalysisTicker(string ticker, string market, string currency)
    {
        await using var db = CreateDb();

        var result = await new SecurityAnalysisResolver(db, new FakeMarketData()).ResolveAsync(ticker, "user-1");

        Assert.Equal(ticker, result.AnalysisTicker);
        Assert.Equal(market, result.AnalysisMarket);
        Assert.Equal(currency, result.AnalysisCurrency);
        Assert.False(result.UsesUnderlyingSecurity);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsNeedsUserInputForDetectedWrapperWithoutMapping()
    {
        await using var db = CreateDb();
        var provider = new FakeMarketData { QuoteName = "Example Canadian Depositary Receipt CAD Hedged" };

        var result = await new SecurityAnalysisResolver(db, provider).ResolveAsync("EXAMPLE.TO", "user-1");

        Assert.Equal(UnderlyingResolutionStatus.NeedsUserInput, result.ResolutionStatus);
        Assert.False(result.UsesUnderlyingSecurity);
        Assert.Contains("Underlying U.S. ticker", result.DataError);
    }

    [Fact]
    public async Task ValidateUnderlyingTickerAsync_RequiresQuoteAndSufficientHistory()
    {
        await using var db = CreateDb();
        var provider = new FakeMarketData { HistoryCount = 199 };
        var resolver = new SecurityAnalysisResolver(db, provider);

        Assert.False(await resolver.ValidateUnderlyingTickerAsync("SPGI"));

        provider.HistoryCount = 200;
        Assert.True(await resolver.ValidateUnderlyingTickerAsync("SPGI"));
    }

    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    private sealed class FakeMarketData : IMarketDataProvider
    {
        public string QuoteName { get; set; } = "Example Security";
        public int HistoryCount { get; set; } = 200;

        public Task<StockQuote?> GetQuoteAsync(string symbol, CancellationToken ct = default) =>
            Task.FromResult<StockQuote?>(new StockQuote { Symbol = symbol, CompanyName = QuoteName, CurrentPrice = 100m });

        public Task<IReadOnlyList<MarketDailyClose>?> GetDailyClosesAsync(string symbol, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<MarketDailyClose>?>(Enumerable.Range(1, HistoryCount)
                .Select(day => new MarketDailyClose(new DateOnly(2025, 1, 1).AddDays(day), 100m, 100m, 100m, 100m, 1L))
                .ToList());

        public Task<Dictionary<string, StockQuote>> GetBatchQuotesAsync(IEnumerable<string> symbols, CancellationToken ct = default) => Task.FromResult(new Dictionary<string, StockQuote>());
        public Task<(string sector, string industry)> GetSectorAsync(string symbol, CancellationToken ct = default) => Task.FromResult(("", ""));
        public Task<IReadOnlyList<SymbolSearchResult>> SearchSymbolAsync(string query, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SymbolSearchResult>>([]);
        public Task<Dictionary<string, decimal>> GetAnalystTargetsAsync(IEnumerable<string> symbols, CancellationToken ct = default) => Task.FromResult(new Dictionary<string, decimal>());
        public Task<FundamentalsSnapshot?> GetFundamentalsAsync(string symbol, CancellationToken ct = default) => Task.FromResult<FundamentalsSnapshot?>(null);
        public Task<Dictionary<string, DateTime>> GetEarningsDatesAsync(IEnumerable<string> symbols, CancellationToken ct = default) => Task.FromResult(new Dictionary<string, DateTime>());
        public Task<Dictionary<string, decimal>> GetHistoricalClosingPricesAsync(string dateStr, IEnumerable<string> symbols, CancellationToken ct = default) => Task.FromResult(new Dictionary<string, decimal>());
    }
}