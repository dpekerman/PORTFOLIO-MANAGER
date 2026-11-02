using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services.Risk;

public sealed class RiskValidationException(string message) : Exception(message);
/// <summary>A well-formed request that conflicts with existing trade-episode integrity (HTTP 409).</summary>
public sealed class RiskConflictException(string message) : Exception(message);
public sealed class RiskNotFoundException(string message) : Exception(message);
public sealed class RiskConcurrencyException(string message) : Exception(message);

/// <summary>Current caller. Ownership mirrors PortfolioService.OwnedItems: admins see everything; others see their rows and legacy NULL-owner rows.</summary>
public sealed class RiskUserContext(IHttpContextAccessor httpCtx)
{
    public string? UserId => httpCtx.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
    public string? UserName => httpCtx.HttpContext?.User.Identity?.Name ?? httpCtx.HttpContext?.User.FindFirstValue(ClaimTypes.Name);
    public bool IsAdmin => httpCtx.HttpContext?.User.IsInRole("Admin") ?? false;

    public IQueryable<PortfolioItem> OwnedLots(AppDbContext db)
    {
        var q = db.PortfolioItems.AsQueryable().Where(x => !x.IsManual);
        if (IsAdmin) return q;
        var uid = UserId;
        return q.Where(x => x.UserId == uid || x.UserId == null);
    }
}

public static class TradingCurrency
{
    private static readonly string[] CadSuffixes = [".TO", ".V", ".CN", ".NE"];

    /// <summary>Provider-reported currency wins; the ticker suffix is only a labelled fallback.</summary>
    public static (string Code, string Basis) Resolve(string symbol, string? providerCurrency)
    {
        if (!string.IsNullOrWhiteSpace(providerCurrency))
            return (providerCurrency.Trim().ToUpperInvariant(), "Provider");
        var s = (symbol ?? string.Empty).Trim().ToUpperInvariant();
        return (CadSuffixes.Any(s.EndsWith) ? "CAD" : "USD", "AssumedFromSuffix");
    }
}

public static class RiskSupport
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string Version(byte[]? rv) => Convert.ToBase64String(rv ?? []);

    /// <summary>Optimistic concurrency that also works on providers that do not generate row versions.</summary>
    public static void CheckVersion(byte[]? current, string? supplied, string what)
    {
        if (string.IsNullOrWhiteSpace(supplied))
            throw new RiskValidationException($"{what} requires its current row version. Reload before saving.");
        Span<byte> decoded = stackalloc byte[64];
        if (!Convert.TryFromBase64String(supplied, decoded, out var length) || length == 0)
            throw new RiskValidationException($"{what} has an invalid row version. Reload before saving.");
        if (!string.Equals(Version(current), supplied, StringComparison.Ordinal))
            throw new RiskConcurrencyException($"{what} was changed by someone else. Reload and review before saving again.");
    }

    public static bool HasValidScale(decimal v) => decimal.Round(v, 4) == v;

    public static decimal RequirePrice(decimal? v, string field)
    {
        if (v is null or <= 0) throw new RiskValidationException($"{field} must be a positive price.");
        if (!HasValidScale(v.Value)) throw new RiskValidationException($"{field} supports at most 4 decimal places.");
        return v.Value;
    }

    public static string Fmt(object? v) => v switch
    {
        null => string.Empty,
        decimal d => d.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture),
        DateTime dt => dt.ToString("yyyy-MM-dd"),
        _ => v.ToString() ?? string.Empty,
    };

    public static void Diff(List<AuditChangeDto> changes, string field, object? oldValue, object? newValue)
    {
        var o = Fmt(oldValue); var n = Fmt(newValue);
        if (!string.Equals(o, n, StringComparison.Ordinal))
            changes.Add(new AuditChangeDto(field, o.Length == 0 ? null : o, n.Length == 0 ? null : n));
    }

    /// <summary>Stages the typed risk event and its AuditLog mirror in the caller's unit of work, so both commit or neither does.</summary>
    public static RiskAuditEvent StageAudit(AppDbContext db, RiskUserContext user, string source, string eventType,
        string account, string symbol, IReadOnlyList<AuditChangeDto> changes, string? reason, string? correlationId,
        RiskPlan? plan = null, BrokerStopRecord? stop = null, Guid? episodeId = null)
    {
        var json = changes.Count == 0 ? null : JsonSerializer.Serialize(changes, Json);
        var ev = new RiskAuditEvent
        {
            UserId = user.UserId, UserName = user.UserName, Source = source, EventType = eventType,
            Symbol = symbol, AccountType = account, ChangesJson = json, Reason = reason, CorrelationId = correlationId,
            RiskPlan = plan, BrokerStopRecord = stop, TradeEpisodeId = plan?.TradeEpisodeId ?? stop?.TradeEpisodeId ?? episodeId,
        };
        db.RiskAuditEvents.Add(ev);
        db.AuditLogs.Add(new AuditLog
        {
            UserId = user.UserId, UserName = user.UserName,
            Source = source == RiskAuditSources.User ? AuditSources.User : AuditSources.System,
            Category = AuditCategories.Risk,
            Action = eventType.Contains("Created") || eventType.Contains("Recorded") || eventType.Contains("Imported") ? AuditActions.Create : AuditActions.Update,
            EntityType = plan is not null ? nameof(RiskPlan) : stop is not null ? nameof(BrokerStopRecord) : nameof(PortfolioItem),
            Symbol = symbol, AccountType = account,
            Summary = $"{eventType} — {symbol} ({account})",
            ChangesJson = json, CorrelationId = correlationId,
        });
        return ev;
    }
}

/// <summary>Assigns and propagates TradeEpisodeId inside PortfolioService writes (same SaveChanges). Ambiguity leaves the id null.</summary>
public static class TradeEpisodeAssigner
{
    /// <summary>New OPEN lot: join the single open episode only when that relationship is established, else start a new one.</summary>
    public static async Task AssignOnOpenAsync(AppDbContext db, PortfolioItem item, CancellationToken ct)
    {
        if (item.IsManual || !string.Equals(item.TransactionType, "OPEN", StringComparison.OrdinalIgnoreCase) || item.Shares <= 0) return;
        var symbol = TradeEpisodeResolver.NormSymbol(item.Symbol);
        var account = item.AccountType ?? string.Empty;

        var open = await db.PortfolioItems.AsNoTracking()
            .Where(x => x.Id != item.Id && x.UserId == item.UserId && x.Symbol == symbol && x.AccountType == account
                        && x.HoldingRole == item.HoldingRole
                        && x.TransactionType == "OPEN" && x.Shares > 0)
            .Select(x => new { x.TradeEpisodeId, x.HoldingRole }).ToListAsync(ct);

        if (open.Count == 0)
        {
            var ep = new TradeEpisode
            {
                UserId = item.UserId, AccountType = account, Symbol = symbol,
                OpenDate = (item.OpenDate ?? DateTime.UtcNow).Date, Source = EpisodeSources.Assigned,
            };
            db.TradeEpisodes.Add(ep);
            item.TradeEpisodeId = ep.Id;
            return;
        }

        var ids = open.Select(o => o.TradeEpisodeId).Distinct().ToList();
        var roles = open.Select(o => (o.HoldingRole ?? string.Empty).ToUpperInvariant()).Append((item.HoldingRole ?? string.Empty).ToUpperInvariant()).Distinct().Count();
        if (ids.Count == 1 && ids[0].HasValue && roles == 1)
        {
            var episode = await db.TradeEpisodes.SingleOrDefaultAsync(e => e.Id == ids[0] && e.UserId == item.UserId
                && e.AccountType == account && e.Symbol == symbol && e.Status == EpisodeStatuses.Open, ct);
            if (episode is null)
                throw new RiskValidationException("The existing open lot has inconsistent episode history. Resolve it before adding a linked lot.");
            if (item.OpenDate?.Date < episode.OpenDate.Date)
                throw new RiskValidationException("An addition cannot predate the existing episode. Record or correct its history separately.");
            item.TradeEpisodeId = episode.Id;
        }
    }

    /// <summary>Partial close splits the remainder into a new lot: it keeps the episode of the lot it came from.</summary>
    public static void PropagateToRemainder(PortfolioItem closed, PortfolioItem remainder) => remainder.TradeEpisodeId = closed.TradeEpisodeId;

    /// <summary>
    /// Full exit of the last open lot in an account+symbol: closes the episode and retires its broker stop records
    /// WITHOUT claiming the broker cancelled them. A later reopen starts a new episode and never inherits them.
    /// </summary>
    public static async Task RetireOnFullExitAsync(AppDbContext db, PortfolioItem closedItem, RiskUserContext user, CancellationToken ct)
    {
        if (!string.Equals(closedItem.TransactionType, "CLOSE", StringComparison.OrdinalIgnoreCase)) return;
        var symbol = TradeEpisodeResolver.NormSymbol(closedItem.Symbol);
        var account = closedItem.AccountType ?? string.Empty;

        var stillOpen = await db.PortfolioItems.AnyAsync(x => x.Id != closedItem.Id && x.UserId == closedItem.UserId && x.Symbol == symbol
            && x.AccountType == account && x.TransactionType == "OPEN" && x.Shares > 0, ct);
        var episodeStillOpen = closedItem.TradeEpisodeId is { } episodeId
            && await db.PortfolioItems.AnyAsync(x => x.Id != closedItem.Id && x.UserId == closedItem.UserId
                && x.TradeEpisodeId == episodeId && x.TransactionType == "OPEN" && x.Shares > 0
                && x.HoldingRole == closedItem.HoldingRole, ct);

        if (!episodeStillOpen && closedItem.TradeEpisodeId is { } eid)
        {
            var ep = await db.TradeEpisodes.FirstOrDefaultAsync(e => e.Id == eid && e.UserId == closedItem.UserId, ct);
            if (ep is not null) { ep.Status = EpisodeStatuses.Closed; ep.ClosedDate = (closedItem.CloseDate ?? DateTime.UtcNow).Date; }
        }
        if (stillOpen) return;

        var stops = await db.BrokerStopRecords
            .Where(s => (s.UserId == closedItem.UserId ||
                        (closedItem.UserId == null && s.UserId == user.UserId && closedItem.TradeEpisodeId != null
                            && s.TradeEpisodeId == closedItem.TradeEpisodeId))
                        && s.Symbol == symbol && s.AccountType == account
                        && s.UserStatus != BrokerUserStatuses.UserReportedCancelled
                        && s.UserStatus != BrokerUserStatuses.UserReportedFilled
                        && s.UserStatus != BrokerUserStatuses.Superseded
                        && s.UserStatus != BrokerUserStatuses.EpisodeClosed)
            .ToListAsync(ct);
        foreach (var s in stops)
        {
            var changes = new List<AuditChangeDto>();
            RiskSupport.Diff(changes, "UserStatus", s.UserStatus, BrokerUserStatuses.EpisodeClosed);
            RiskSupport.Diff(changes, "BrokerCancellationUnverified", s.BrokerCancellationUnverified, true);
            s.UserStatus = BrokerUserStatuses.EpisodeClosed;
            s.BrokerCancellationUnverified = true;
            s.UpdatedAtUtc = DateTime.UtcNow;
            RiskSupport.StageAudit(db, user, RiskAuditSources.System, "EpisodeClosedBrokerOrderRetired", account, symbol, changes,
                "Trade fully closed in Portfolio Manager. The brokerage order may still be open — verify or cancel it at the broker.",
                null, stop: s);
        }
    }
}
