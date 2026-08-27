using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public interface ITransactionContextCaptureService
{
    /// <summary>Captures current market context for a newly created transaction. Non-throwing.</summary>
    Task TryCaptureAsync(int transactionId, string symbol, string? holdingRole, string? sector, CancellationToken ct = default);
    Task<TransactionContextSnapshot?> GetSnapshotAsync(int transactionId, CancellationToken ct = default);
}

public sealed class TransactionContextCaptureService(AppDbContext db) : ITransactionContextCaptureService
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task TryCaptureAsync(int transactionId, string symbol, string? holdingRole, string? sector, CancellationToken ct = default)
    {
        try
        {
            var rsiSnap = await db.RsiScanSnapshots.AsNoTracking()
                .SingleOrDefaultAsync(s => s.Id == 1, ct);
            var sectorTargets = await db.AllocationSectorTargets.AsNoTracking().ToListAsync(ct);
            var valueSnap = await db.ValueScreenerSnapshots.AsNoTracking()
                .Where(s => s.Origin == "Portfolio" || s.Origin == "Watchlist")
                .OrderByDescending(s => s.RunAt)
                .FirstOrDefaultAsync(ct);

            RsiScanResult? scanResult = null;
            if (rsiSnap is not null)
            {
                var scanner = Deserialize<ScannerResponse>(rsiSnap.SnapshotJson);
                scanResult = scanner?.OversoldChain
                    .Concat(scanner.OverboughtChain)
                    .FirstOrDefault(r => string.Equals(r.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
            }

            decimal? valueScore = null;
            string? valueTier = null;
            if (valueSnap is not null)
            {
                var results = Deserialize<List<ValueScreenerResult>>(valueSnap.ResultsJson);
                var match = results?.FirstOrDefault(r => string.Equals(r.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
                if (match is not null) { valueScore = match.Score; valueTier = match.Tier.ToString(); }
            }

            // Compute sector allocation status
            string? allocationStatus = null;
            if (!string.IsNullOrEmpty(sector))
            {
                var target = sectorTargets.FirstOrDefault(t =>
                    string.Equals(t.Sector, sector, StringComparison.OrdinalIgnoreCase));
                if (target is not null)
                {
                    var portfolioItems = await db.PortfolioItems.AsNoTracking()
                        .Where(p => p.TransactionType != "CLOSE")
                        .ToListAsync(ct);
                    var total = portfolioItems.Sum(p => p.AverageCostBasis * p.Shares);
                    var sectorPct = total > 0
                        ? portfolioItems
                            .Where(p => string.Equals(p.Sector, sector, StringComparison.OrdinalIgnoreCase))
                            .Sum(p => p.AverageCostBasis * p.Shares) / total * 100m
                        : 0m;
                    var delta = sectorPct - target.TargetPct;
                    allocationStatus = delta > 2m ? "over" : delta < -2m ? "under" : "on-target";
                }
            }

            var snapshot = new TransactionContextSnapshot
            {
                TransactionId              = transactionId,
                CapturedAt                 = DateTime.UtcNow,
                RsiAtEntry                 = scanResult?.Rsi,
                TrendShiftAtEntry          = scanResult?.TrendShift,
                FibZoneAtEntry             = scanResult?.FibZone,
                VolumeSignalAtEntry        = scanResult?.VolumeSignal,
                TurnStrengthAtEntry        = scanResult?.TurnStrength,
                ValueScoreAtEntry          = valueScore,
                ValueTierAtEntry           = valueTier,
                HoldingRoleAtEntry         = holdingRole,
                SectorAllocationStatusAtEntry = allocationStatus,
            };

            db.TransactionContextSnapshots.Add(snapshot);
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            // Context capture is non-critical — never fail the main transaction
        }
    }

    private static T? Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        try { return JsonSerializer.Deserialize<T>(json, JsonOpts); }
        catch { return default; }
    }

    public async Task<TransactionContextSnapshot?> GetSnapshotAsync(int transactionId, CancellationToken ct = default)
        => await db.TransactionContextSnapshots.AsNoTracking()
            .FirstOrDefaultAsync(s => s.TransactionId == transactionId, ct);
}
