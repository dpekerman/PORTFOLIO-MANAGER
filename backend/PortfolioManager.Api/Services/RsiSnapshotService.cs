using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public interface IRsiSnapshotService
{
    Task SaveAsync(ScannerResponse response, CancellationToken ct = default);
    Task<ScannerResponse?> GetLatestAsync(CancellationToken ct = default);
}

public class RsiSnapshotService(AppDbContext db, ILogger<RsiSnapshotService> logger) : IRsiSnapshotService
{
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public async Task SaveAsync(ScannerResponse response, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(response, _json);
        var existing = await db.RsiScanSnapshots.FindAsync([1], ct);
        if (existing is null)
        {
            db.RsiScanSnapshots.Add(new RsiScanSnapshot
            {
                Id = 1,
                SnapshotJson = json,
                ScannedAt = response.ScannedAt,
                SymbolCount = response.OversoldChain.Count + response.OverboughtChain.Count,
                OversoldCount = response.OversoldChain.Count,
                OverboughtCount = response.OverboughtChain.Count,
            });
        }
        else
        {
            existing.SnapshotJson = json;
            existing.ScannedAt = response.ScannedAt;
            existing.SymbolCount = response.OversoldChain.Count + response.OverboughtChain.Count;
            existing.OversoldCount = response.OversoldChain.Count;
            existing.OverboughtCount = response.OverboughtChain.Count;
        }

        await db.SaveChangesAsync(ct);
        logger.LogDebug("[RsiSnapshot] Saved snapshot: {Os} oversold, {Ob} overbought.",
            response.OversoldChain.Count, response.OverboughtChain.Count);
    }

    public async Task<ScannerResponse?> GetLatestAsync(CancellationToken ct = default)
    {
        var row = await db.RsiScanSnapshots.FindAsync([1], ct);
        if (row is null) return null;

        try
        {
            return JsonSerializer.Deserialize<ScannerResponse>(row.SnapshotJson, _json);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[RsiSnapshot] Failed to deserialize snapshot JSON.");
            return null;
        }
    }
}
