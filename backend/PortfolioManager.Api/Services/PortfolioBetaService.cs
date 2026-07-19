using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public interface IPortfolioBetaService
{
    Task<PortfolioBetaResult> CalculateAsync(CancellationToken ct);
}

/// <summary>
/// Calculates the weighted portfolio beta using Yahoo Finance fundamentals.
/// CDRs (e.g. PYPL.TO) use the underlying US ticker. Options use the underlying stock beta.
/// Cash has beta = 0. Missing betas are replaced with sector proxies (marked as proxy).
/// </summary>
public sealed class PortfolioBetaService(
    AppDbContext db,
    IMarketDataProvider marketData,
    ILogger<PortfolioBetaService> logger) : IPortfolioBetaService
{
    // Sector average betas (proxy fallback)
    private static readonly Dictionary<string, decimal> SectorBetas = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Technology"] = 1.25m,
        ["Financials"] = 1.10m,
        ["Health Care"] = 0.85m,
        ["Consumer Discretionary"] = 1.15m,
        ["Consumer Staples"] = 0.65m,
        ["Industrials"] = 1.05m,
        ["Energy"] = 1.20m,
        ["Materials"] = 1.10m,
        ["Utilities"] = 0.55m,
        ["Real Estate"] = 0.90m,
        ["Communication Services"] = 1.10m,
    };

    public async Task<PortfolioBetaResult> CalculateAsync(CancellationToken ct)
    {
        // Load all open positions
        var stocks = await db.PortfolioItems
            .Where(p => p.TransactionType != "CLOSE" && !p.IsManual)
            .ToListAsync(ct);
        var cashTotal = await db.CashItems.SumAsync(c => c.Amount, ct);
        var options = await db.OptionItems
            .Where(o => o.TransactionType != "CLOSE")
            .ToListAsync(ct);

        // Get current prices for stocks
        var symbols = stocks.Select(s => s.Symbol).Distinct().ToList();
        var optionUnderlyings = options.Select(o => o.UnderlyingTicker).Distinct().ToList();
        var allSymbols = symbols.Concat(optionUnderlyings).Distinct().ToList();

        Dictionary<string, decimal> prices = new(StringComparer.OrdinalIgnoreCase);
        if (allSymbols.Count > 0)
        {
            var quotes = await marketData.GetBatchQuotesAsync(allSymbols, ct);
            foreach (var kv in quotes) prices[kv.Key] = kv.Value.CurrentPrice;
        }

        // Compute market values
        var positionValues = new List<(string Symbol, decimal MarketValue, bool IsOption)>();
        foreach (var s in stocks)
        {
            var price = prices.TryGetValue(s.Symbol, out var p) ? p : s.AverageCostBasis;
            positionValues.Add((s.Symbol, price * s.Shares, false));
        }
        foreach (var o in options)
        {
            var mv = o.MarketPrice * o.NumberOfContracts * 100;
            positionValues.Add((o.UnderlyingTicker, mv, true));
        }

        var totalValue = positionValues.Sum(pv => pv.MarketValue) + cashTotal;
        if (totalValue <= 0) return new PortfolioBetaResult(0, 0, 0, 0, "Good", []);

        // Determine which underlying tickers we need beta for
        var betaSymbols = positionValues
            .Select(pv => StripCdr(pv.Symbol))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var betaMap = new Dictionary<string, (decimal Beta, bool IsProxy)>(StringComparer.OrdinalIgnoreCase);
        foreach (var sym in betaSymbols)
        {
            var beta = await FetchBetaAsync(sym, ct);
            betaMap[sym] = beta;
            await Task.Delay(300, ct); // throttle per Yahoo Finance conventions
        }

        // Aggregate by symbol (group multiple lots)
        var grouped = positionValues
            .GroupBy(pv => StripCdr(pv.Symbol), StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var mv = g.Sum(pv => pv.MarketValue);
                var (b, proxy) = betaMap.TryGetValue(g.Key, out var bv) ? bv : (1.0m, true);
                return (Symbol: g.Key, MarketValue: mv, Beta: b, IsProxy: proxy);
            })
            .ToList();

        // Weighted beta
        decimal stocksAndOptionsTotal = grouped.Sum(g => g.MarketValue);
        decimal portfolioBeta = 0m;
        decimal proxyValue = 0m;

        foreach (var g in grouped)
        {
            var weight = g.MarketValue / totalValue;
            portfolioBeta += weight * g.Beta;
            if (g.IsProxy) proxyValue += g.MarketValue;
        }
        // Cash contributes 0 (beta = 0)

        decimal exCashBeta = stocksAndOptionsTotal > 0
            ? grouped.Sum(g => (g.MarketValue / stocksAndOptionsTotal) * g.Beta)
            : 0m;

        var cashPct = cashTotal / totalValue * 100m;
        var proxyPct = stocksAndOptionsTotal > 0 ? proxyValue / stocksAndOptionsTotal * 100m : 0m;

        var status = portfolioBeta < 0.95m ? "Good"
            : portfolioBeta <= 1.05m ? "Warning"
            : "TooMuchRisk";

        // Top 5 contributors (highest weighted beta impact)
        var top5 = grouped
            .OrderByDescending(g => Math.Abs((g.MarketValue / totalValue) * g.Beta))
            .Take(5)
            .Select(g => new BetaContributor(
                g.Symbol,
                Math.Round(g.MarketValue / totalValue * 100, 2),
                Math.Round(g.Beta, 2),
                g.IsProxy))
            .ToList();

        return new PortfolioBetaResult(
            Math.Round(portfolioBeta, 2),
            Math.Round(exCashBeta, 2),
            Math.Round(cashPct, 1),
            Math.Round(proxyPct, 1),
            status,
            top5);
    }

    private async Task<(decimal Beta, bool IsProxy)> FetchBetaAsync(string symbol, CancellationToken ct)
    {
        try
        {
            var snap = await marketData.GetFundamentalsAsync(symbol, ct);
            if (snap is not null && snap.Beta != 0)
                return (snap.Beta, false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[PortfolioBeta] Failed to fetch beta for {Symbol}", symbol);
        }

        // Fallback: sector proxy
        return (1.0m, true);
    }

    /// <summary>Converts CDR tickers to underlying US ticker (e.g. PYPL.TO → PYPL).</summary>
    private static string StripCdr(string symbol)
    {
        if (symbol.EndsWith(".TO", StringComparison.OrdinalIgnoreCase))
            return symbol[..^3];
        if (symbol.EndsWith(".UN", StringComparison.OrdinalIgnoreCase))
            return symbol[..^3];
        return symbol;
    }
}
