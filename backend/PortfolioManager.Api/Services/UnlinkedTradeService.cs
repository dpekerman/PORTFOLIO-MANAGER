using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using System.Security.Claims;

namespace PortfolioManager.Api.Services;

public interface IUnlinkedTradeService
{
    /// <summary>Trade legs (stock/option buys and sells on or after the ledger start date) that have neither a
    /// linked cash row nor a hand-entered look-alike, i.e. legs that still double-count in Portfolio Value.</summary>
    Task<IReadOnlyList<UnlinkedTradeDto>> GetUnlinkedAsync(CancellationToken ct = default);
}

public sealed class UnlinkedTradeService(
    AppDbContext db,
    IHttpContextAccessor httpCtx,
    ICashLedgerQueryService cashLedger) : IUnlinkedTradeService
{
    private const int OptionMultiplier = 100;
    private const int LookAlikeDayWindow = 3;
    private const decimal LookAlikeMinTolerance = 1m;
    private const decimal LookAlikeRelativeTolerance = 0.01m;

    private sealed record Leg(
        string SourceType, int SourceItemId, string Symbol, string Label, string Quantity,
        decimal? Price, decimal? Amount, string? AccountType, DateTime TradeDate);

    public async Task<IReadOnlyList<UnlinkedTradeDto>> GetUnlinkedAsync(CancellationToken ct = default)
    {
        var uid = httpCtx.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var admin = httpCtx.HttpContext?.User.IsInRole("Admin") ?? false;
        var ledgerStart = (await cashLedger.GetLedgerStartDateAsync(ct)).ToDateTime(TimeOnly.MinValue);

        var stocks = await db.PortfolioItems.AsNoTracking()
            .Where(x => !x.IsManual && (admin || x.UserId == uid || x.UserId == null))
            .ToListAsync(ct);
        var options = await db.OptionItems.AsNoTracking()
            .Where(x => admin || x.UserId == uid || x.UserId == null)
            .ToListAsync(ct);
        var cash = await db.CashItems.AsNoTracking()
            .Where(c => admin || c.UserId == uid || c.UserId == null)
            .ToListAsync(ct);

        var legs = new List<Leg>();
        foreach (var s in stocks)
        {
            var isClosed = string.Equals(s.TransactionType, "CLOSE", StringComparison.OrdinalIgnoreCase);
            var qty = $"{s.Shares:0.####} sh";
            legs.Add(new Leg(TradeLinkSourceTypes.PortfolioOpen, s.Id, s.Symbol, $"Buy {s.Symbol}", qty,
                s.AverageCostBasis, Round2(s.Shares * s.AverageCostBasis), s.AccountType, (s.OpenDate ?? s.AddedAt).Date));
            if (isClosed)
                legs.Add(new Leg(TradeLinkSourceTypes.PortfolioClose, s.Id, s.Symbol, $"Sell {s.Symbol}", qty,
                    s.ClosingPrice, s.ClosingPrice is { } cp ? Round2(s.Shares * cp) : null, s.AccountType, (s.CloseDate ?? s.AddedAt).Date));
        }
        foreach (var o in options)
        {
            var isClosed = string.Equals(o.TransactionType, "CLOSE", StringComparison.OrdinalIgnoreCase);
            var name = $"{o.UnderlyingTicker} {o.PositionType} ${o.Strike:0.##}";
            var qty = $"{o.NumberOfContracts} contract{(o.NumberOfContracts == 1 ? "" : "s")}";
            legs.Add(new Leg(TradeLinkSourceTypes.OptionOpen, o.Id, o.UnderlyingTicker, $"Buy {name}", qty,
                o.Premium, Round2(o.NumberOfContracts * o.Premium * OptionMultiplier), o.AccountType, (o.OpenDate ?? o.AddedAt).Date));
            if (isClosed)
                legs.Add(new Leg(TradeLinkSourceTypes.OptionClose, o.Id, o.UnderlyingTicker, $"Sell {name}", qty,
                    o.ClosingPrice, o.ClosingPrice is { } cp ? Round2(o.NumberOfContracts * cp * OptionMultiplier) : null, o.AccountType, (o.CloseDate ?? o.AddedAt).Date));
        }

        var linked = cash.Where(c => c.SourceType != null && c.SourceItemId != null)
            .Select(c => (c.SourceType!, c.SourceItemId!.Value))
            .ToHashSet();
        // Each hand-entered row may vouch for only one trade leg.
        var freeRows = cash.Where(c => c.SourceType == null).ToList();
        var used = new HashSet<int>();

        var result = new List<UnlinkedTradeDto>();
        foreach (var leg in legs.Where(l => l.TradeDate >= ledgerStart && !linked.Contains((l.SourceType, l.SourceItemId)))
                                .OrderBy(l => l.TradeDate).ThenBy(l => l.SourceItemId))
        {
            if (leg.Amount is { } amount)
            {
                // A worthless expiry / zero-price leg moves no cash, so there is nothing to link.
                if (amount == 0m) continue;

                var type = TradeLinkSourceTypes.CashFlowTypeFor(leg.SourceType);
                var tolerance = Math.Max(LookAlikeMinTolerance, amount * LookAlikeRelativeTolerance);
                var match = freeRows.FirstOrDefault(c =>
                    !used.Contains(c.Id)
                    && string.Equals(c.CashFlowType, type, StringComparison.OrdinalIgnoreCase)
                    && (c.AccountType ?? null) == (leg.AccountType ?? null)
                    && Math.Abs(Math.Abs(c.Amount) - amount) <= tolerance
                    && Math.Abs(((c.TransactionDate ?? c.AddedAt).Date - leg.TradeDate).TotalDays) <= LookAlikeDayWindow);
                if (match is not null)
                {
                    used.Add(match.Id);
                    continue;
                }
            }

            result.Add(new UnlinkedTradeDto(
                leg.SourceType, leg.SourceItemId, leg.Symbol, leg.Label, leg.Quantity, leg.Price, leg.Amount,
                leg.AccountType, leg.TradeDate, leg.Amount is null ? "MissingPrice" : "NoCash"));
        }

        return result.OrderByDescending(r => r.TradeDate).ThenBy(r => r.Symbol).ToList();
    }

    private static decimal Round2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
