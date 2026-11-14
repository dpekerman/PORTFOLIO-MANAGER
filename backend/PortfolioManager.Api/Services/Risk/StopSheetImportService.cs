using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services.Risk;

public interface IStopSheetImportService
{
    Task<StopSheetResultDto> ImportAsync(StopSheetRequest request, CancellationToken ct = default);
}

/// <summary>
/// One-time, auditable import of user-supplied broker stop records. Dry-run by default. Idempotent through
/// ImportKey (sheet:version:row); never overwrites an existing stop; never guesses an account or episode;
/// never infers covered quantity from holdings.
/// </summary>
public sealed class StopSheetImportService(
    AppDbContext db,
    IHttpContextAccessor httpCtx,
    IPortfolioSnapshotService? snapshots = null) : IStopSheetImportService
{
    private readonly RiskUserContext _user = new(httpCtx);

    public async Task<StopSheetResultDto> ImportAsync(StopSheetRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.SheetId) || string.IsNullOrWhiteSpace(request.SheetVersion))
            throw new RiskValidationException("SheetId and SheetVersion identify the import and are required.");
        if (!request.DryRun && !_user.IsAdmin) throw new RiskValidationException("Only an administrator can import stop records.");

        var correlation = Guid.NewGuid().ToString("N");
        var results = new List<StopSheetRowResult>();
        var staged = new List<(StopSheetRow Row, ResolvedPosition Pos, string Currency)>();

        foreach (var row in request.Rows)
        {
            var r = await EvaluateRowAsync(request, row, ct);
            results.Add(r.Result);
            if (r.Result.Action == StopSheetActions.Import) staged.Add((row, r.Pos!, r.Result.Currency!));
        }

        var blocked = results.Count(x => x.Action is StopSheetActions.Unmatched or StopSheetActions.Ambiguous
            or StopSheetActions.Conflict or StopSheetActions.Invalid);
        var skipped = results.Count(x => x.Action == StopSheetActions.AlreadyImported);
        var toImport = staged.Count;

        if (request.DryRun || blocked > 0 || toImport == 0)
            return new StopSheetResultDto(request.DryRun, false, correlation, toImport, skipped, blocked, results);

        foreach (var (row, pos, currency) in staged)
        {
            var rec = new BrokerStopRecord
            {
                ImportKey = ImportKey(request, row), UserId = pos.OpenLots[0].UserId, AccountType = pos.Account, Symbol = pos.Symbol,
                OrderType = NormalizeOrderType(row.OrderType), TriggerPrice = row.TriggerPrice, Currency = currency,
                UserStatus = BrokerUserStatuses.UserConfirmedEstablished,
                BrokerVerificationStatus = BrokerVerification.Unverified,
                CoveredQuantity = null,
                ConfirmationSource = ConfirmationSources.UserSuppliedStopSheet,
                TradeEpisodeId = pos.EpisodeId, EpisodeOpenDate = pos.EpisodeOpenDate,
                Notes = $"Initial user-supplied stop sheet {request.SheetId} v{request.SheetVersion}, row {row.RowNumber}.",
            };
            db.BrokerStopRecords.Add(rec);
            var changes = new List<AuditChangeDto>();
            RiskSupport.Diff(changes, "TriggerPrice", null, rec.TriggerPrice);
            RiskSupport.Diff(changes, "OrderType", null, rec.OrderType);
            RiskSupport.Diff(changes, "UserStatus", null, rec.UserStatus);
            RiskSupport.Diff(changes, "ConfirmationSource", null, rec.ConfirmationSource);
            RiskSupport.StageAudit(db, _user, RiskAuditSources.Import, "BrokerStopImported", pos.Account, pos.Symbol, changes,
                $"Initialization from user-supplied stop sheet {request.SheetId} v{request.SheetVersion}", correlation, stop: rec);
        }

        // One SaveChanges: every record and its audit event commit together or not at all.
        await db.SaveChangesAsync(ct);
        var applied = results.Select(x => x.Action == StopSheetActions.Import ? x with { Action = StopSheetActions.Imported } : x).ToList();
        return new StopSheetResultDto(false, true, correlation, toImport, skipped, 0, applied);
    }

    private static string ImportKey(StopSheetRequest req, StopSheetRow row) => $"{req.SheetId.Trim()}:{req.SheetVersion.Trim()}:{row.RowNumber}";

    private static string NormalizeOrderType(string? t) => string.IsNullOrWhiteSpace(t) ? BrokerOrderTypes.StopMarket : t.Trim();

    private async Task<string?> ProviderCurrencyAsync(string symbol, CancellationToken ct)
    {
        if (snapshots is null || _user.UserId is null) return null;
        var snap = await snapshots.GetLatestAsync(_user.UserId, ct);
        return snap?.FirstOrDefault(s => string.Equals(s.Item.Symbol, symbol, StringComparison.OrdinalIgnoreCase))?.Quote?.Currency;
    }

    private async Task<(StopSheetRowResult Result, ResolvedPosition? Pos)> EvaluateRowAsync(StopSheetRequest req, StopSheetRow row, CancellationToken ct)
    {
        var sym = TradeEpisodeResolver.NormSymbol(row.Symbol);
        StopSheetRowResult Fail(string action, string detail, ResolvedPosition? p = null, string? existing = null, string? cur = null, string? basis = null) =>
            new(row.RowNumber, sym, action, p?.Account, p?.SwingQuantity, p?.TotalQuantity, cur, basis,
                p?.Resolution, p?.EpisodeOpenDate, row.TriggerPrice, existing, detail);

        if (sym.Length == 0) return (Fail(StopSheetActions.Invalid, "Symbol is required."), null);
        if (row.TriggerPrice <= 0 || !RiskSupport.HasValidScale(row.TriggerPrice))
            return (Fail(StopSheetActions.Invalid, "Trigger must be a positive price with at most 4 decimals."), null);
        var orderType = NormalizeOrderType(row.OrderType);
        if (!BrokerOrderTypes.All.Contains(orderType)) return (Fail(StopSheetActions.Invalid, "Unsupported order type."), null);

        var lots = (await _user.OwnedLots(db).AsNoTracking().Where(x => x.Symbol == sym).ToListAsync(ct)).Select(LotRef.From).ToList();
        var groups = lots.Where(l => l.IsOpen && l.IsSwing)
            .GroupBy(l => (l.UserId, Account: TradeEpisodeResolver.NormAccount(l.AccountType).ToUpperInvariant())).ToList();
        if (!string.IsNullOrWhiteSpace(row.Account))
            groups = groups.Where(g => g.Key.Account == TradeEpisodeResolver.NormAccount(row.Account).ToUpperInvariant()).ToList();

        if (groups.Count == 0) return (Fail(StopSheetActions.Unmatched, "No open Swing position with this exact ticker."), null);
        if (groups.Count > 1)
            return (Fail(StopSheetActions.Ambiguous, $"Open Swing positions exist in several accounts ({string.Join(", ", groups.Select(g => g.First().Account))}); specify the account."), null);

        var account = groups[0].First().Account;
        var owner = groups[0].Key.UserId;
        var open = lots.Where(l => l.UserId == owner && l.IsOpen && TradeEpisodeResolver.SameScope(l, account, sym)).ToList();
        var ids = open.Where(l => l.TradeEpisodeId.HasValue).Select(l => l.TradeEpisodeId!.Value).Distinct().ToList();
        var episodes = ids.Count == 0 ? new Dictionary<Guid, TradeEpisode>()
            : await db.TradeEpisodes.AsNoTracking().Where(e => ids.Contains(e.Id)).ToDictionaryAsync(e => e.Id, ct);
        var pos = TradeEpisodeResolver.ResolveScope(lots, open, episodes);

        var (currency, basis) = TradingCurrency.Resolve(sym, await ProviderCurrencyAsync(sym, ct));
        if (!string.IsNullOrWhiteSpace(row.Currency) && !string.Equals(row.Currency.Trim(), currency, StringComparison.OrdinalIgnoreCase))
            return (Fail(StopSheetActions.Invalid, $"Sheet currency {row.Currency} does not match the security's trading currency {currency} ({basis}).", pos, null, currency, basis), pos);

        var key = ImportKey(req, row);
        var byKey = await db.BrokerStopRecords.AsNoTracking().FirstOrDefaultAsync(s => s.ImportKey == key, ct);
        if (byKey is not null)
        {
            if (byKey.Symbol != sym || byKey.AccountType != account ||
                (byKey.UserId != owner && !(owner == null && byKey.UserId == _user.UserId && byKey.TradeEpisodeId == pos.EpisodeId)))
                return (Fail(StopSheetActions.Conflict, "This import identity belongs to a different position. No data has been changed.", pos), pos);
            return byKey.TriggerPrice == row.TriggerPrice
                ? (Fail(StopSheetActions.AlreadyImported, "This sheet row was already imported; nothing changes.", pos, $"#{byKey.Id} @ {byKey.TriggerPrice:0.####}", currency, basis), pos)
                : (Fail(StopSheetActions.Conflict, $"This sheet row was imported with a different trigger ({byKey.TriggerPrice:0.####}); it is never updated silently.", pos, $"#{byKey.Id} @ {byKey.TriggerPrice:0.####}", currency, basis), pos);
        }

        var existing = (await db.BrokerStopRecords.AsNoTracking().Where(s => s.AccountType == account && s.Symbol == sym
                && (s.UserId == owner || (owner == null && s.UserId == _user.UserId && s.TradeEpisodeId == pos.EpisodeId))).ToListAsync(ct))
            .Where(s => BrokerUserStatuses.IsActive(s.UserStatus)).ToList();
        if (existing.Count > 0 && !req.AllowAdditionalOrders)
            return (Fail(StopSheetActions.Conflict, "An active broker stop is already recorded for this position; it is not overwritten. Set AllowAdditionalOrders only if this is a separate real order.", pos,
                string.Join("; ", existing.Select(e => $"#{e.Id} @ {e.TriggerPrice:0.####}")), currency, basis), pos);

        var detail = pos.Resolution == EpisodeResolutions.Unresolved
            ? $"Ready. Attaches to the current open position; historical episode is unresolved ({pos.ResolutionNote})"
            : "Ready.";
        return (Fail(StopSheetActions.Import, detail, pos, null, currency, basis), pos);
    }
}
