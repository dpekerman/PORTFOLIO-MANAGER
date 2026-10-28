using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services.Risk;

public sealed record SwingTradeRowDto(
    string Key, string Symbol, string CompanyName, string Account, int AnchorItemId, IReadOnlyList<int> LotIds,
    string? DecisionSource, DateTime? EpisodeOpenDate, DateTime? FirstPurchaseDate, DateTime? LastExecutionDate, int? DaysHeld,
    string EpisodeResolution, string? EpisodeNote,
    decimal SwingQuantity, decimal TotalQuantity, bool MixedRole, decimal AverageCost,
    string Currency, string CurrencyBasis,
    decimal? CurrentPrice, DateTime? PriceAsOfUtc, DateOnly? PriceSessionDate, string PriceBasis, string PriceSource,
    decimal? MarketValue, decimal? UnrealizedPnl, decimal? UnrealizedPnlPct,
    decimal? PlannedStop, decimal? TargetPrice, string? StopType, bool PlanFromPriorEpisode,
    decimal? BrokerStopTrigger, string? BrokerOrderType, string? BrokerStatus, string? BrokerVerification,
    IReadOnlyList<BrokerStopDto> BrokerStops, decimal? CoveredQuantity, string CoverageState,
    string Protection, IReadOnlyList<string> RiskStatuses, string Severity, int SeverityRank, bool NeedsAttention,
    decimal? DistanceAbs, decimal? DistancePct, string? GoverningStopSource, string? Observation);

public sealed record CurrencyTotalDto(string Currency, decimal MarketValue, decimal UnrealizedPnl, decimal UnrealizedPnlPct);

public sealed record OpenTradesSummaryDto(
    int OpenTrades, int NeedsAttention, IReadOnlyList<CurrencyTotalDto> Totals, DateTime? AsOfUtc, bool AnyStale, bool AnyUnavailable);

public sealed record OpenTradesResponse(
    OpenTradesSummaryDto Summary, IReadOnlyList<SwingTradeRowDto> Rows, decimal ApproachingThresholdPct, string ThresholdNote);

public sealed record JournalHistoryItemDto(DateTime OccurredAtUtc, string EventType, string Summary);

public sealed record JournalRowDto(
    string Key, string Symbol, string Account, Guid? EpisodeId, string EpisodeResolution,
    DateTime? OpenDate, DateTime? FinalCloseDate, int? DaysHeld, decimal ClosedQuantity,
    decimal? EntryPrice, decimal? ExitPrice, decimal? RealizedPnl, decimal? RealizedPnlPct, string Currency,
    string? OriginalDecisionSource, string? ClosingDecisionSource, string? Thesis, string? Notes,
    string Outcome, bool EpisodeStillOpen, IReadOnlyList<JournalHistoryItemDto> RiskHistory, IReadOnlyList<int> LotIds);

public interface ISwingTradingService
{
    Task<OpenTradesResponse> GetOpenTradesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<JournalRowDto>> GetJournalAsync(CancellationToken ct = default);
}

public sealed class SwingTradingService(
    AppDbContext db,
    IHttpContextAccessor httpCtx,
    IConfiguration config,
    IPortfolioSnapshotService? snapshots = null,
    TimeProvider? clock = null) : ISwingTradingService
{
    public const string ThresholdNote = "Advisory alert threshold in Portfolio Manager only. It is not a broker order level and does not place or change any order.";
    private readonly RiskUserContext _user = new(httpCtx);
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    private IQueryable<T> Mine<T>(IQueryable<T> q) where T : class
    {
        if (_user.IsAdmin) return q;
        var uid = _user.UserId;
        return typeof(T) == typeof(RiskPlan)
            ? (IQueryable<T>)((IQueryable<RiskPlan>)q).Where(x => x.UserId == uid)
            : typeof(T) == typeof(BrokerStopRecord)
                ? (IQueryable<T>)((IQueryable<BrokerStopRecord>)q).Where(x => x.UserId == uid)
                : q;
    }

    private async Task<Dictionary<string, StockQuote>> QuotesAsync(CancellationToken ct)
    {
        var map = new Dictionary<string, StockQuote>(StringComparer.OrdinalIgnoreCase);
        if (snapshots is null || _user.UserId is null) return map;
        try
        {
            var snap = await snapshots.GetLatestAsync(_user.UserId, ct);
            foreach (var s in snap ?? [])
                if (s.Quote is not null) map[s.Item.Symbol] = s.Quote;
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { /* no snapshot: evidence becomes Unavailable */ }
        return map;
    }

    public async Task<OpenTradesResponse> GetOpenTradesAsync(CancellationToken ct = default)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var threshold = config.GetValue("Risk:ApproachingStopThresholdPct", StopRiskEvaluator.DefaultThresholdPct);

        var lots = await _user.OwnedLots(db).AsNoTracking().ToListAsync(ct);
        var refs = lots.Select(LotRef.From).ToList();
        var epIds = refs.Where(l => l.IsOpen && l.TradeEpisodeId.HasValue).Select(l => l.TradeEpisodeId!.Value).Distinct().ToList();
        var episodes = epIds.Count == 0 ? new Dictionary<Guid, TradeEpisode>()
            : await db.TradeEpisodes.AsNoTracking().Where(e => epIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, ct);
        var positions = TradeEpisodeResolver.ResolveOpenSwingPositions(refs, episodes);

        var plans = await Mine(db.RiskPlans.AsNoTracking()).ToListAsync(ct);
        var stops = await Mine(db.BrokerStopRecords.AsNoTracking()).ToListAsync(ct);
        var quotes = await QuotesAsync(ct);
        var rows = new List<SwingTradeRowDto>();

        foreach (var pos in positions)
        {
            var swingLots = pos.OpenLots.Where(l => l.IsSwing).ToList();
            quotes.TryGetValue(pos.Symbol, out var quote);
            var evidence = quote is null ? PriceEvidence.Unavailable("no quote in portfolio snapshot")
                : PriceEvidence.FromQuote(quote.CurrentPrice, quote.Timestamp, quote.MarketState, pos.Symbol, now);
            var (currency, basis) = TradingCurrency.Resolve(pos.Symbol, quote?.Currency);

            var plan = plans.Where(p => p.AccountType == pos.Account && p.Symbol == pos.Symbol)
                .OrderByDescending(p => p.UpdatedAtUtc).FirstOrDefault();
            var planCurrent = plan is not null && RiskService.BelongsToEpisode(plan.TradeEpisodeId, plan.EpisodeOpenDate, pos);
            var active = stops.Where(s => s.AccountType == pos.Account && s.Symbol == pos.Symbol
                    && BrokerUserStatuses.IsActive(s.UserStatus) && RiskService.BelongsToEpisode(s.TradeEpisodeId, s.EpisodeOpenDate, pos))
                .OrderByDescending(s => s.TriggerPrice).ToList();

            var eval = StopRiskEvaluator.Evaluate(new RiskEvalInput(evidence, planCurrent ? plan!.PlannedStopPrice : null,
                active.Select(s => new StopLite(s.Id, s.TriggerPrice, s.CoveredQuantity, s.BrokerVerificationStatus)).ToList(),
                pos.SwingQuantity, pos.TotalQuantity, threshold));

            var avg = swingLots.Sum(l => l.AverageCostBasis * l.Shares) / pos.SwingQuantity;
            decimal? price = evidence.Price is > 0 ? evidence.Price : null;
            decimal? mv = price is null ? null : Math.Round(price.Value * pos.SwingQuantity, 2);
            decimal? pnl = price is null ? null : Math.Round((price.Value - avg) * pos.SwingQuantity, 2);
            decimal? pnlPct = price is null || avg <= 0 ? null : Math.Round((price.Value - avg) / avg * 100m, 2);
            var top = active.FirstOrDefault();
            var anchor = swingLots.OrderBy(l => l.OpenDate ?? DateTime.MaxValue).ThenBy(l => l.Id).First();
            var company = lots.FirstOrDefault(l => l.Id == anchor.Id)?.CompanyName ?? pos.Symbol;

            rows.Add(new SwingTradeRowDto(
                $"{pos.Account}|{pos.Symbol}", pos.Symbol, company, pos.Account, anchor.Id, swingLots.Select(l => l.Id).ToList(),
                anchor.DecisionSource, pos.EpisodeOpenDate, pos.FirstPurchaseDate, pos.LastExecutionDate,
                pos.EpisodeOpenDate is null ? null : TradeEpisodeResolver.DaysHeld(pos.EpisodeOpenDate, now),
                pos.Resolution, pos.ResolutionNote, pos.SwingQuantity, pos.TotalQuantity, pos.MixedRole, Math.Round(avg, 4),
                currency, basis, price, evidence.AsOfUtc, evidence.SessionDate, evidence.Basis, evidence.Source, mv, pnl, pnlPct,
                planCurrent ? plan!.PlannedStopPrice : null, planCurrent ? plan!.TargetPrice : null, planCurrent ? plan!.StopType : null,
                plan is not null && !planCurrent,
                top?.TriggerPrice, top?.OrderType, top?.UserStatus, top?.BrokerVerificationStatus,
                active.Select(s => RiskService.ToDto(s, pos)).ToList(), eval.CoveredQuantity, eval.CoverageState,
                eval.Protection, eval.Statuses, eval.Severity, eval.Rank, eval.NeedsAttention,
                eval.DistanceAbs, eval.DistancePct, eval.GoverningStopSource, eval.Observation));
        }

        var ordered = rows.OrderBy(r => r.SeverityRank).ThenBy(r => r.DistancePct ?? decimal.MaxValue).ThenBy(r => r.Symbol).ToList();
        var totals = ordered.Where(r => r.MarketValue is not null).GroupBy(r => r.Currency).Select(g =>
        {
            var mv = g.Sum(r => r.MarketValue!.Value);
            var pnl = g.Sum(r => r.UnrealizedPnl ?? 0);
            var cost = mv - pnl;
            return new CurrencyTotalDto(g.Key, mv, pnl, cost <= 0 ? 0 : Math.Round(pnl / cost * 100m, 2));
        }).ToList();
        var asOf = ordered.Where(r => r.PriceAsOfUtc.HasValue).Select(r => r.PriceAsOfUtc).Min();

        return new OpenTradesResponse(new OpenTradesSummaryDto(ordered.Count, ordered.Count(r => r.NeedsAttention), totals, asOf,
            ordered.Any(r => r.PriceBasis == PriceBases.Stale), ordered.Any(r => r.PriceBasis == PriceBases.Unavailable)),
            ordered, threshold, ThresholdNote);
    }

    public async Task<IReadOnlyList<JournalRowDto>> GetJournalAsync(CancellationToken ct = default)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var lots = (await _user.OwnedLots(db).AsNoTracking().ToListAsync(ct)).Select(LotRef.From).ToList();
        var closes = lots.Where(l => l.IsClose && l.IsSwing).ToList();
        var quotes = await QuotesAsync(ct);
        var plans = await Mine(db.RiskPlans.AsNoTracking()).ToListAsync(ct);
        var events = await db.RiskAuditEvents.AsNoTracking().OrderBy(e => e.OccurredAtUtc).ToListAsync(ct);
        var episodeIds = closes.Where(c => c.TradeEpisodeId.HasValue).Select(c => c.TradeEpisodeId!.Value).Distinct().ToList();
        var episodes = episodeIds.Count == 0 ? new Dictionary<Guid, TradeEpisode>()
            : await db.TradeEpisodes.AsNoTracking().Where(e => episodeIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, ct);

        var groups = new List<(Guid? Id, List<LotRef> Rows)>();
        foreach (var g in closes.Where(c => c.TradeEpisodeId.HasValue).GroupBy(c => c.TradeEpisodeId!.Value))
            groups.Add((g.Key, g.ToList()));
        // Without an episode id a CLOSE record is never merged with another: each stays its own trade.
        foreach (var c in closes.Where(c => c.TradeEpisodeId is null)) groups.Add((null, [c]));

        var result = new List<JournalRowDto>();
        foreach (var (id, rows) in groups)
        {
            var first = rows.OrderBy(r => r.OpenDate ?? DateTime.MaxValue).ThenBy(r => r.Id).First();
            var symbol = TradeEpisodeResolver.NormSymbol(first.Symbol);
            var account = TradeEpisodeResolver.NormAccount(first.AccountType);
            var stillOpen = id.HasValue && lots.Any(l => l.IsOpen && l.TradeEpisodeId == id);
            episodes.TryGetValue(id ?? Guid.Empty, out var ep);

            var open = ep?.OpenDate.Date ?? rows.Where(r => r.OpenDate.HasValue).Select(r => (DateTime?)r.OpenDate!.Value.Date).Min();
            var lastClose = rows.Where(r => r.CloseDate.HasValue).Select(r => (DateTime?)r.CloseDate!.Value.Date).Max();
            var qty = rows.Sum(r => r.Shares);
            var cost = rows.Sum(r => r.AverageCostBasis * r.Shares);
            var priced = rows.All(r => r.ClosingPrice.HasValue);
            decimal? pnl = priced ? Math.Round(rows.Sum(r => (r.ClosingPrice!.Value - r.AverageCostBasis) * r.Shares), 2) : null;
            decimal? pct = pnl is null || cost <= 0 ? null : Math.Round(pnl.Value / cost * 100m, 2);
            DateTime? endForDays = stillOpen ? now : lastClose;
            int? days = open is null || endForDays is null ? null
                : Math.Max(0, DateOnly.FromDateTime(endForDays.Value).DayNumber - DateOnly.FromDateTime(open.Value).DayNumber);

            quotes.TryGetValue(symbol, out var q);
            var (currency, _) = TradingCurrency.Resolve(symbol, q?.Currency);
            var plan = plans.FirstOrDefault(p => p.AccountType == account && p.Symbol == symbol
                && p.EpisodeOpenDate.HasValue && open.HasValue && p.EpisodeOpenDate.Value.Date == open.Value);
            var history = events.Where(e => e.AccountType == account && e.Symbol == symbol
                    && (open is null || e.OccurredAtUtc.Date >= open.Value) && (lastClose is null || stillOpen || e.OccurredAtUtc.Date <= lastClose.Value.AddDays(1)))
                .Select(e => new JournalHistoryItemDto(e.OccurredAtUtc, e.EventType, HistoryLine(e))).ToList();

            result.Add(new JournalRowDto(
                id.HasValue ? $"ep:{id}" : $"lot:{first.Id}", symbol, account, id,
                id.HasValue ? EpisodeResolutions.Persisted : EpisodeResolutions.Unresolved, open, stillOpen ? null : lastClose, days, qty,
                qty <= 0 ? null : Math.Round(cost / qty, 4),
                priced && qty > 0 ? Math.Round(rows.Sum(r => r.ClosingPrice!.Value * r.Shares) / qty, 4) : null,
                pnl, pct, currency,
                first.DecisionSource, rows.OrderBy(r => r.CloseDate).ThenBy(r => r.Id).Last().DecisionSourceClosed, plan?.TradeRationale,
                string.Join(" | ", rows.Select(r => r.Notes).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct()),
                stillOpen ? "Partially closed" : pnl is null ? "Unknown" : pnl > 0 ? "Win" : pnl < 0 ? "Loss" : "Flat",
                stillOpen, history, rows.Select(r => r.Id).ToList()));
        }
        return result.OrderByDescending(r => r.FinalCloseDate ?? DateTime.MaxValue).ThenBy(r => r.Symbol).ToList();
    }

    private static string HistoryLine(RiskAuditEvent e)
    {
        if (string.IsNullOrEmpty(e.ChangesJson)) return e.EventType;
        var changes = System.Text.Json.JsonSerializer.Deserialize<List<AuditChangeDto>>(e.ChangesJson, RiskSupport.Json) ?? [];
        return string.Join("; ", changes.Select(c => $"{c.Field}: {c.Old ?? "—"} → {c.New ?? "—"}"));
    }
}
