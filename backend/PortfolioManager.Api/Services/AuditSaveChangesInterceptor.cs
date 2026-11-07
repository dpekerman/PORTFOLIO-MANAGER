using System.Collections;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

/// <summary>
/// Records create/update/delete of whitelisted entities as AuditLog rows. Values are captured before the
/// save (original values are still available) and persisted after it succeeds so identity keys are known.
/// A failure here is logged and never fails the business save.
/// </summary>
public sealed class AuditSaveChangesInterceptor(
    IHttpContextAccessor httpCtx,
    ILogger<AuditSaveChangesInterceptor> logger) : SaveChangesInterceptor
{
    private sealed record Pending(EntityEntry Entry, string Action, string Type, List<AuditChangeDto> Changes);

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DbContext, List<Pending>> State = new();
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DbContext, object> Writing = new();

    private static readonly Dictionary<string, string> CategoryByType = new()
    {
        [nameof(PortfolioItem)] = AuditCategories.Stock,
        [nameof(OptionItem)] = AuditCategories.Option,
        [nameof(CashItem)] = AuditCategories.Cash,
        [nameof(CashLedgerSettings)] = AuditCategories.Cash,
        [nameof(UnlinkedTradeBaseline)] = AuditCategories.Cash,
        [nameof(WatchlistItem)] = AuditCategories.Watchlist,
        [nameof(MarketLeadershipTracker)] = AuditCategories.Watchlist,
        [nameof(SecurityAnalysisMapping)] = AuditCategories.Watchlist,
        [nameof(AllocationRiskTarget)] = AuditCategories.Allocation,
        [nameof(AllocationSectorTarget)] = AuditCategories.Allocation,
        [nameof(SinglePositionLimit)] = AuditCategories.Allocation,
        [nameof(AccountType)] = AuditCategories.Config,
        [nameof(SectorIndustryConfig)] = AuditCategories.Config,
        [nameof(ValueScreenerScheduleConfig)] = AuditCategories.Config,
        [nameof(ApplicationUser)] = AuditCategories.User,
    };

    // UI preferences (grid columns, filters) are not portfolio operations; timestamp columns are derived.
    private static readonly HashSet<string> IgnoredFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "PasswordHash", "SecurityStamp", "ConcurrencyStamp", "Version", "UserId", "TradeEpisodeId",
        "NormalizedName", "NormalizedUserName", "NormalizedEmail", "LockoutEnd", "AccessFailedCount",
        "UpdatedAt", "ModifiedAt", "LastModifiedAt",
    };

    private static readonly Dictionary<string, string> FriendlyNames = new()
    {
        [nameof(PortfolioItem)] = "stock",
        [nameof(OptionItem)] = "option",
        [nameof(CashItem)] = "cash entry",
        [nameof(WatchlistItem)] = "watchlist item",
        [nameof(AllocationRiskTarget)] = "risk target",
        [nameof(AllocationSectorTarget)] = "sector target",
        [nameof(SinglePositionLimit)] = "position limit",
        [nameof(AccountType)] = "account type",
        [nameof(SectorIndustryConfig)] = "sector/industry setting",
        [nameof(ApplicationUser)] = "user",
    };

    private static readonly string[] SymbolProps = ["Symbol", "UnderlyingTicker", "Ticker", "TradingTicker"];

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        Capture(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken ct = default)
    {
        var ctx = eventData.Context;
        if (ctx is not null && TakePending(ctx) is { Count: > 0 } pending)
        {
            try
            {
                var rows = Build(pending);
                Writing.Add(ctx, new object());
                try
                {
                    ctx.Set<AuditLog>().AddRange(rows);
                    await ctx.SaveChangesAsync(ct);
                }
                finally { Writing.Remove(ctx); }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Audit log write failed");
                DetachAudit(ctx);
            }
        }
        return result;
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        var ctx = eventData.Context;
        if (ctx is not null && TakePending(ctx) is { Count: > 0 } pending)
        {
            try
            {
                var rows = Build(pending);
                Writing.Add(ctx, new object());
                try
                {
                    ctx.Set<AuditLog>().AddRange(rows);
                    ctx.SaveChanges();
                }
                finally { Writing.Remove(ctx); }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Audit log write failed");
                DetachAudit(ctx);
            }
        }
        return result;
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        if (eventData.Context is { } ctx) TakePending(ctx);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken ct = default)
    {
        SaveChangesFailed(eventData);
        return Task.CompletedTask;
    }

    private static List<Pending>? TakePending(DbContext ctx)
    {
        if (!State.TryGetValue(ctx, out var list)) return null;
        State.Remove(ctx);
        return list;
    }

    private static void DetachAudit(DbContext ctx)
    {
        foreach (var e in ctx.ChangeTracker.Entries<AuditLog>().ToList())
            e.State = EntityState.Detached;
    }

    private void Capture(DbContext? ctx)
    {
        if (ctx is null || Writing.TryGetValue(ctx, out _)) return;
        try
        {
            var list = new List<Pending>();
            foreach (var entry in ctx.ChangeTracker.Entries())
            {
                var type = entry.Metadata.ClrType.Name;
                if (!CategoryByType.ContainsKey(type)) continue;
                var action = entry.State switch
                {
                    EntityState.Added => AuditActions.Create,
                    EntityState.Modified => AuditActions.Update,
                    EntityState.Deleted => AuditActions.Delete,
                    _ => null,
                };
                if (action is null) continue;

                var changes = new List<AuditChangeDto>();
                foreach (var p in entry.Properties)
                {
                    var name = p.Metadata.Name;
                    if (IgnoredFields.Contains(name) || p.Metadata.IsPrimaryKey()) continue;
                    if (action == AuditActions.Update)
                    {
                        if (!p.IsModified) continue;
                        var o = Format(p.OriginalValue);
                        var n = Format(p.CurrentValue);
                        if (o == n) continue;
                        changes.Add(new AuditChangeDto(name, o, n));
                    }
                    else if (action == AuditActions.Create)
                    {
                        var n = Format(p.CurrentValue);
                        if (n is not null) changes.Add(new AuditChangeDto(name, null, n));
                    }
                    else
                    {
                        var o = Format(p.OriginalValue);
                        if (o is not null) changes.Add(new AuditChangeDto(name, o, null));
                    }
                }
                if (action == AuditActions.Update && changes.Count == 0) continue;
                list.Add(new Pending(entry, action, type, changes));
            }
            if (list.Count > 0) State.AddOrUpdate(ctx, list);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Audit capture failed");
        }
    }

    private List<AuditLog> Build(List<Pending> pending)
    {
        var http = httpCtx.HttpContext;
        var userId = http?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = http?.User.FindFirstValue(ClaimTypes.Name) ?? http?.User.FindFirstValue(ClaimTypes.Email);
        var isUser = !string.IsNullOrEmpty(userId);
        var correlation = http?.TraceIdentifier;
        var now = DateTime.UtcNow;

        return pending.Select(p =>
        {
            var entity = p.Entry.Entity;
            var symbol = SymbolProps.Select(n => Read(p.Entry, n)).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
            var account = Read(p.Entry, "AccountType");
            var id = string.Join(",", p.Entry.Metadata.FindPrimaryKey()!.Properties
                .Select(k => Format(p.Entry.Property(k.Name).CurrentValue ?? p.Entry.Property(k.Name).OriginalValue)));
            var label = symbol ?? p.Type;
            return new AuditLog
            {
                OccurredAtUtc = now,
                UserId = isUser ? userId : null,
                UserName = isUser ? userName : null,
                Source = isUser ? AuditSources.User : AuditSources.System,
                Category = CategoryByType[p.Type],
                Action = p.Action,
                EntityType = p.Type,
                EntityId = Truncate(id, 64),
                Symbol = Truncate(symbol, 30),
                AccountType = Truncate(account, 120),
                Summary = Truncate(Summarize(label, account, p), 500)!,
                ChangesJson = JsonSerializer.Serialize(p.Changes),
                CorrelationId = Truncate(correlation, 64),
            };
        }).ToList();
    }

    private static string Summarize(string label, string? account, Pending p)
    {
        var parts = p.Changes
            .Where(c => c.Field is not ("Id" or "AddedAt" or "CreatedAt"))
            .Take(6)
            .Select(c => p.Action switch
            {
                AuditActions.Update => $"{c.Field} {c.Old ?? "∅"} → {c.New ?? "∅"}",
                AuditActions.Create => $"{c.Field} {c.New}",
                _ => $"{c.Field} {c.Old}",
            });
        var verb = p.Action switch { AuditActions.Create => "Added", AuditActions.Delete => "Deleted", _ => "Updated" };
        var body = string.Join(", ", parts);
        var friendly = FriendlyNames.GetValueOrDefault(p.Type, p.Type);
        var subject = label == p.Type ? friendly : $"{friendly} {label}";
        return p.Action == AuditActions.Update
            ? $"{(label == p.Type ? friendly : label)}{(account is null ? "" : $" ({account})")}: {body}"
            : $"{verb} {subject}{(account is null ? "" : $" ({account})")}: {body}";
    }

    private static string? Read(EntityEntry entry, string name)
    {
        var prop = entry.Metadata.FindProperty(name);
        if (prop is null) return null;
        var pe = entry.Property(name);
        return Format(entry.State == EntityState.Deleted ? pe.OriginalValue : pe.CurrentValue);
    }

    private static string? Format(object? v) => v switch
    {
        null => null,
        DateTime d => d.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        decimal m => m.ToString("0.####", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        IEnumerable and not string => "[collection]",
        _ => v.ToString(),
    };

    private static string? Truncate(string? s, int max) =>
        s is null || s.Length <= max ? s : s[..max];
}
