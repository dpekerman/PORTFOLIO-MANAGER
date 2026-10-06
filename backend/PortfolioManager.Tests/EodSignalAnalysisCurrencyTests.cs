using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Tests;

/// <summary>Persisted EOD signals must remember which security (and currency) their prices belong to.</summary>
public class EodSignalAnalysisCurrencyTests
{
    [Fact]
    public async Task SaveAsync_StoresAnalysisTickerAndCurrencyForUnderlyingAndNativeSignals()
    {
        var (service, scopeFactory) = CreateService();

        await service.SaveAsync([
            Promoted("NVDA.TO", price: 237m, underlying: "NVDA"),
            Promoted("RY.TO", price: 180m, underlying: null),
        ]);

        using var scope = scopeFactory.CreateScope();
        var rows = await scope.ServiceProvider.GetRequiredService<AppDbContext>().DailySignals.ToListAsync();
        var cdr = Assert.Single(rows, r => r.Symbol == "NVDA.TO");
        var plain = Assert.Single(rows, r => r.Symbol == "RY.TO");
        Assert.Equal("NVDA", cdr.AnalysisTicker);
        Assert.Equal("USD", cdr.AnalysisCurrency);
        Assert.Equal(237m, cdr.Price);
        Assert.Equal("RY.TO", plain.AnalysisTicker);
        Assert.Equal("CAD", plain.AnalysisCurrency);
    }

    [Fact]
    public async Task SaveAsync_RefreshOfExistingSignal_UpdatesAnalysisFields()
    {
        var (service, scopeFactory) = CreateService();
        await service.SaveAsync([Promoted("NVDA.TO", 237m, underlying: null)]);

        await service.SaveAsync([Promoted("NVDA.TO", 238m, underlying: "NVDA")]);

        using var scope = scopeFactory.CreateScope();
        var row = Assert.Single(await scope.ServiceProvider.GetRequiredService<AppDbContext>().DailySignals.ToListAsync());
        Assert.Equal("NVDA", row.AnalysisTicker);
        Assert.Equal("USD", row.AnalysisCurrency);
        Assert.Equal(238m, row.Price);
    }

    [Fact]
    public async Task SaveAsync_InfersUsCurrencyWhenScannerResultHasNoCurrencyMetadata()
    {
        var (service, scopeFactory) = CreateService();
        var result = Promoted("AAPL", 225m, underlying: null);
        result.AnalysisCurrency = "";

        await service.SaveAsync([result]);

        using var scope = scopeFactory.CreateScope();
        var row = Assert.Single(await scope.ServiceProvider.GetRequiredService<AppDbContext>().DailySignals.ToListAsync());
        Assert.Equal("AAPL", row.AnalysisTicker);
        Assert.Equal("USD", row.AnalysisCurrency);
    }

    // Passes the Stage-2 gate: Bull Turn + strong close near the high + volume >= 1.5x.
    private static RsiScanResult Promoted(string symbol, decimal price, string? underlying) => new()
    {
        Symbol = symbol,
        CompanyName = symbol,
        ScanType = ScanType.Oversold,
        Status = SignalStatus.Confirmed,
        Rsi = 30m,
        CurrentPrice = price,
        OpenPrice = price - 5m,
        DayHigh = price,
        DayLow = price - 6m,
        DailyAtr = 4m,
        VolumeRatio = 2m,
        RsiDelta1D = 1m,
        TrendShift = "Bull Turn",
        TradingDate = new DateOnly(2026, 10, 1),
        UsesUnderlyingSecurity = underlying is not null,
        AnalysisTicker = underlying ?? symbol,
        AnalysisCurrency = underlying is not null ? "USD" : "CAD",
    };

    private static (EodSignalPersistenceService, IServiceScopeFactory) CreateService()
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddSingleton<IStagedSignalService, NoOpStagedSignals>();
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        return (new EodSignalPersistenceService(NullLogger<EodSignalPersistenceService>.Instance, scopeFactory), scopeFactory);
    }

    private sealed class NoOpStagedSignals : IStagedSignalService
    {
        public Task<Dictionary<string, ScanType>> LoadActiveStagedSymbolsAsync(CancellationToken ct = default) => Task.FromResult(new Dictionary<string, ScanType>());
        public Task UpsertAndEnrichAsync(IEnumerable<RsiScanResult> results, decimal trendShiftThreshold = 0.25m, decimal earlyMin = 0.25m, decimal normalMin = 1.0m, decimal strongMin = 5.0m, decimal explosiveMin = 10.0m, int maxActiveTradingDays = 7, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeactivateAsync(string symbol, string scanType, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> CleanupStaleAsync(int retentionDays = 30, CancellationToken ct = default) => Task.FromResult(0);
    }
}
