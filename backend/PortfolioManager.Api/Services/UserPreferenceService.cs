using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public interface IUserPreferenceService
{
    Task<Dictionary<string, string>> GetAllAsync(string userId, CancellationToken ct = default);
    Task UpsertAsync(string userId, string key, string value, CancellationToken ct = default);
    Task DeleteAsync(string userId, string key, CancellationToken ct = default);
}

public class UserPreferenceService(AppDbContext db) : IUserPreferenceService
{
    public async Task<Dictionary<string, string>> GetAllAsync(string userId, CancellationToken ct = default)
    {
        var rows = await db.UserPreferences
            .Where(p => p.UserId == userId)
            .AsNoTracking()
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.PreferenceKey, r => r.PreferenceValue);
    }

    public async Task UpsertAsync(string userId, string key, string value, CancellationToken ct = default)
    {
        var existing = await db.UserPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId && p.PreferenceKey == key, ct);

        if (existing is null)
        {
            db.UserPreferences.Add(new UserPreference
            {
                UserId = userId,
                PreferenceKey = key,
                PreferenceValue = value,
                UpdatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            existing.PreferenceValue = value;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(string userId, string key, CancellationToken ct = default)
    {
        var row = await db.UserPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId && p.PreferenceKey == key, ct);

        if (row is not null)
        {
            db.UserPreferences.Remove(row);
            await db.SaveChangesAsync(ct);
        }
    }
}
