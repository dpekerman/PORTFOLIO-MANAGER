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
    /// <summary>Records an explicit event (automation run, backup, restore...). Never throws.</summary>
    Task LogAsync(string category, string action, string summary, string? entityType = null,
        string? symbol = null, CancellationToken ct = default);
}

public sealed class AuditService(AppDbContext db, IHttpContextAccessor httpCtx, ILogger<AuditService> logger) : IAuditService
{
    private const int MaxPageSize = 200;

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
        q = ((IOrderedQueryable<AuditLog>)q).ThenByDescending(x => x.Id);

        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, MaxPageSize);
        var total = await q.CountAsync(ct);
        var rows = await q.Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new AuditPagedResponse(rows.Select(ToDto).ToList(), total, page, size);
    }

    public async Task<AuditLogDto?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var row = await Scoped().FirstOrDefaultAsync(x => x.Id == id, ct);
        return row is null ? null : ToDto(row);
    }

    public async Task<AuditFiltersDto> GetFiltersAsync(CancellationToken ct = default)
    {
        var users = IsAdmin()
            ? (await db.AuditLogs.AsNoTracking().Where(x => x.UserId != null)
                .Select(x => new { x.UserId, x.UserName }).Distinct().ToListAsync(ct))
                .GroupBy(x => x.UserId!)
                .Select(g => new AuditUserDto(g.Key, g.Select(x => x.UserName).FirstOrDefault(n => n != null) ?? g.Key))
                .OrderBy(u => u.UserName).ToList()
            : new List<AuditUserDto>();
        return new AuditFiltersDto(
            [AuditCategories.Stock, AuditCategories.Option, AuditCategories.Cash, AuditCategories.Watchlist,
             AuditCategories.Allocation, AuditCategories.Config, AuditCategories.User, AuditCategories.Automation],
            [AuditActions.Create, AuditActions.Update, AuditActions.Delete, AuditActions.Restore,
             AuditActions.Adjust, AuditActions.Other],
            users);
    }

    public async Task LogAsync(string category, string action, string summary, string? entityType = null,
        string? symbol = null, CancellationToken ct = default)
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
                CorrelationId = http?.TraceIdentifier,
            });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Explicit audit log failed");
        }
    }

    private static AuditLogDto ToDto(AuditLog x)
    {
        IReadOnlyList<AuditChangeDto> changes = [];
        if (!string.IsNullOrEmpty(x.ChangesJson))
        {
            try { changes = JsonSerializer.Deserialize<List<AuditChangeDto>>(x.ChangesJson) ?? []; }
            catch (JsonException) { }
        }
        return new AuditLogDto(x.Id, x.OccurredAtUtc, x.UserId, x.UserName, x.Source, x.Category, x.Action,
            x.EntityType, x.EntityId, x.Symbol, x.AccountType, x.Summary, changes, x.CorrelationId);
    }
}
