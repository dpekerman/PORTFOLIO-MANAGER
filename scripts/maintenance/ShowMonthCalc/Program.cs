// Read-only: computes the live total exactly like DashboardService does, then shows
// the exact "This Month" calculation against the current Aug 31 baseline row.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

const string connectionString =
    "Server=localhost;Database=PortfolioManagerLocal;Trusted_Connection=True;TrustServerCertificate=True";

var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
services.AddDbContext<AppDbContext>(opts => opts.UseSqlServer(connectionString));
services.AddSingleton<YahooCrumbService>();
services.AddHttpClient<IMarketDataProvider, YahooFinanceService>(client =>
{
    client.DefaultRequestHeaders.Add("User-Agent",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
    client.DefaultRequestHeaders.Add("Accept", "*/*");
    client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
    client.Timeout = TimeSpan.FromSeconds(15);
});

await using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
var marketData = scope.ServiceProvider.GetRequiredService<IMarketDataProvider>();

var portfolioItems = await db.PortfolioItems
    .Where(p => p.TransactionType != "CLOSE")
    .ToListAsync();

var nonManualSymbols = portfolioItems.Where(p => !p.IsManual).Select(p => p.Symbol).Distinct().ToList();
var quotes = nonManualSymbols.Count > 0
    ? await marketData.GetBatchQuotesAsync(nonManualSymbols)
    : new Dictionary<string, StockQuote>();

decimal liveStocksValue = portfolioItems.Sum(p => p.IsManual
    ? (p.ManualMarketValue ?? p.AverageCostBasis * p.Shares)
    : (quotes.TryGetValue(p.Symbol, out var q) ? q.CurrentPrice : p.AverageCostBasis) * p.Shares);

var liveCashValue = await db.CashItems.SumAsync(c => c.Amount);
var liveOptionsValue = await db.OptionItems
    .Where(o => o.TransactionType != "CLOSE")
    .SumAsync(o => o.MarketPrice * o.NumberOfContracts * 100);

var liveTotal = liveStocksValue + liveCashValue + liveOptionsValue;

var monthBase = await db.PortfolioValueHistories.SingleAsync(h => h.RecordedDate == "2026-08-31");

var monthChange = liveTotal - monthBase.TotalValue;
var monthPercent = Math.Round(monthChange / monthBase.TotalValue * 100m, 2);

Console.WriteLine($"liveStocksValue  = {liveStocksValue:C2}");
Console.WriteLine($"liveCashValue    = {liveCashValue:C2}");
Console.WriteLine($"liveOptionsValue = {liveOptionsValue:C2}");
Console.WriteLine($"liveTotal        = {liveTotal:C2}");
Console.WriteLine();
Console.WriteLine($"monthBase (Aug 31) = {monthBase.TotalValue:C2}");
Console.WriteLine($"MonthChange  = {liveTotal:C2} - {monthBase.TotalValue:C2} = {monthChange:C2}");
Console.WriteLine($"MonthPercent = {monthPercent}%");
