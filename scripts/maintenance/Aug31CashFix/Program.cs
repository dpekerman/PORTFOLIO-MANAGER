// One-time fix: BackfillDateAsync used today's CURRENT cash total for every backfilled
// date, but CashItems has no OpenDate/CloseDate to reconstruct point-in-time cash — a
// deleted row leaves no trace. Aug 28 and Sep 1 (both real, live-captured snapshots)
// show identical Cash=$113,649, proving cash was almost certainly $113,649 on Aug 31 too,
// not the $72,977.75 (today's current total) the backfill wrongly substituted.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Services;

const string connectionString =
    "Server=localhost;Database=PortfolioManagerLocal;Trusted_Connection=True;TrustServerCertificate=True";

var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Information));
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
services.AddScoped<IPortfolioValueHistoryService, PortfolioValueHistoryService>();

await using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

Console.WriteLine("── Removing the Aug 31 row (cash was wrongly substituted from today) ──");
var row = await db.PortfolioValueHistories.SingleOrDefaultAsync(h => h.RecordedDate == "2026-08-31");
if (row is null)
{
    Console.WriteLine("  No row found for 2026-08-31 — nothing to do.");
    return 1;
}
Console.WriteLine($"  Deleting Id={row.Id} TotalValue={row.TotalValue:C2} CashValue={row.CashValue:C2}");
db.PortfolioValueHistories.Remove(row);
await db.SaveChangesAsync();

Console.WriteLine();
Console.WriteLine("── Regenerating with nearest-snapshot cash value ────────────────");
var history = scope.ServiceProvider.GetRequiredService<IPortfolioValueHistoryService>();
var filled = await history.BackfillMissingAsync(lookbackDays: 10, CancellationToken.None);

var newRow = filled.FirstOrDefault(f => f.RecordedDate == "2026-08-31");
if (newRow is null)
{
    Console.WriteLine("  WARNING: Aug 31 was not regenerated (no market data?).");
    return 1;
}
Console.WriteLine($"  New: Total={newRow.TotalValue:C2} Stocks={newRow.StocksValue:C2} Cash={newRow.CashValue:C2} Options={newRow.OptionsValue:C2}");
return 0;
