using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services.Risk;

public interface IRiskService
{
    Task<RiskPositionDto> GetPositionAsync(string account, string symbol, CancellationToken ct = default);
    Task<RiskPositionDto> SavePlanAsync(SaveRiskPlanRequest request, CancellationToken ct = default);
    Task<RiskPositionDto> SaveBrokerStopAsync(SaveBrokerStopRequest request, CancellationToken ct = default);
    Task<RiskPositionDto> TransitionBrokerStopAsync(int id, BrokerStopTransitionRequest request, CancellationToken ct = default);
    Task<BackfillReportDto> BackfillEpisodesAsync(bool dryRun, CancellationToken ct = default);
}

public sealed class RiskService(
    AppDbContext db,
    IHttpContextAccessor httpCtx,
    IPortfolioSnapshotService? snapshots = null) : IRiskService
{
    private readonly RiskUserContext _user = new(httpCtx);

    private IQueryable<T> Mine<T>(IQueryable<T> q) where T : class
    {
        if (_user.IsAdmin) return q;
        var uid = _user.UserId;
        return typeof(T) == typeof(RiskPlan)
            ? (IQueryable<T>)((IQueryable<RiskPlan>)q).Where(x => x.UserId == uid || x.UserId == null)
            : typeof(T) == typeof(BrokerStopRecord)
                ? (IQueryable<T>)((IQueryable<BrokerStopRecord>)q).Where(x => x.UserId == uid || x.UserId == null)
                : q;
    }

    internal async Task<(IReadOnlyList<LotRef> All, ResolvedPosition? Position)> LoadPositionAsync(string account, string symbol, CancellationToken ct)
    {
        var sym = TradeEpisodeResolver.NormSymbol(symbol);
        var lots = (await _user.OwnedLots(db).AsNoTracking().Where(x => x.Symbol == sym).ToListAsync(ct))
            .Select(LotRef.From).ToList();
        var open = lots.Where(l => l.IsOpen && TradeEpisodeResolver.SameScope(l, account, sym)).ToList();
        if (open.Count == 0) return (lots, null);

        var ids = open.Where(l => l.TradeEpisodeId.HasValue).Select(l => l.TradeEpisodeId!.Value).Distinct().ToList();
        var episodes = ids.Count == 0 ? new Dictionary<Guid, TradeEpisode>()
            : await db.TradeEpisodes.AsNoTracking().Where(e => ids.Contains(e.Id)).ToDictionaryAsync(e => e.Id, ct);
        return (lots, TradeEpisodeResolver.ResolveScope(lots, open, episodes));
    }

    /// <summary>A record applies to the current trade only when its episode matches; a reopened trade never inherits older orders.</summary>
    internal static bool BelongsToEpisode(Guid? recordEpisodeId, DateTime? recordOpenDate, ResolvedPosition? pos)
    {
        if (pos is null) return false;
        if (recordEpisodeId is not null) return pos.EpisodeId == recordEpisodeId;
        if (pos.EpisodeId is not null) return false;
        return recordOpenDate is not null && pos.EpisodeOpenDate is not null
            && recordOpenDate.Value.Date == pos.EpisodeOpenDate.Value.Date;
    }

    private async Task<string?> ProviderCurrencyAsync(string symbol, CancellationToken ct)
    {
        if (snapshots is null || _user.UserId is null) return null;
        var snap = await snapshots.GetLatestAsync(_user.UserId, ct);
        return snap?.FirstOrDefault(s => string.Equals(s.Item.Symbol, symbol, StringComparison.OrdinalIgnoreCase))?.Quote?.Currency;
    }

    public async Task<RiskPositionDto> GetPositionAsync(string account, string symbol, CancellationToken ct = default)
    {
        account = TradeEpisodeResolver.NormAccount(account);
        var sym = TradeEpisodeResolver.NormSymbol(symbol);
        var (_, pos) = await LoadPositionAsync(account, sym, ct);
        account = pos?.Account ?? account;
        var (currency, basis) = TradingCurrency.Resolve(sym, await ProviderCurrencyAsync(sym, ct));

        var plans = await Mine(db.RiskPlans.AsNoTracking()).Where(p => p.AccountType == account && p.Symbol == sym)
            .OrderByDescending(p => p.UpdatedAtUtc).ToListAsync(ct);
        if (pos is not null) plans = plans.Where(p => p.UserId == pos.OpenLots[0].UserId).ToList();
        var plan = plans.FirstOrDefault(p => BelongsToEpisode(p.TradeEpisodeId, p.EpisodeOpenDate, pos))
            ?? plans.FirstOrDefault();
        var stops = await Mine(db.BrokerStopRecords.AsNoTracking()).Where(s => s.AccountType == account && s.Symbol == sym)
            .OrderByDescending(s => s.CreatedAtUtc).ToListAsync(ct);
        if (pos is not null)
            stops = stops.Where(s => s.UserId == pos.OpenLots[0].UserId ||
                (pos.OpenLots[0].UserId == null && s.UserId == _user.UserId && s.TradeEpisodeId == pos.EpisodeId)).ToList();
        var planIds = plans.Select(p => p.Id).ToList();
        var stopIds = stops.Select(s => s.Id).ToList();
        var events = await db.RiskAuditEvents.AsNoTracking().Where(e =>
                ((e.AccountType == account && e.Symbol == sym) ||
                    (e.RiskPlanId.HasValue && planIds.Contains(e.RiskPlanId.Value)) ||
                    (e.BrokerStopRecordId.HasValue && stopIds.Contains(e.BrokerStopRecordId.Value)))
                && (_user.IsAdmin || e.UserId == _user.UserId || e.UserId == null))
            .OrderByDescending(e => e.OccurredAtUtc).ThenByDescending(e => e.Id).Take(100).ToListAsync(ct);

        var snapshot = snapshots is null || _user.UserId is null ? null : await snapshots.GetLatestAsync(_user.UserId, ct);
        var quote = snapshot?.FirstOrDefault(s => string.Equals(s.Item.Symbol, sym, StringComparison.OrdinalIgnoreCase))?.Quote;
        var evidence = quote is null ? PriceEvidence.Unavailable("no snapshot quote") :
            PriceEvidence.FromQuote(quote.CurrentPrice, quote.Timestamp, quote.MarketState, sym, DateTime.UtcNow, quote.SourcePriceTimestamp);
        decimal? average = pos?.TotalQuantity > 0 ? pos.OpenLots.Sum(l => l.Shares * l.AverageCostBasis) / pos.TotalQuantity : null;
        decimal? swingAverage = pos?.SwingQuantity > 0 ?
            pos.OpenLots.Where(l => l.IsSwing).Sum(l => l.Shares * l.AverageCostBasis) / pos.SwingQuantity : null;
        var pnl = (evidence.Price - average) * pos?.TotalQuantity;
        return new RiskPositionDto(account, sym, pos is not null, pos?.TotalQuantity ?? 0, pos?.SwingQuantity ?? 0,
            pos?.MixedRole ?? false, pos?.Resolution ?? EpisodeResolutions.Unresolved,
            pos is null ? "No open position in this account." : pos.ResolutionNote, pos?.EpisodeOpenDate, currency, basis,
            plan is null ? null : ToDto(plan, pos), stops.Select(s => ToDto(s, pos)).ToList(), events.Select(ToDto).ToList())
        {
            Facts = new(average, swingAverage, evidence.Price, evidence.Basis, evidence.AsOfUtc,
                evidence.Price * pos?.TotalQuantity, pnl,
                average is > 0 ? (evidence.Price - average) / average * 100 : null,
                evidence.Price * pos?.SwingQuantity, (evidence.Price - swingAverage) * pos?.SwingQuantity),
        };
    }

    public async Task<RiskPositionDto> SavePlanAsync(SaveRiskPlanRequest r, CancellationToken ct = default)
    {
        await using var tx = db.Database.IsRelational() && db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct) : null;
        var sym = TradeEpisodeResolver.NormSymbol(r.Symbol);
        var (_, pos) = await LoadPositionAsync(r.AccountType, sym, ct);
        if (pos is null) throw new RiskNotFoundException("No open position in this account for that symbol.");
        var account = pos.Account;

        decimal? stop = r.PlannedStopPrice is null ? null : RiskSupport.RequirePrice(r.PlannedStopPrice, "Planned stop");
        decimal? target = r.TargetPrice is null ? null : RiskSupport.RequirePrice(r.TargetPrice, "Target price");
        var stopType = string.IsNullOrWhiteSpace(r.StopType) ? StopTypes.Fixed : r.StopType.Trim();
        if (stopType is not (StopTypes.Fixed or StopTypes.Trailing)) throw new RiskValidationException("Stop type must be Fixed or Trailing.");
        if (r.StopRationale?.Length > 1000 || r.TradeRationale?.Length > 1000 || r.RiskNotes?.Length > 2000)
            throw new RiskValidationException("A text field is too long.");

        var (currency, _) = TradingCurrency.Resolve(sym, await ProviderCurrencyAsync(sym, ct));
        var owner = pos.OpenLots[0].UserId;
        var plan = await db.RiskPlans.FirstOrDefaultAsync(p => p.AccountType == account && p.Symbol == sym
            && p.UserId == owner && p.TradeEpisodeId == pos.EpisodeId
            && (pos.EpisodeId != null || p.EpisodeOpenDate == pos.EpisodeOpenDate), ct);

        var changes = new List<AuditChangeDto>();
        var creating = plan is null;
        if (creating)
        {
            plan = new RiskPlan { UserId = owner, AccountType = account, Symbol = sym };
            db.RiskPlans.Add(plan);
        }
        else
        {
            RiskSupport.CheckVersion(plan!.RowVersion, r.RowVersion, "The risk plan");
            if (r.RowVersion is not null) db.Entry(plan).Property(p => p.RowVersion).OriginalValue = plan.RowVersion;
        }

        RiskSupport.Diff(changes, "PlannedStopPrice", plan!.PlannedStopPrice, stop);
        RiskSupport.Diff(changes, "TargetPrice", plan.TargetPrice, target);
        RiskSupport.Diff(changes, "StopType", creating ? null : plan.StopType, stopType);
        RiskSupport.Diff(changes, "StopRationale", plan.StopRationale, r.StopRationale);
        RiskSupport.Diff(changes, "TradeRationale", plan.TradeRationale, r.TradeRationale);
        RiskSupport.Diff(changes, "RiskNotes", plan.RiskNotes, r.RiskNotes);
        if (!creating && changes.Count == 0) return await GetPositionAsync(account, sym, ct);

        plan.PlannedStopPrice = stop; plan.TargetPrice = target; plan.StopType = stopType;
        plan.StopRationale = r.StopRationale; plan.TradeRationale = r.TradeRationale; plan.RiskNotes = r.RiskNotes;
        plan.Currency = currency; plan.TradeEpisodeId = pos.EpisodeId; plan.EpisodeOpenDate = pos.EpisodeOpenDate;
        plan.UpdatedAtUtc = DateTime.UtcNow;

        RiskSupport.StageAudit(db, _user, RiskAuditSources.User, creating ? "PlanCreated" : "PlanUpdated",
            account, sym, changes, r.Reason, null, plan: plan);
        await SaveAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return await GetPositionAsync(account, sym, ct);
    }

    public async Task<RiskPositionDto> SaveBrokerStopAsync(SaveBrokerStopRequest r, CancellationToken ct = default)
    {
        await using var tx = db.Database.IsRelational() && db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct) : null;
        var sym = TradeEpisodeResolver.NormSymbol(r.Symbol);
        var (_, pos) = await LoadPositionAsync(r.AccountType, sym, ct);
        if (pos is null) throw new RiskNotFoundException("No open position in this account for that symbol.");
        var account = pos.Account;

        var trigger = RiskSupport.RequirePrice(r.TriggerPrice, "Trigger price");
        var orderType = string.IsNullOrWhiteSpace(r.OrderType) ? BrokerOrderTypes.StopMarket : r.OrderType.Trim();
        if (!BrokerOrderTypes.All.Contains(orderType)) throw new RiskValidationException("Unsupported order type.");
        decimal? limit = r.LimitPrice is null ? null : RiskSupport.RequirePrice(r.LimitPrice, "Limit price");
        if (r.CoveredQuantity is <= 0) throw new RiskValidationException("Covered quantity must be positive, or left blank when unknown.");
        if (r.CoveredQuantity > pos.TotalQuantity && !(r.AllowOverCoverage && !string.IsNullOrWhiteSpace(r.Reason)))
            throw new RiskValidationException($"Covered quantity exceeds the current account quantity ({pos.TotalQuantity:0.######}). Confirm an unusual case with a reason.");
        if (r.PlacedDate is { } pd && pd.Date > DateTime.UtcNow.Date.AddDays(1)) throw new RiskValidationException("Placed date cannot be in the future.");
        if (r.BrokerOrderRef?.Length > 100 || r.Notes?.Length > 1000) throw new RiskValidationException("A text field is too long.");

        var (currency, _) = TradingCurrency.Resolve(sym, await ProviderCurrencyAsync(sym, ct));
        var changes = new List<AuditChangeDto>();
        BrokerStopRecord rec;
        string eventType;
        if (r.Id is null)
        {
            rec = new BrokerStopRecord
            {
                UserId = pos.OpenLots[0].UserId, AccountType = account, Symbol = sym, Currency = currency,
                TradeEpisodeId = pos.EpisodeId, EpisodeOpenDate = pos.EpisodeOpenDate,
                ConfirmationSource = ConfirmationSources.ManualEntry,
            };
            db.BrokerStopRecords.Add(rec);
            eventType = "BrokerStopRecorded";
            RiskSupport.Diff(changes, "TriggerPrice", null, trigger);
            RiskSupport.Diff(changes, "OrderType", null, orderType);
            RiskSupport.Diff(changes, "CoveredQuantity", null, r.CoveredQuantity);
        }
        else
        {
            rec = await Mine(db.BrokerStopRecords).FirstOrDefaultAsync(s => s.Id == r.Id && s.AccountType == account && s.Symbol == sym, ct)
                ?? throw new RiskNotFoundException("Broker stop record not found.");
            if (!BrokerUserStatuses.IsActive(rec.UserStatus))
                throw new RiskValidationException("This broker stop is no longer active and cannot be edited.");
            if (!BelongsToEpisode(rec.TradeEpisodeId, rec.EpisodeOpenDate, pos))
                throw new RiskValidationException("This broker stop belongs to a previous trade episode.");
            if (rec.UserId != pos.OpenLots[0].UserId &&
                !(pos.OpenLots[0].UserId == null && rec.UserId == _user.UserId && rec.TradeEpisodeId == pos.EpisodeId))
                throw new RiskValidationException("The broker stop and position have different owners.");
            RiskSupport.CheckVersion(rec.RowVersion, r.RowVersion, "The broker stop");
            if (r.RowVersion is not null) db.Entry(rec).Property(p => p.RowVersion).OriginalValue = rec.RowVersion;
            RiskSupport.Diff(changes, "TriggerPrice", rec.TriggerPrice, trigger);
            RiskSupport.Diff(changes, "OrderType", rec.OrderType, orderType);
            RiskSupport.Diff(changes, "LimitPrice", rec.LimitPrice, limit);
            RiskSupport.Diff(changes, "CoveredQuantity", rec.CoveredQuantity, r.CoveredQuantity);
            RiskSupport.Diff(changes, "PlacedDate", rec.PlacedDate, r.PlacedDate);
            RiskSupport.Diff(changes, "BrokerOrderRef", rec.BrokerOrderRef, r.BrokerOrderRef);
            RiskSupport.Diff(changes, "LastUserVerifiedDate", rec.LastUserVerifiedDate, r.LastUserVerifiedDate);
            RiskSupport.Diff(changes, "Notes", rec.Notes, r.Notes);
            if (changes.Count == 0) return await GetPositionAsync(account, sym, ct);
            eventType = changes.All(c => c.Field == "CoveredQuantity") ? "CoverageAdjusted" : "BrokerStopUpdated";
        }

        rec.OrderType = orderType; rec.TriggerPrice = trigger; rec.LimitPrice = limit; rec.CoveredQuantity = r.CoveredQuantity;
        rec.PlacedDate = r.PlacedDate; rec.BrokerOrderRef = r.BrokerOrderRef; rec.LastUserVerifiedDate = r.LastUserVerifiedDate;
        rec.Notes = r.Notes; rec.UpdatedAtUtc = DateTime.UtcNow;

        RiskSupport.StageAudit(db, _user, RiskAuditSources.User, eventType, account, sym, changes, r.Reason, null, stop: rec);
        await SaveAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return await GetPositionAsync(account, sym, ct);
    }

    public async Task<RiskPositionDto> TransitionBrokerStopAsync(int id, BrokerStopTransitionRequest r, CancellationToken ct = default)
    {
        var rec = await Mine(db.BrokerStopRecords).FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new RiskNotFoundException("Broker stop record not found.");
        if (r.Status is not (BrokerUserStatuses.UserReportedCancelled or BrokerUserStatuses.UserReportedFilled or BrokerUserStatuses.Superseded))
            throw new RiskValidationException("Status must be UserReportedCancelled, UserReportedFilled or Superseded.");
        if (!BrokerUserStatuses.IsActive(rec.UserStatus) && !(rec.UserStatus == BrokerUserStatuses.EpisodeClosed && rec.BrokerCancellationUnverified))
            throw new RiskValidationException("This broker stop is already inactive.");
        if (string.IsNullOrWhiteSpace(r.Reason))
            throw new RiskValidationException("A reason describing the broker confirmation is required.");
        RiskSupport.CheckVersion(rec.RowVersion, r.RowVersion, "The broker stop");
        if (r.RowVersion is not null) db.Entry(rec).Property(p => p.RowVersion).OriginalValue = rec.RowVersion;

        if (r.Status == BrokerUserStatuses.Superseded)
        {
            var replacement = r.ReplacedByRecordId is null ? null
                : await Mine(db.BrokerStopRecords).FirstOrDefaultAsync(s => s.Id == r.ReplacedByRecordId
                    && s.UserId == rec.UserId && s.AccountType == rec.AccountType && s.Symbol == rec.Symbol, ct);
            if (replacement is null || replacement.Id == rec.Id) throw new RiskValidationException("A replacement record in the same account and symbol is required.");
        }

        var changes = new List<AuditChangeDto>();
        RiskSupport.Diff(changes, "UserStatus", rec.UserStatus, r.Status);
        RiskSupport.Diff(changes, "BrokerCancellationUnverified", rec.BrokerCancellationUnverified, false);
        rec.UserStatus = r.Status;
        rec.ReplacedByRecordId = r.Status == BrokerUserStatuses.Superseded ? r.ReplacedByRecordId : null;
        rec.BrokerCancellationUnverified = false;
        rec.UpdatedAtUtc = DateTime.UtcNow;

        RiskSupport.StageAudit(db, _user, RiskAuditSources.User, "BrokerStatusChanged", rec.AccountType, rec.Symbol, changes, r.Reason, null, stop: rec);
        await SaveAsync(ct);
        return await GetPositionAsync(rec.AccountType, rec.Symbol, ct);
    }

    /// <summary>Report only unless dryRun is false. Links history only where the relationship is unambiguous.</summary>
    public async Task<BackfillReportDto> BackfillEpisodesAsync(bool dryRun, CancellationToken ct = default)
    {
        if (!_user.IsAdmin) throw new RiskValidationException("Only an administrator can plan or apply episode backfill.");
        var all = (await db.PortfolioItems.AsNoTracking().Where(x => !x.IsManual).ToListAsync(ct)).Select(LotRef.From).ToList();
        var plan = TradeEpisodeResolver.PlanBackfill(all);

        if (!dryRun)
        {
            foreach (var e in plan.Episodes)
                db.TradeEpisodes.Add(new TradeEpisode
                {
                    Id = e.Id, UserId = e.UserId, AccountType = e.Account, Symbol = e.Symbol, OpenDate = e.OpenDate,
                    ClosedDate = e.ClosedDate, Status = e.Status, Source = EpisodeSources.Backfilled,
                });
            var byItem = plan.Assignments.ToDictionary(a => a.ItemId, a => a.EpisodeId);
            foreach (var item in await db.PortfolioItems.Where(x => byItem.Keys.Contains(x.Id)).ToListAsync(ct))
                item.TradeEpisodeId = byItem[item.Id];
            await db.SaveChangesAsync(ct);
        }
        return new BackfillReportDto(!dryRun, plan.Episodes.Count, plan.Assignments.Count, plan.Unresolved.Count,
            plan.Episodes, plan.Assignments, plan.Unresolved);
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            throw new RiskConcurrencyException("The record was changed by someone else. Reload and review before saving again.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
        {
            throw new RiskConcurrencyException("A risk record was created concurrently. Reload and review before saving again.");
        }
    }

    internal static RiskPlanDto ToDto(RiskPlan p, ResolvedPosition? pos) => new(p.Id, p.AccountType, p.Symbol, p.PlannedStopPrice,
        p.TargetPrice, p.StopType, p.StopRationale, p.TradeRationale, p.RiskNotes, p.Currency, p.EpisodeOpenDate,
        BelongsToEpisode(p.TradeEpisodeId, p.EpisodeOpenDate, pos), p.UpdatedAtUtc, RiskSupport.Version(p.RowVersion));

    internal static BrokerStopDto ToDto(BrokerStopRecord s, ResolvedPosition? pos) => new(s.Id, s.OrderIdentity, s.OrderType,
        s.TriggerPrice, s.LimitPrice, s.Currency, s.UserStatus, s.BrokerVerificationStatus, s.CoveredQuantity, s.PlacedDate,
        s.BrokerOrderRef, s.LastUserVerifiedDate, s.ConfirmationSource, s.Notes, s.EpisodeOpenDate,
        BrokerUserStatuses.IsActive(s.UserStatus), BelongsToEpisode(s.TradeEpisodeId, s.EpisodeOpenDate, pos),
        s.BrokerCancellationUnverified, s.ReplacedByRecordId, s.UpdatedAtUtc, RiskSupport.Version(s.RowVersion));

    private static RiskAuditEventDto ToDto(RiskAuditEvent e)
    {
        IReadOnlyList<AuditChangeDto> changes = string.IsNullOrEmpty(e.ChangesJson)
            ? []
            : System.Text.Json.JsonSerializer.Deserialize<List<AuditChangeDto>>(e.ChangesJson, RiskSupport.Json) ?? [];
        return new RiskAuditEventDto(e.Id, e.OccurredAtUtc, e.UserName, e.Source, e.EventType, changes, e.Reason, e.BrokerStopRecordId);
    }
}
