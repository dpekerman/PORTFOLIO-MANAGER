using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

/// <summary>
/// Persists Value Screener results to the database so the UI can load the latest
/// analysis without calling Yahoo Finance on every page visit.
/// </summary>
public class ValueScreenerPersistenceService(
    IServiceScopeFactory scopeFactory,
    ILogger<ValueScreenerPersistenceService> logger)
{
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    // ── Write ─────────────────────────────────────────────────────────────────

    /// <summary>Saves the screener results for a given origin ("Portfolio" or "Watchlist").</summary>
    public async Task SaveAsync(string origin, List<ValueScreenerResult> results, CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var json = JsonSerializer.Serialize(results, _json);
        var snapshot = new ValueScreenerSnapshot
        {
            Origin = origin,
            RunAt = DateTime.UtcNow,
            ResultsJson = json,
        };

        db.ValueScreenerSnapshots.Add(snapshot);

        // Keep only the latest 5 snapshots per origin to avoid unbounded growth
        var old = await db.ValueScreenerSnapshots
            .Where(s => s.Origin == origin)
            .OrderByDescending(s => s.RunAt)
            .Skip(4)
            .ToListAsync(ct);
        if (old.Count > 0) db.ValueScreenerSnapshots.RemoveRange(old);

        await db.SaveChangesAsync(ct);

        // Update the schedule config last-run timestamp
        var cfg = await db.ValueScreenerScheduleConfigs.FirstOrDefaultAsync(ct);
        if (cfg != null)
        {
            if (origin == "Portfolio") cfg.LastPortfolioRunAt = snapshot.RunAt;
            else cfg.LastWatchlistRunAt = snapshot.RunAt;
            await db.SaveChangesAsync(ct);
        }

        logger.LogInformation("[ValueScreenerPersistence] Saved {Count} results for {Origin}", results.Count, origin);
    }

    // ── Read ──────────────────────────────────────────────────────────────────

    /// <summary>Returns the latest persisted snapshot for Portfolio and Watchlist.</summary>
    public async Task<ValueScreenerLatestDto> GetLatestAsync(CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var portfolioSnap = await db.ValueScreenerSnapshots
            .Where(s => s.Origin == "Portfolio")
            .OrderByDescending(s => s.RunAt)
            .FirstOrDefaultAsync(ct);

        var watchlistSnap = await db.ValueScreenerSnapshots
            .Where(s => s.Origin == "Watchlist")
            .OrderByDescending(s => s.RunAt)
            .FirstOrDefaultAsync(ct);

        return new ValueScreenerLatestDto
        {
            Portfolio = Deserialize(portfolioSnap?.ResultsJson),
            PortfolioRunAt = portfolioSnap?.RunAt,
            Watchlist = Deserialize(watchlistSnap?.ResultsJson),
            WatchlistRunAt = watchlistSnap?.RunAt,
        };
    }

    /// <summary>Gets the current schedule configuration (creates default if none).</summary>
    public async Task<ValueScreenerScheduleConfig> GetOrCreateScheduleConfigAsync(CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cfg = await db.ValueScreenerScheduleConfigs.FirstOrDefaultAsync(ct);
        if (cfg is null)
        {
            cfg = new ValueScreenerScheduleConfig { ScheduledTimeEt = "17:00", Enabled = true };
            db.ValueScreenerScheduleConfigs.Add(cfg);
            await db.SaveChangesAsync(ct);
        }
        return cfg;
    }

    /// <summary>Updates the schedule configuration.</summary>
    public async Task UpdateScheduleConfigAsync(string scheduledTimeEt, bool enabled, CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cfg = await db.ValueScreenerScheduleConfigs.FirstOrDefaultAsync(ct)
            ?? new ValueScreenerScheduleConfig();

        cfg.ScheduledTimeEt = scheduledTimeEt;
        cfg.Enabled = enabled;

        if (db.Entry(cfg).State == Microsoft.EntityFrameworkCore.EntityState.Detached)
            db.ValueScreenerScheduleConfigs.Add(cfg);

        await db.SaveChangesAsync(ct);
    }

    private static List<ValueScreenerResult> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]") return [];
        try { return JsonSerializer.Deserialize<List<ValueScreenerResult>>(json, _json) ?? []; }
        catch { return []; }
    }

    /// <summary>Deletes all persisted snapshots for the given origin, or all if origin is null.</summary>
    public async Task ClearAsync(string? origin = null, CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var query = db.ValueScreenerSnapshots.AsQueryable();
        if (origin != null) query = query.Where(s => s.Origin == origin);

        var rows = await query.ToListAsync(ct);
        if (rows.Count > 0)
        {
            db.ValueScreenerSnapshots.RemoveRange(rows);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("[ValueScreenerPersistence] Cleared {Count} snapshot(s) for origin={Origin}", rows.Count, origin ?? "All");
        }

        // Reset last-run timestamps on the schedule config
        if (origin == null || origin == "Portfolio" || origin == "Watchlist")
        {
            var cfg = await db.ValueScreenerScheduleConfigs.FirstOrDefaultAsync(ct);
            if (cfg != null)
            {
                if (origin == null || origin == "Portfolio") cfg.LastPortfolioRunAt = null;
                if (origin == null || origin == "Watchlist") cfg.LastWatchlistRunAt = null;
                await db.SaveChangesAsync(ct);
            }
        }
    }
}
