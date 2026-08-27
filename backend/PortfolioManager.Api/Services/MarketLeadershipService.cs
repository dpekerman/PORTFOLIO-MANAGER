using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public sealed record MarketLeadershipRow(
    string Sector,
    int SymbolCount,
    decimal AvgRsi,
    decimal Avg1MReturnPct,
    int PctAboveEma20,          // % of symbols whose price > EMA20
    string Leadership,          // Strong | Improving | Neutral | Weakening | Declining
    string LeadershipEmoji);

public sealed record MarketLeadershipResponse(
    IReadOnlyList<MarketLeadershipRow> Rows,
    DateTime ComputedAt);

public interface IMarketLeadershipService
{
    Task<MarketLeadershipResponse> GetLeadershipAsync(string userId, CancellationToken ct = default);
}

public sealed class MarketLeadershipService(AppDbContext db) : IMarketLeadershipService
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<MarketLeadershipResponse> GetLeadershipAsync(string userId, CancellationToken ct = default)
    {
        var portfolioSnap = await db.PortfolioSnapshots.AsNoTracking().SingleOrDefaultAsync(s => s.UserId == userId, ct);
        var watchlistSnap = await db.WatchlistSnapshots.AsNoTracking().SingleOrDefaultAsync(s => s.UserId == userId, ct);
        var rsiSnap = await db.RsiScanSnapshots.AsNoTracking().SingleOrDefaultAsync(s => s.Id == 1, ct);

        var portfolio = Deserialize<List<PortfolioSummaryDto>>(portfolioSnap?.SnapshotJson ?? "[]") ?? [];
        var watchlist = Deserialize<List<WatchlistSummaryDto>>(watchlistSnap?.SnapshotJson ?? "[]") ?? [];
        var scanner = Deserialize<ScannerResponse>(rsiSnap?.SnapshotJson ?? "{}") ?? new ScannerResponse();

        // Build signal map for RSI + trend context
        var signalMap = scanner.OversoldChain.Concat(scanner.OverboughtChain)
            .GroupBy(r => r.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // Collect all symbols with their sector + quote data
        var allItems = portfolio
            .Where(p => !IsClose(p.Item.TransactionType) && !string.IsNullOrEmpty(p.Item.Sector))
            .Select(p => new
            {
                Symbol = p.Item.Symbol,
                Sector = p.Item.Sector ?? "",
                Price = p.Quote?.CurrentPrice ?? 0m,
                ChangePercent1D = p.Quote?.ChangePercent ?? 0m,
                // Approximate 1M return from EMA deviation if no direct 1M data
                Ema20 = signalMap.TryGetValue(p.Item.Symbol, out var s) ? s.Ema20Price : 0m,
                Rsi = signalMap.TryGetValue(p.Item.Symbol, out var rs) ? rs.Rsi : 0m,
            })
            .Concat(
                watchlist.Select(w => new
                {
                    Symbol = w.Item.Symbol,
                    Sector = w.Quote?.Sector ?? "",
                    Price = w.Quote?.CurrentPrice ?? 0m,
                    ChangePercent1D = w.Quote?.ChangePercent ?? 0m,
                    Ema20 = signalMap.TryGetValue(w.Item.Symbol, out var s) ? s.Ema20Price : 0m,
                    Rsi = signalMap.TryGetValue(w.Item.Symbol, out var rs) ? rs.Rsi : 0m,
                }).Where(w => !string.IsNullOrEmpty(w.Sector))
            )
            .GroupBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First()) // deduplicate
            .ToList();

        if (allItems.Count == 0)
            return new MarketLeadershipResponse([], DateTime.UtcNow);

        var rows = allItems
            .GroupBy(x => x.Sector, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() >= 2) // sectors with at least 2 symbols
            .Select(g =>
            {
                var items = g.ToList();
                var avgRsi = items.Where(x => x.Rsi > 0).Select(x => x.Rsi).DefaultIfEmpty(50m).Average();
                var avg1M = items.Select(x => x.ChangePercent1D).Average(); // using 1D as proxy
                var aboveEma20Count = items.Count(x => x.Price > 0 && x.Ema20 > 0 && x.Price > x.Ema20);
                var pctAbove = items.Count > 0 ? (int)((double)aboveEma20Count / items.Count * 100) : 0;

                var (label, emoji) = ClassifyLeadership(avgRsi, pctAbove, avg1M);

                return new MarketLeadershipRow(
                    Sector: g.Key,
                    SymbolCount: items.Count,
                    AvgRsi: Math.Round(avgRsi, 1),
                    Avg1MReturnPct: Math.Round(avg1M, 2),
                    PctAboveEma20: pctAbove,
                    Leadership: label,
                    LeadershipEmoji: emoji);
            })
            .OrderBy(r => LeadershipOrder(r.Leadership))
            .ThenByDescending(r => r.PctAboveEma20)
            .ToList();

        return new MarketLeadershipResponse(rows.AsReadOnly(), DateTime.UtcNow);
    }

    private static (string label, string emoji) ClassifyLeadership(decimal avgRsi, int pctAboveEma20, decimal avg1M)
    {
        // Overbought territory with high breadth = Strong
        if (avgRsi > 65 && pctAboveEma20 >= 70) return ("Strong", "🔥");
        if (avgRsi > 55 && pctAboveEma20 >= 55) return ("Improving", "↑");
        if (avgRsi < 35 && pctAboveEma20 < 30) return ("Declining", "↓↓");
        if (avgRsi < 45 && pctAboveEma20 < 45) return ("Weakening", "↓");
        return ("Neutral", "→");
    }

    private static int LeadershipOrder(string leadership) => leadership switch
    {
        "Strong"    => 0,
        "Improving" => 1,
        "Neutral"   => 2,
        "Weakening" => 3,
        "Declining" => 4,
        _           => 5,
    };

    private static bool IsClose(string? txType)
        => string.Equals(txType, "CLOSE", StringComparison.OrdinalIgnoreCase);

    private static T? Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        try { return JsonSerializer.Deserialize<T>(json, JsonOpts); }
        catch { return default; }
    }
}
