using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public interface IAuditService
{
    Task<AuditPagedResponse> QueryAsync(AuditQuery query, CancellationToken ct = default);
    Task<AuditLogDto?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<AuditFiltersDto> GetFiltersAsync(CancellationToken ct = default);
    /// <summary>Admin-only removal of one audit entry; the removal itself is recorded.</summary>
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
    /// <summary>Admin-only removal of several entries (max 500); recorded as a single entry. Returns the count removed.</summary>
    Task<int> DeleteManyAsync(IReadOnlyList<long> ids, CancellationToken ct = default);
    /// <summary>CSV of the filtered list (max 10,000 rows), scoped like the list itself.</summary>
    Task<string> ExportCsvAsync(AuditQuery query, CancellationToken ct = default);
    /// <summary>Records an explicit event (automation run, backup, restore...). Never throws.</summary>
    Task LogAsync(string category, string action, string summary, string? entityType = null,
        string? symbol = null, CancellationToken ct = default, string? changesJson = null);
}

public sealed class AuditService(AppDbContext db, IHttpContextAccessor httpCtx, ILogger<AuditService> logger) : IAuditService
{
    private const int MaxPageSize = 200;
    private const int MaxExportRows = 10000;
    private const int MaxBatchDelete = 500;

    private string? CurrentUserId() => httpCtx.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
    private bool IsAdmin() => httpCtx.HttpContext?.User.IsInRole("Admin") == true;

    private IQueryable<AuditLog> Scoped()
    {
        var q = db.AuditLogs.AsNoTracking();
        if (IsAdmin()) return q;
        var uid = CurrentUserId();
        return q.Where(x => x.UserId == uid);
    }

    public async Task<AuditPagedResponse> QueryAsync(AuditQuery query, CancellationToken ct = default)
    {
        var q = Ordered(Filtered(query), query);
        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, MaxPageSize);
        var total = await q.CountAsync(ct);
        var rows = await q.Skip((page - 1) * size).Take(size).ToListAsync(ct);
        var names = await DisplayNamesAsync(rows.Select(r => r.UserId), ct);
        return new AuditPagedResponse(rows.Select(r => ToDto(r, names)).ToList(), total, page, size);
    }

    public async Task<string> ExportCsvAsync(AuditQuery query, CancellationToken ct = default)
    {
        var rows = await Ordered(Filtered(query), query).Take(MaxExportRows).ToListAsync(ct);
        var names = await DisplayNamesAsync(rows.Select(r => r.UserId), ct);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Time (UTC),User,Source,Category,Action,Symbol,Account,Summary,Changes");
        foreach (var r in rows)
        {
            var user = r.UserId is not null && names.TryGetValue(r.UserId, out var n) ? n : r.UserName;
            var changes = string.Join("; ", ToDto(r, names).Changes.Select(c => $"{c.Field}: {c.Old ?? "∅"} -> {c.New ?? "∅"}"));
            sb.AppendLine(string.Join(",", new[]
            {
                r.OccurredAtUtc.ToString("yyyy-MM-dd HH:mm:ss"), user, r.Source, r.Category, r.Action,
                r.Symbol, r.AccountType, r.Summary, changes,
            }.Select(Csv)));
        }
        return sb.ToString();
    }

    /// <summary>Quotes a CSV cell and neutralises spreadsheet formulas (=, +, -, @).</summary>
    private static string Csv(string? value)
    {
        var v = value ?? string.Empty;
        if (v.Length > 0 && "=+-@".Contains(v[0])) v = "'" + v;
        return "\"" + v.Replace("\"", "\"\"") + "\"";
    }

    private IQueryable<AuditLog> Filtered(AuditQuery query)
    {
        var q = Scoped();
        if (query.From is { } from) q = q.Where(x => x.OccurredAtUtc >= from);
        if (query.To is { } to) q = q.Where(x => x.OccurredAtUtc <= to);
        if (query.Category is { Length: > 0 } cats) q = q.Where(x => cats.Contains(x.Category));
        if (query.Action is { Length: > 0 } acts) q = q.Where(x => acts.Contains(x.Action));
        if (!string.IsNullOrWhiteSpace(query.Source)) q = q.Where(x => x.Source == query.Source);
        if (IsAdmin() && !string.IsNullOrWhiteSpace(query.UserId)) q = q.Where(x => x.UserId == query.UserId);
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var term = query.Q.Trim();
            q = q.Where(x => (x.Symbol != null && x.Symbol.Contains(term)) || x.Summary.Contains(term)
                || (x.AccountType != null && x.AccountType.Contains(term)));
        }
        return q;
    }

    private static IOrderedQueryable<AuditLog> Ordered(IQueryable<AuditLog> q, AuditQuery query)
    {
        var desc = !string.Equals(query.SortDir, "asc", StringComparison.OrdinalIgnoreCase);
        q = (query.SortBy?.ToLowerInvariant()) switch
        {
            "user" => desc ? q.OrderByDescending(x => x.UserName) : q.OrderBy(x => x.UserName),
            "category" => desc ? q.OrderByDescending(x => x.Category) : q.OrderBy(x => x.Category),
            "action" => desc ? q.OrderByDescending(x => x.Action) : q.OrderBy(x => x.Action),
            "symbol" => desc ? q.OrderByDescending(x => x.Symbol) : q.OrderBy(x => x.Symbol),
            "account" => desc ? q.OrderByDescending(x => x.AccountType) : q.OrderBy(x => x.AccountType),
            "source" => desc ? q.OrderByDescending(x => x.Source) : q.OrderBy(x => x.Source),
            _ => desc ? q.OrderByDescending(x => x.OccurredAtUtc) : q.OrderBy(x => x.OccurredAtUtc),
        };
        return ((IOrderedQueryable<AuditLog>)q).ThenByDescending(x => x.Id);
    }

    /// <summary>Current display names for the given users (a renamed user shows the new name).</summary>
    private async Task<Dictionary<string, string>> DisplayNamesAsync(IEnumerable<string?> userIds, CancellationToken ct)
    {
        var ids = userIds.Where(i => !string.IsNullOrEmpty(i)).Select(i => i!).Distinct().ToList();
        if (ids.Count == 0) return [];
        return await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
    }

    public async Task<AuditLogDto?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var row = await Scoped().FirstOrDefaultAsync(x => x.Id == id, ct);
        return row is null ? null : ToDto(row, await DisplayNamesAsync([row.UserId], ct));
    }

    public async Task<AuditFiltersDto> GetFiltersAsync(CancellationToken ct = default)
    {
        var users = new List<AuditUserDto>();
        if (IsAdmin())
        {
            var ids = await db.AuditLogs.AsNoTracking().Where(x => x.UserId != null)
                .Select(x => x.UserId!).Distinct().ToListAsync(ct);
            var names = await DisplayNamesAsync(ids, ct);
            users = ids.Select(i => new AuditUserDto(i, names.GetValueOrDefault(i, i))).OrderBy(u => u.UserName).ToList();
        }
        return new AuditFiltersDto(
            [AuditCategories.Stock, AuditCategories.Option, AuditCategories.Cash, AuditCategories.Watchlist,
             AuditCategories.Allocation, AuditCategories.Config, AuditCategories.User, AuditCategories.Automation],
            [AuditActions.Create, AuditActions.Update, AuditActions.Delete, AuditActions.Restore,
             AuditActions.Adjust, AuditActions.Other],
            users);
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        if (!IsAdmin()) return false;
        var row = await db.AuditLogs.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null) return false;
        db.AuditLogs.Remove(row);
        await db.SaveChangesAsync(ct);
        await LogAsync(AuditCategories.Config, AuditActions.Delete,
            $"Audit entry #{id} deleted ({row.Category} {row.Action}{(row.Symbol is null ? "" : " " + row.Symbol)})",
            nameof(AuditLog), ct: ct);
        return true;
    }

    public async Task<int> DeleteManyAsync(IReadOnlyList<long> ids, CancellationToken ct = default)
    {
        if (!IsAdmin() || ids.Count == 0) return 0;
        var batch = ids.Distinct().Take(MaxBatchDelete).ToList();
        var rows = await db.AuditLogs.Where(x => batch.Contains(x.Id)).ToListAsync(ct);
        if (rows.Count == 0) return 0;
        db.AuditLogs.RemoveRange(rows);
        await db.SaveChangesAsync(ct);
        await LogAsync(AuditCategories.Config, AuditActions.Delete,
            $"{rows.Count} audit entries deleted", nameof(AuditLog), ct: ct);
        return rows.Count;
    }

    public async Task LogAsync(string category, string action, string summary, string? entityType = null,
        string? symbol = null, CancellationToken ct = default, string? changesJson = null)
    {
        try
        {
            var http = httpCtx.HttpContext;
            var uid = http?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            db.AuditLogs.Add(new AuditLog
            {
                UserId = string.IsNullOrEmpty(uid) ? null : uid,
                UserName = string.IsNullOrEmpty(uid) ? null : http?.User.FindFirstValue(ClaimTypes.Name),
                Source = string.IsNullOrEmpty(uid) ? AuditSources.System : AuditSources.User,
                Category = category,
                Action = action,
                EntityType = entityType ?? category,
                Symbol = symbol,
                Summary = summary.Length > 500 ? summary[..500] : summary,
                ChangesJson = changesJson,
                CorrelationId = http?.TraceIdentifier,
            });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Explicit audit log failed");
        }
    }

    private static AuditLogDto ToDto(AuditLog x, IReadOnlyDictionary<string, string> names)
    {
        IReadOnlyList<AuditChangeDto> changes = [];
        if (!string.IsNullOrEmpty(x.ChangesJson))
        {
            try { changes = JsonSerializer.Deserialize<List<AuditChangeDto>>(x.ChangesJson) ?? []; }
            catch (JsonException) { }
        }
        var userName = x.UserId is not null && names.TryGetValue(x.UserId, out var current) ? current : x.UserName;
        return new AuditLogDto(x.Id, x.OccurredAtUtc, x.UserId, userName, x.Source, x.Category, x.Action,
            x.EntityType, x.EntityId, x.Symbol, x.AccountType, x.Summary, changes, x.CorrelationId);
    }
}
