using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using System.Security.Claims;

namespace PortfolioManager.Api.Services;

public interface IUnlinkedTradeService
{
    /// <summary>Trade legs (stock/option buys and sells on or after the ledger start date) that have neither a
    /// linked cash row nor a hand-entered look-alike, i.e. legs that still double-count in Portfolio Value.
    /// Legs acknowledged in the legacy baseline (and unchanged since) are not returned. Each result carries a
    /// diagnosis explaining why it was reported.</summary>
    Task<IReadOnlyList<UnlinkedTradeDto>> GetUnlinkedAsync(CancellationToken ct = default);

    /// <summary>Records every currently reported leg as acknowledged legacy history so it stops being displayed.
    /// Insert-only and idempotent; never modifies trades, cash or snapshots.</summary>
    Task<UnlinkedBaselineResultDto> BaselineCurrentAsync(CancellationToken ct = default);
}

public sealed class UnlinkedTradeService(
    AppDbContext db,
    IHttpContextAccessor httpCtx,
    ICashLedgerQueryService cashLedger,
    ILogger<UnlinkedTradeService>? logger = null) : IUnlinkedTradeService
{
    private const int OptionMultiplier = 100;
    private const int LookAlikeDayWindow = 3;
    private const int NearMissDayWindow = 10;
    private const decimal LookAlikeMinTolerance = 1m;
    private const decimal LookAlikeRelativeTolerance = 0.01m;
    private const decimal NearMissRelativeAmount = 0.5m;

    // The panel is re-queried after every ledger change; log each distinct finding once per process.
    private static readonly ConcurrentDictionary<string, bool> Logged = new();

    private sealed record Leg(
        string SourceType, int SourceItemId, string Symbol, string Label, string Quantity,
        decimal? Price, decimal? Amount, string? AccountType, DateTime TradeDate,
        DateTime RecordedAt, string? OwnerId)
    {
        public string Fingerprint => string.Join('|', Quantity,
            Price?.ToString("0.####", CultureInfo.InvariantCulture), Amount?.ToString("0.00", CultureInfo.InvariantCulture),
            AccountType, TradeDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }

    public async Task<IReadOnlyList<UnlinkedTradeDto>> GetUnlinkedAsync(CancellationToken ct = default)
    {
        var result = (await DetectAsync(ct)).Select(r => r.dto).ToList();
        foreach (var r in result)
        {
            var key = $"{r.SourceType}:{r.SourceItemId}:{r.Amount}:{r.TradeDate:yyyyMMdd}";
            if (Logged.TryAdd(key, true))
                logger?.LogWarning("Trade without cash entry: {Label} ({Qty}, {Account}, {Date:yyyy-MM-dd}) amount {Amount}. {Summary} {Details}",
                    r.Label, r.Quantity, r.AccountType, r.TradeDate, r.Amount, r.Diagnosis?.Summary,
                    r.Diagnosis is null ? "" : string.Join(" | ", r.Diagnosis.Details));
        }
        return result;
    }

    public async Task<UnlinkedBaselineResultDto> BaselineCurrentAsync(CancellationToken ct = default)
    {
        var current = await DetectAsync(ct);
        var existing = await db.UnlinkedTradeBaselines
            .ToDictionaryAsync(b => (b.SourceType, b.SourceItemId), ct);

        var added = 0;
        foreach (var leg in current.Select(c => c.leg))
        {
            if (existing.TryGetValue((leg.SourceType, leg.SourceItemId), out var row))
            {
                // Re-acknowledging an edited trade refreshes the stored fingerprint.
                row.Fingerprint = leg.Fingerprint;
                continue;
            }
            db.UnlinkedTradeBaselines.Add(new UnlinkedTradeBaseline
            {
                SourceType = leg.SourceType, SourceItemId = leg.SourceItemId, Symbol = leg.Symbol, Label = leg.Label,
                Amount = leg.Amount, AccountType = leg.AccountType, TradeDate = leg.TradeDate,
                Fingerprint = leg.Fingerprint, AcknowledgedAt = DateTime.UtcNow
            });
            added++;
        }
        await db.SaveChangesAsync(ct);
        return new UnlinkedBaselineResultDto(added, existing.Count);
    }

    private async Task<List<(UnlinkedTradeDto dto, Leg leg)>> DetectAsync(CancellationToken ct)
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
        var baseline = await db.UnlinkedTradeBaselines.AsNoTracking()
            .ToDictionaryAsync(b => (b.SourceType, b.SourceItemId), b => b.Fingerprint, ct);

        var legs = new List<Leg>();
        foreach (var s in stocks)
        {
            var isClosed = string.Equals(s.TransactionType, "CLOSE", StringComparison.OrdinalIgnoreCase);
            var qty = $"{s.Shares:0.####} sh";
            legs.Add(new Leg(TradeLinkSourceTypes.PortfolioOpen, s.Id, s.Symbol, $"Buy {s.Symbol}", qty,
                s.AverageCostBasis, Round2(s.Shares * s.AverageCostBasis), s.AccountType, (s.OpenDate ?? s.AddedAt).Date, s.AddedAt, s.UserId));
            if (isClosed)
                legs.Add(new Leg(TradeLinkSourceTypes.PortfolioClose, s.Id, s.Symbol, $"Sell {s.Symbol}", qty,
                    s.ClosingPrice, s.ClosingPrice is { } cp ? Round2(s.Shares * cp) : null, s.AccountType, (s.CloseDate ?? s.AddedAt).Date, s.AddedAt, s.UserId));
        }
        foreach (var o in options)
        {
            var isClosed = string.Equals(o.TransactionType, "CLOSE", StringComparison.OrdinalIgnoreCase);
            var name = $"{o.UnderlyingTicker} {o.PositionType} ${o.Strike:0.##}";
            var qty = $"{o.NumberOfContracts} contract{(o.NumberOfContracts == 1 ? "" : "s")}";
            legs.Add(new Leg(TradeLinkSourceTypes.OptionOpen, o.Id, o.UnderlyingTicker, $"Buy {name}", qty,
                o.Premium, Round2(o.NumberOfContracts * o.Premium * OptionMultiplier), o.AccountType, (o.OpenDate ?? o.AddedAt).Date, o.AddedAt, o.UserId));
            if (isClosed)
                legs.Add(new Leg(TradeLinkSourceTypes.OptionClose, o.Id, o.UnderlyingTicker, $"Sell {name}", qty,
                    o.ClosingPrice, o.ClosingPrice is { } cp ? Round2(o.NumberOfContracts * cp * OptionMultiplier) : null, o.AccountType, (o.CloseDate ?? o.AddedAt).Date, o.AddedAt, o.UserId));
        }

        var linked = cash.Where(c => c.SourceType != null && c.SourceItemId != null)
            .Select(c => (c.SourceType!, c.SourceItemId!.Value))
            .ToHashSet();
        // Each hand-entered row may vouch for only one trade leg.
        var freeRows = cash.Where(c => c.SourceType == null).ToList();
        var used = new HashSet<int>();

        var reported = new List<(Leg leg, string reason)>();
        foreach (var leg in legs.Where(l => l.TradeDate >= ledgerStart && !linked.Contains((l.SourceType, l.SourceItemId)))
                                .OrderBy(l => l.TradeDate).ThenBy(l => l.SourceItemId))
        {
            if (leg.Amount is { } amount)
            {
                // A worthless expiry / zero-price leg moves no cash, so there is nothing to link.
                if (amount == 0m) continue;

                var type = TradeLinkSourceTypes.CashFlowTypeFor(leg.SourceType);
                var tolerance = Tolerance(amount);
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

            reported.Add((leg, leg.Amount is null ? "MissingPrice" : "NoCash"));
        }

        var output = new List<(UnlinkedTradeDto, Leg)>();
        foreach (var (leg, reason) in reported.OrderByDescending(r => r.leg.TradeDate).ThenBy(r => r.leg.Symbol))
        {
            var changedSinceBaseline = false;
            if (baseline.TryGetValue((leg.SourceType, leg.SourceItemId), out var fingerprint))
            {
                if (fingerprint == leg.Fingerprint) continue;
                changedSinceBaseline = true;
            }

            var diagnosis = Diagnose(leg, reason, changedSinceBaseline, freeRows, used, linked, uid);
            output.Add((new UnlinkedTradeDto(
                leg.SourceType, leg.SourceItemId, leg.Symbol, leg.Label, leg.Quantity, leg.Price, leg.Amount,
                leg.AccountType, leg.TradeDate, reason, diagnosis), leg));
        }
        return output;
    }

    private static UnlinkedTradeDiagnosisDto Diagnose(
        Leg leg, string reason, bool changedSinceBaseline, List<CashItem> freeRows, HashSet<int> used,
        HashSet<(string, int)> linked, string? currentUserId)
    {
        var expectedType = TradeLinkSourceTypes.CashFlowTypeFor(leg.SourceType);
        decimal? expectedSigned = leg.Amount is { } a ? CashFlowTypeRules.DeriveSignedAmount(expectedType, a) : null;
        decimal? tolerance = leg.Amount is { } a2 ? Tolerance(a2) : null;
        var details = new List<string>();
        var isBuy = expectedType == CashFlowTypeRules.TradePurchase;

        details.Add($"{leg.Label}: {leg.Quantity}" + (leg.Price is { } p ? $" @ {p:0.####}" : "") +
                    $" in {leg.AccountType ?? "no account"} on {leg.TradeDate:yyyy-MM-dd}.");
        details.Add($"The position record was created {leg.RecordedAt:yyyy-MM-dd HH:mm} UTC" +
                    (leg.RecordedAt.Date > leg.TradeDate ? $", {(leg.RecordedAt.Date - leg.TradeDate).Days} day(s) after the trade date (back-dated entry)." : "."));
        details.Add($"Expected cash row: {expectedType}" + (expectedSigned is { } es ? $" of {es:+0.00;-0.00}" : " (amount unknown, price missing)") +
                    $" in {leg.AccountType ?? "no account"}, dated {leg.TradeDate:yyyy-MM-dd}.");
        details.Add("No cash row is linked to this trade leg (SourceType/SourceItemId), so the trade was saved without the cash dialog being completed (Skip, closed dialog, or the link request failed).");

        var otherSource = leg.SourceType switch
        {
            TradeLinkSourceTypes.PortfolioClose => TradeLinkSourceTypes.PortfolioOpen,
            TradeLinkSourceTypes.OptionClose => TradeLinkSourceTypes.OptionOpen,
            TradeLinkSourceTypes.PortfolioOpen => TradeLinkSourceTypes.PortfolioClose,
            _ => TradeLinkSourceTypes.OptionClose
        };
        if (linked.Contains((otherSource, leg.SourceItemId)))
            details.Add($"The {(isBuy ? "closing (sell)" : "opening (buy)")} leg of this same position does have a linked cash row, so only this leg is missing.");
        if (leg.OwnerId is null)
            details.Add("The trade has no owner (legacy/unowned record).");
        else if (currentUserId is not null && leg.OwnerId != currentUserId)
            details.Add("The trade belongs to another user (visible because you are an Admin).");
        if (changedSinceBaseline)
            details.Add("This trade was previously acknowledged as legacy, but it has been edited since (size, price, account or date changed), so it is shown again.");

        UnlinkedNearestCashDto? nearest = null;
        if (leg.Amount is { } amount)
        {
            var candidate = freeRows
                .Where(c => Math.Abs(((c.TransactionDate ?? c.AddedAt).Date - leg.TradeDate).TotalDays) <= NearMissDayWindow)
                .Select(c => new { Row = c, Diff = Math.Abs(Math.Abs(c.Amount) - amount) })
                .Where(x => x.Diff <= amount * NearMissRelativeAmount)
                .OrderBy(x => used.Contains(x.Row.Id)).ThenBy(x => x.Diff)
                .FirstOrDefault()?.Row;
            if (candidate is not null)
            {
                var mismatches = new List<string>();
                var rowDate = (candidate.TransactionDate ?? candidate.AddedAt).Date;
                if (!string.Equals(candidate.CashFlowType, expectedType, StringComparison.OrdinalIgnoreCase))
                    mismatches.Add($"type is {candidate.CashFlowType ?? "none"}, expected {expectedType}");
                if ((candidate.AccountType ?? null) != (leg.AccountType ?? null))
                    mismatches.Add($"account is {candidate.AccountType ?? "none"}, expected {leg.AccountType ?? "none"}");
                var diff = Math.Abs(Math.Abs(candidate.Amount) - amount);
                if (diff > tolerance)
                    mismatches.Add($"amount differs by {diff:0.00} (allowed {tolerance:0.00})");
                var days = (int)Math.Abs((rowDate - leg.TradeDate).TotalDays);
                if (days > LookAlikeDayWindow)
                    mismatches.Add($"dated {days} days from the trade (allowed {LookAlikeDayWindow})");
                if (used.Contains(candidate.Id))
                    mismatches.Add("already vouching for a different trade");
                if (mismatches.Count == 0)
                    mismatches.Add("matches every rule, but another trade claimed it first");
                nearest = new UnlinkedNearestCashDto(candidate.Id, candidate.Description, candidate.Amount,
                    candidate.AccountType, rowDate, candidate.CashFlowType, mismatches);
                details.Add($"Closest hand-entered cash row #{candidate.Id} ({candidate.Amount:+0.00;-0.00}, {candidate.AccountType ?? "no account"}, {rowDate:yyyy-MM-dd}) was NOT accepted: {string.Join("; ", mismatches)}.");
            }
            else
                details.Add($"No hand-entered cash row within {NearMissDayWindow} days of the trade has a comparable amount.");
        }
        else
            details.Add("The price is missing, so the cash amount cannot be computed. Enter the " + (isBuy ? "cost basis" : "closing price") + " on the trade, then link cash.");

        var summary = reason == "MissingPrice"
            ? $"{leg.Label} has no {(isBuy ? "price" : "closing price")}, so no cash entry could be sized or matched."
            : nearest is not null
                ? $"No cash entry is linked to {leg.Label}; the nearest cash row was rejected ({string.Join("; ", nearest.Mismatches)})."
                : $"No cash entry is linked to {leg.Label}, and no hand-entered cash row resembles it.";
        if (changedSinceBaseline) summary = "Edited after being acknowledged as legacy. " + summary;

        return new UnlinkedTradeDiagnosisDto(summary, leg.RecordedAt, expectedType, expectedSigned, tolerance, nearest, details);
    }

    private static decimal Tolerance(decimal amount) => Math.Max(LookAlikeMinTolerance, amount * LookAlikeRelativeTolerance);

    private static decimal Round2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
