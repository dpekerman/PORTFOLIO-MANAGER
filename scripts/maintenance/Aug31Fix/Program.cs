// One-time fix: BackfillDateAsync didn't filter OptionItems by OpenDate/CloseDate,
// so the regenerated Aug 31 row wrongly included ATD.TO (opened Sep 1) and excluded
// XGD.TO (open Aug 21-Sep 1). Delete and regenerate that single row with the fix applied.
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

Console.WriteLine("── Removing the Aug 31 row (options were wrongly composed) ────");
var row = await db.PortfolioValueHistories.SingleOrDefaultAsync(h => h.RecordedDate == "2026-08-31");
if (row is null)
{
    Console.WriteLine("  No row found for 2026-08-31 — nothing to do.");
    return 1;
}
Console.WriteLine($"  Deleting Id={row.Id} TotalValue={row.TotalValue:C2} OptionsValue={row.OptionsValue:C2}");
db.PortfolioValueHistories.Remove(row);
await db.SaveChangesAsync();

Console.WriteLine();
Console.WriteLine("── Regenerating with corrected option filtering ────────────────");
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
