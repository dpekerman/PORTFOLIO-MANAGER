using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public sealed record ActionScoreDto(
    string Symbol,
    string CompanyName,
    string HoldingRole,
    string WatchlistTier,
    decimal PortfolioNeedScore,   // 0-30
    decimal TechnicalScore,       // 0-30
    decimal FundamentalScore,     // 0-25
    decimal RiskScore,            // 0-15
    decimal TotalScore,           // 0-100
    string Badge,                 // HIGH_PRIORITY | WATCH | NO_ADD
    string TrendShift,
    decimal Rsi,
    string AllocationStatus);

public interface IPortfolioActionScoreService
{
    Task<IReadOnlyList<ActionScoreDto>> GetScoresAsync(string userId, CancellationToken ct = default);
}

public sealed class PortfolioActionScoreService(AppDbContext db) : IPortfolioActionScoreService
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<ActionScoreDto>> GetScoresAsync(string userId, CancellationToken ct = default)
    {
        var watchlistItems = await db.WatchlistItems.AsNoTracking()
            .Where(w => w.UserId == userId || w.UserId == null)
            .ToListAsync(ct);

        if (watchlistItems.Count == 0) return [];

        var portfolioSnap = await db.PortfolioSnapshots.AsNoTracking().SingleOrDefaultAsync(s => s.UserId == userId, ct);
        var rsiSnap = await db.RsiScanSnapshots.AsNoTracking().SingleOrDefaultAsync(s => s.Id == 1, ct);
        var sectorTargets = await db.AllocationSectorTargets.AsNoTracking().ToListAsync(ct);
        var roleTargets = await db.AllocationRiskTargets.AsNoTracking().ToListAsync(ct);
        var positionLimits = await db.SinglePositionLimits.AsNoTracking().ToListAsync(ct);
        var valueSnap = await db.ValueScreenerSnapshots.AsNoTracking()
            .OrderByDescending(s => s.RunAt).FirstOrDefaultAsync(ct);

        var portfolio = Deserialize<List<PortfolioSummaryDto>>(portfolioSnap?.SnapshotJson ?? "[]") ?? [];
        var scanner = Deserialize<ScannerResponse>(rsiSnap?.SnapshotJson ?? "{}") ?? new ScannerResponse();
        var valueResults = Deserialize<List<ValueScreenerResult>>(valueSnap?.ResultsJson ?? "[]") ?? [];

        // Build lookup maps
        var signalMap = scanner.OversoldChain.Concat(scanner.OverboughtChain)
            .GroupBy(r => r.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var valueMap = valueResults
            .GroupBy(r => r.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var openPortfolio = portfolio.Where(p => !IsClose(p.Item.TransactionType)).ToList();
        var totalValue = openPortfolio.Sum(p => MarketValue(p));

        // Sector actual allocation %
        var sectorActuals = openPortfolio
            .GroupBy(p => p.Item.Sector ?? "", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key,
                g => totalValue > 0 ? g.Sum(p => MarketValue(p)) / totalValue * 100m : 0m,
                StringComparer.OrdinalIgnoreCase);

        // Role actual allocation %
        var roleActuals = openPortfolio
            .GroupBy(p => p.Item.HoldingRole ?? "Strategic", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key,
                g => totalValue > 0 ? g.Sum(p => MarketValue(p)) / totalValue * 100m : 0m,
                StringComparer.OrdinalIgnoreCase);

        var results = new List<ActionScoreDto>();

        foreach (var item in watchlistItems)
        {
            signalMap.TryGetValue(item.Symbol, out var scan);
            valueMap.TryGetValue(item.Symbol, out var vs);

            // 1. Portfolio Need (30 pts) — sector underweight boosts score
            var portfolioNeed = ComputePortfolioNeed(item.Symbol, sectorActuals, sectorTargets, openPortfolio);

            // 2. Technical Setup (30 pts) — RSI scan state quality
            var technical = ComputeTechnicalScore(scan);

            // 3. Fundamental Quality (25 pts) — ValueScreener score
            var fundamental = vs is not null ? Math.Min(25m, vs.Score / 4m) : 0m; // score 0-100 → 0-25

            // 4. Risk / Position Room (15 pts) — how much room remains before limits hit
            var risk = ComputeRiskScore(item.Role ?? "Strategic", roleActuals, roleTargets, positionLimits, totalValue);

            var total = Math.Round(portfolioNeed + technical + fundamental + risk, 1);
            var badge = total >= 75 ? "HIGH_PRIORITY" : total >= 50 ? "WATCH" : "NO_ADD";

            // Determine allocation status for display
            var sectorForItem = scan?.Sector ?? "";
            var allocationStatus = ComputeAllocationStatus(sectorForItem, sectorActuals, sectorTargets);

            results.Add(new ActionScoreDto(
                Symbol: item.Symbol,
                CompanyName: scan?.CompanyName ?? item.Symbol,
                HoldingRole: item.Role ?? "Strategic",
                WatchlistTier: item.WatchlistTier ?? "Strategic",
                PortfolioNeedScore: portfolioNeed,
                TechnicalScore: technical,
                FundamentalScore: Math.Round(fundamental, 1),
                RiskScore: Math.Round(risk, 1),
                TotalScore: total,
                Badge: badge,
                TrendShift: scan?.TrendShift ?? "",
                Rsi: scan?.Rsi ?? 0m,
                AllocationStatus: allocationStatus));
        }

        return results
            .OrderByDescending(r => r.TotalScore)
            .ToList()
            .AsReadOnly();
    }

    private static decimal ComputePortfolioNeed(
        string symbol,
        Dictionary<string, decimal> sectorActuals,
        List<AllocationSectorTarget> targets,
        List<PortfolioSummaryDto> portfolio)
    {
        // Find the sector of this watchlist symbol from portfolio if already held, else skip
        var inPortfolio = portfolio.FirstOrDefault(p =>
            string.Equals(p.Item.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
        var sector = inPortfolio?.Item.Sector ?? "";
        if (string.IsNullOrEmpty(sector)) return 15m; // neutral when sector unknown

        var target = targets.FirstOrDefault(t => string.Equals(t.Sector, sector, StringComparison.OrdinalIgnoreCase));
        if (target is null) return 10m; // no target set

        sectorActuals.TryGetValue(sector, out var actual);
        var delta = actual - target.TargetPct;

        // Underweight = high need. Overweight = low need.
        if (delta < -5m) return 30m;
        if (delta < -2m) return 22m;
        if (delta <= 2m) return 15m;
        if (delta <= 5m) return 7m;
        return 0m;
    }

    private static decimal ComputeTechnicalScore(RsiScanResult? scan)
    {
        if (scan is null) return 0m;

        // Chase risk = zero technical score
        if (!string.IsNullOrEmpty(scan.ChaseRisk)) return 0m;

        var baseScore = scan.ScanType switch
        {
            ScanType.Oversold => scan.Rsi < 25 ? 20m : 14m,
            ScanType.Overbought => 0m, // overbought not a buy setup
            _ => 0m,
        };

        // Trend shift bonus
        if (scan.TrendShift?.Contains("Bull Turn") == true) baseScore += 8m;
        else if (scan.TrendShift?.Contains("Stabilizing") == true) baseScore += 4m;

        // Volume confirmation bonus
        if (scan.VolumeSignal == "Validated") baseScore += 2m;

        return Math.Min(30m, Math.Round(baseScore, 1));
    }

    private static decimal ComputeRiskScore(
        string role,
        Dictionary<string, decimal> roleActuals,
        List<AllocationRiskTarget> roleTargets,
        List<SinglePositionLimit> positionLimits,
        decimal totalValue)
    {
        var roleTarget = roleTargets.FirstOrDefault(t => string.Equals(t.Role, role, StringComparison.OrdinalIgnoreCase));
        if (roleTarget is null) return 8m; // no rule = neutral

        roleActuals.TryGetValue(role, out var roleActual);
        var roleRoom = roleTarget.TargetPct - roleActual;

        if (roleRoom > 5m) return 15m;
        if (roleRoom > 1m) return 10m;
        if (roleRoom >= 0m) return 5m;
        return 0m; // over role target
    }

    private static string ComputeAllocationStatus(
        string sector,
        Dictionary<string, decimal> actuals,
        List<AllocationSectorTarget> targets)
    {
        if (string.IsNullOrEmpty(sector)) return "";
        var target = targets.FirstOrDefault(t => string.Equals(t.Sector, sector, StringComparison.OrdinalIgnoreCase));
        if (target is null) return "";
        actuals.TryGetValue(sector, out var actual);
        var delta = actual - target.TargetPct;
        return delta > 2m ? "over" : delta < -2m ? "under" : "on-target";
    }

    private static bool IsClose(string? txType)
        => string.Equals(txType, "CLOSE", StringComparison.OrdinalIgnoreCase);

    private static decimal MarketValue(PortfolioSummaryDto p)
    {
        if (p.Item.IsManual) return p.Item.ManualMarketValue ?? p.Item.AverageCostBasis * p.Item.Shares;
        return (p.Quote?.CurrentPrice ?? p.Item.AverageCostBasis) * p.Item.Shares;
    }

    private static T? Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        try { return JsonSerializer.Deserialize<T>(json, JsonOpts); }
        catch { return default; }
    }
}
