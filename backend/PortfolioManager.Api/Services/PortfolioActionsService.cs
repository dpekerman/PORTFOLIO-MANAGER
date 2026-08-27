using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public interface IPortfolioActionsService
{
    Task<IReadOnlyList<PortfolioActionDto>> GetActionsAsync(string userId, CancellationToken ct = default);
}

public sealed class PortfolioActionsService(AppDbContext db) : IPortfolioActionsService
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private static readonly HashSet<string> BullishShifts = new(StringComparer.OrdinalIgnoreCase)
        { "🟢 Bull Turn", "🟡 Stabilizing" };
    private static readonly HashSet<string> BearishShifts = new(StringComparer.OrdinalIgnoreCase)
        { "🟢 Bear Turn", "🔴 Still Rising" };

    public async Task<IReadOnlyList<PortfolioActionDto>> GetActionsAsync(string userId, CancellationToken ct = default)
    {
        // Load data from snapshots — avoids Yahoo Finance calls on every request
        var portfolioSnap = await db.PortfolioSnapshots.AsNoTracking()
            .SingleOrDefaultAsync(s => s.UserId == userId, ct);
        var watchlistSnap = await db.WatchlistSnapshots.AsNoTracking()
            .SingleOrDefaultAsync(s => s.UserId == userId, ct);
        var rsiSnap = await db.RsiScanSnapshots.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == 1, ct);
        var sectorTargets = await db.AllocationSectorTargets.AsNoTracking().ToListAsync(ct);

        var portfolio = Deserialize<List<PortfolioSummaryDto>>(portfolioSnap?.SnapshotJson ?? "[]") ?? [];
        var watchlist = Deserialize<List<WatchlistSummaryDto>>(watchlistSnap?.SnapshotJson ?? "[]") ?? [];
        var scanner = Deserialize<ScannerResponse>(rsiSnap?.SnapshotJson ?? "{}") ?? new ScannerResponse();

        // Build a flat lookup of all RSI signals keyed by symbol
        var signals = scanner.OversoldChain
            .Concat(scanner.OverboughtChain)
            .GroupBy(r => r.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        if (signals.Count == 0) return [];

        // Compute sector allocations to determine over/under status
        var totalValue = portfolio
            .Where(p => !IsClose(p.Item.TransactionType))
            .Sum(p => MarketValue(p));
        var sectorActuals = portfolio
            .Where(p => !IsClose(p.Item.TransactionType))
            .GroupBy(p => p.Item.Sector ?? "", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key,
                g => totalValue > 0 ? g.Sum(p => MarketValue(p)) / totalValue * 100m : 0m,
                StringComparer.OrdinalIgnoreCase);

        var results = new List<PortfolioActionDto>();

        // Portfolio holdings with signals
        var portfolioBySymbol = portfolio
            .Where(p => !IsClose(p.Item.TransactionType))
            .GroupBy(p => p.Item.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var (symbol, scan) in signals)
        {
            portfolioBySymbol.TryGetValue(symbol, out var pos);
            var wlItem = watchlist.FirstOrDefault(w => string.Equals(w.Item.Symbol, symbol, StringComparison.OrdinalIgnoreCase));

            if (pos is null && wlItem is null) continue;

            var holdingRole = pos?.Item.HoldingRole ?? wlItem?.Item.Role ?? "Strategic";
            var sector = pos?.Item.Sector ?? scan.Sector ?? "";
            var allocationStatus = ComputeAllocationStatus(sector, sectorActuals, sectorTargets);
            var (actionLabel, severity) = DeriveAction(scan, holdingRole, allocationStatus);

            results.Add(new PortfolioActionDto(
                Symbol: symbol,
                CompanyName: scan.CompanyName,
                HoldingRole: holdingRole,
                ScanType: scan.ScanType.ToString(),
                Rsi: scan.Rsi,
                TrendShift: scan.TrendShift ?? "",
                FibZone: scan.FibZone ?? "",
                ChaseRisk: scan.ChaseRisk ?? "",
                AllocationStatus: allocationStatus,
                ActionLabel: actionLabel,
                ActionSeverity: severity,
                IsInPortfolio: pos is not null,
                IsInWatchlist: wlItem is not null));
        }

        // Sort: portfolio first, then by severity priority, then by RSI
        return results
            .OrderByDescending(r => r.IsInPortfolio)
            .ThenBy(r => SeverityOrder(r.ActionSeverity))
            .ThenBy(r => r.ScanType == "Oversold" ? r.Rsi : 100 - r.Rsi)
            .ToList()
            .AsReadOnly();
    }

    private static (string label, string severity) DeriveAction(RsiScanResult scan, string role, string allocationStatus)
    {
        // Chase risk overrides everything
        if (!string.IsNullOrEmpty(scan.ChaseRisk))
            return ("DO NOT CHASE", "danger");

        var isBullish = BullishShifts.Contains(scan.TrendShift ?? "");
        var isBearish = BearishShifts.Contains(scan.TrendShift ?? "");
        var isTrendDamage = string.Equals(scan.FibZone, "Trend Damage", StringComparison.OrdinalIgnoreCase);
        var isOversold = scan.ScanType == ScanType.Oversold;
        var isCore = string.Equals(role, "Core", StringComparison.OrdinalIgnoreCase);
        var isSwingSpec = role is "Swing" or "Speculative";

        if (isTrendDamage)
            return ("REVIEW — TREND DAMAGE", "review");

        if (isOversold && isBullish)
        {
            if (isCore)
                return allocationStatus == "over" ? ("HOLD — SECTOR OVERWEIGHT", "hold") : ("ADD WATCH", "buy");
            if (isSwingSpec)
                return ("ENTRY CANDIDATE", "buy");
            return ("BUY WATCH", "buy");
        }

        if (isOversold && !isBullish)
            return ("WAIT — STILL FALLING", "wait");

        if (!isOversold && isBearish)
        {
            if (isCore)
                return ("HOLD / TRIM WATCH", "trim");
            if (isSwingSpec)
                return ("TRIM / TAKE PROFIT", "trim");
            return ("TRIM WATCH", "trim");
        }

        if (!isOversold && !isBearish)
            return ("HOLD — EXTENDED", "hold");

        return ("MONITOR", "hold");
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
        if (p.Item.IsManual)
            return p.Item.ManualMarketValue ?? p.Item.AverageCostBasis * p.Item.Shares;
        return (p.Quote?.CurrentPrice ?? p.Item.AverageCostBasis) * p.Item.Shares;
    }

    private static int SeverityOrder(string severity) => severity switch
    {
        "buy"    => 0,
        "trim"   => 1,
        "review" => 2,
        "danger" => 3,
        "wait"   => 4,
        _        => 5,
    };

    private static T? Deserialize<T>(string json)
    {
        try { return JsonSerializer.Deserialize<T>(json, JsonOpts); }
        catch { return default; }
    }
}
