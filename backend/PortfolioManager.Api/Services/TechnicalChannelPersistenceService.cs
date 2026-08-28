using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public interface ITechnicalChannelPersistenceService
{
    Task UpsertAsync(IEnumerable<RsiScanResult> results, CancellationToken ct = default);
}

public sealed class TechnicalChannelPersistenceService(AppDbContext db) : ITechnicalChannelPersistenceService
{
    public async Task UpsertAsync(IEnumerable<RsiScanResult> results, CancellationToken ct = default)
    {
        var rows = results
            .GroupBy(r => r.Symbol, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.Last())
            .ToList();
        if (rows.Count == 0) return;

        var symbols = rows.Select(r => r.Symbol).ToList();
        var existing = await db.TechnicalChannels
            .Where(c => c.Timeframe == "1D" && symbols.Contains(c.Ticker))
            .ToDictionaryAsync(c => c.Ticker, StringComparer.OrdinalIgnoreCase, ct);

        foreach (var result in rows)
        {
            if (!existing.TryGetValue(result.Symbol, out var row))
            {
                row = new TechnicalChannel { Ticker = result.Symbol, Timeframe = "1D" };
                db.TechnicalChannels.Add(row);
            }

            row.Direction = result.ChannelDirection;
            row.Slope = result.ChannelSlope;
            row.LowerRailCurrent = result.LowerRailToday;
            row.UpperRailCurrent = result.UpperRailToday;
            row.ChannelQuality = result.ChannelQuality;
            row.LowerTouchCount = result.PriorConfirmedLowerTouches;
            row.LastLowerTouchDate = result.LastLowerTouchDate;
            row.DistanceToLowerRailPercent = result.DistanceToLowerRailPercent;
            row.DistanceToLowerRailATR = result.DistanceToLowerRailATR;
            row.ChannelState = result.ChannelState;
            row.NearestOpenGapAbove = result.NearestOpenGapAbove;
            row.NearestOpenGapBelow = result.NearestOpenGapBelow;
            row.DistanceToGapAbovePercent = result.DistanceToGapAbovePercent;
            row.DistanceToGapBelowPercent = result.DistanceToGapBelowPercent;
            row.CalculatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }
}