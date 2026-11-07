using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services.Risk;

/// <summary>Whose rows a risk read covers. Mirrors <see cref="RiskUserContext"/>: admins see everything; others see their rows and legacy NULL-owner rows.</summary>
public sealed record RiskScope(string? UserId, bool IsAdmin);

public sealed record OpenRiskPosition(
    ResolvedPosition Position, StockQuote? Quote, PriceEvidence Evidence, RiskPlan? Plan, bool PlanCurrent,
    IReadOnlyList<BrokerStopRecord> ActiveStops, RiskEvaluation Evaluation, string? Owner);

public sealed record OpenRiskSnapshot(
    IReadOnlyList<OpenRiskPosition> Positions, IReadOnlyList<BrokerStopRecord> AllStops, decimal ThresholdPct, DateTime NowUtc);

/// <summary>
/// Read-only source of open Swing position risk. It is the single place that assembles the evaluator input (episodes,
/// plans, broker stops, quote evidence) and calls <see cref="StopRiskEvaluator"/>; Swing Trading and Final Action both use it.
/// It loads everything once per call, so evaluating many lots costs a fixed number of queries.
/// </summary>
public interface IOpenRiskReader
{
    Task<OpenRiskSnapshot> ReadAsync(RiskScope scope, IReadOnlyDictionary<string, StockQuote> quotes, CancellationToken ct = default);
}

public sealed class OpenRiskReader(AppDbContext db, IConfiguration config, TimeProvider? clock = null) : IOpenRiskReader
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<OpenRiskSnapshot> ReadAsync(RiskScope scope, IReadOnlyDictionary<string, StockQuote> quotes, CancellationToken ct = default)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var threshold = config.GetValue("Risk:ApproachingStopThresholdPct", StopRiskEvaluator.DefaultThresholdPct);

        var lotQuery = db.PortfolioItems.AsQueryable().Where(x => !x.IsManual);
        if (!scope.IsAdmin) lotQuery = lotQuery.Where(x => x.UserId == scope.UserId || x.UserId == null);
        var lots = await lotQuery.AsNoTracking().ToListAsync(ct);
        var refs = lots.Select(LotRef.From).ToList();
        var epIds = refs.Where(l => l.IsOpen && l.TradeEpisodeId.HasValue).Select(l => l.TradeEpisodeId!.Value).Distinct().ToList();
        var episodes = epIds.Count == 0 ? new Dictionary<Guid, TradeEpisode>()
            : await db.TradeEpisodes.AsNoTracking().Where(e => epIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, ct);
        var positions = TradeEpisodeResolver.ResolveOpenSwingPositions(refs, episodes);

        var planQuery = db.RiskPlans.AsNoTracking();
        var stopQuery = db.BrokerStopRecords.AsNoTracking();
        if (!scope.IsAdmin)
        {
            planQuery = planQuery.Where(x => x.UserId == scope.UserId || x.UserId == null);
            stopQuery = stopQuery.Where(x => x.UserId == scope.UserId || x.UserId == null);
        }
        var plans = await planQuery.ToListAsync(ct);
        var stops = await stopQuery.ToListAsync(ct);

        var result = new List<OpenRiskPosition>();
        foreach (var pos in positions)
        {
            quotes.TryGetValue(pos.Symbol, out var quote);
            var evidence = quote is null ? PriceEvidence.Unavailable("no quote in portfolio snapshot")
                : PriceEvidence.FromQuote(quote.CurrentPrice, quote.Timestamp, quote.MarketState, pos.Symbol, now, quote.SourcePriceTimestamp);

            var owner = pos.OpenLots[0].UserId;
            var scopePlans = plans.Where(p => p.UserId == owner && p.AccountType == pos.Account && p.Symbol == pos.Symbol)
                .OrderByDescending(p => p.UpdatedAtUtc).ToList();
            var plan = scopePlans.FirstOrDefault(p => RiskService.BelongsToEpisode(p.TradeEpisodeId, p.EpisodeOpenDate, pos))
                ?? scopePlans.FirstOrDefault();
            var planCurrent = plan is not null && RiskService.BelongsToEpisode(plan.TradeEpisodeId, plan.EpisodeOpenDate, pos);
            var active = stops.Where(s => (s.UserId == owner || (owner == null && s.UserId == scope.UserId)) && s.AccountType == pos.Account
                    && s.Symbol == pos.Symbol && BrokerUserStatuses.IsActive(s.UserStatus)
                    && RiskService.BelongsToEpisode(s.TradeEpisodeId, s.EpisodeOpenDate, pos))
                .OrderByDescending(s => s.TriggerPrice).ToList();

            var eval = StopRiskEvaluator.Evaluate(new RiskEvalInput(evidence, planCurrent ? plan!.PlannedStopPrice : null,
                active.Select(s => new StopLite(s.Id, s.TriggerPrice, s.CoveredQuantity, s.BrokerVerificationStatus)).ToList(),
                pos.SwingQuantity, pos.TotalQuantity, threshold));
            result.Add(new OpenRiskPosition(pos, quote, evidence, plan, planCurrent, active, eval, owner));
        }
        return new OpenRiskSnapshot(result, stops, threshold, now);
    }
}
