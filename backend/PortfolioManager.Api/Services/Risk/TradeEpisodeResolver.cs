using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services.Risk;

/// <summary>Immutable view of one PortfolioItems row (one lot / transaction record).</summary>
public sealed record LotRef(
    int Id,
    string? UserId,
    string Symbol,
    string? AccountType,
    decimal Shares,
    string? TransactionType,
    DateTime? OpenDate,
    DateTime? CloseDate,
    string? HoldingRole,
    Guid? TradeEpisodeId,
    decimal AverageCostBasis,
    decimal? ClosingPrice,
    string? DecisionSource,
    string? DecisionSourceClosed,
    string? Notes)
{
    public bool IsOpen => string.Equals(TransactionType, "OPEN", StringComparison.OrdinalIgnoreCase) && Shares > 0;
    public bool IsClose => string.Equals(TransactionType, "CLOSE", StringComparison.OrdinalIgnoreCase);
    public bool IsSwing => string.Equals(HoldingRole, "Swing", StringComparison.OrdinalIgnoreCase);
    public string Account => AccountType ?? string.Empty;

    public static LotRef From(PortfolioItem i) => new(i.Id, i.UserId, i.Symbol, i.AccountType, i.Shares, i.TransactionType,
        i.OpenDate, i.CloseDate, i.HoldingRole, i.TradeEpisodeId, i.AverageCostBasis, i.ClosingPrice,
        i.DecisionSource, i.DecisionSourceClosed, i.Notes);
}

public static class EpisodeResolutions
{
    /// <summary>Open lots carry one persisted TradeEpisodeId.</summary>
    public const string Persisted = "Persisted";
    /// <summary>No stored id, but the history proves one unbroken episode (no overlapping closes, one role).</summary>
    public const string Derived = "Derived";
    /// <summary>History cannot reliably establish the episode boundary. Never inferred from dates alone.</summary>
    public const string Unresolved = "Unresolved";
}

/// <summary>Current open exposure of one account+symbol, with its (possibly unresolved) trade-episode identity.</summary>
public sealed record ResolvedPosition(
    string Account,
    string Symbol,
    IReadOnlyList<LotRef> OpenLots,
    decimal TotalQuantity,
    decimal SwingQuantity,
    bool MixedRole,
    Guid? EpisodeId,
    DateTime? EpisodeOpenDate,
    DateTime? FirstPurchaseDate,
    DateTime? LastExecutionDate,
    string Resolution,
    string? ResolutionNote);

public sealed record BackfillAssignment(int ItemId, Guid EpisodeId);
public sealed record BackfillEpisode(Guid Id, string? UserId, string Account, string Symbol, DateTime OpenDate, DateTime? ClosedDate, string Status);
public sealed record BackfillUnresolved(int ItemId, string Symbol, string Account, string Reason);
public sealed record BackfillPlan(
    IReadOnlyList<BackfillEpisode> Episodes,
    IReadOnlyList<BackfillAssignment> Assignments,
    IReadOnlyList<BackfillUnresolved> Unresolved);

/// <summary>
/// Pure trade-episode logic. Records: an addition is a new OPEN row; a partial close turns the row into a CLOSE row
/// and splits the remainder into a new OPEN row with the SAME OpenDate; a full close flips the row to CLOSE.
/// Nothing links those rows except TradeEpisodeId, so history without ids is only trusted when it is unambiguous.
/// </summary>
public static class TradeEpisodeResolver
{
    public static string NormSymbol(string? s) => (s ?? string.Empty).Trim().ToUpperInvariant();
    public static string NormAccount(string? s) => (s ?? string.Empty).Trim();

    public static bool SameScope(LotRef l, string account, string symbol) =>
        string.Equals(NormAccount(l.AccountType), NormAccount(account), StringComparison.OrdinalIgnoreCase)
        && NormSymbol(l.Symbol) == NormSymbol(symbol);

    /// <summary>Every account+symbol that currently holds at least one open Swing lot.</summary>
    public static IReadOnlyList<ResolvedPosition> ResolveOpenSwingPositions(
        IReadOnlyList<LotRef> allLots, IReadOnlyDictionary<Guid, TradeEpisode>? episodes = null)
    {
        var result = new List<ResolvedPosition>();
        var groups = allLots.Where(l => l.IsOpen)
            .GroupBy(l => (Account: NormAccount(l.AccountType).ToUpperInvariant(), Symbol: NormSymbol(l.Symbol)));

        foreach (var g in groups)
        {
            var open = g.ToList();
            if (!open.Any(l => l.IsSwing)) continue;
            result.Add(ResolveScope(allLots, open, episodes));
        }
        return result.OrderBy(p => p.Symbol).ThenBy(p => p.Account).ToList();
    }

    public static ResolvedPosition ResolveScope(
        IReadOnlyList<LotRef> allLots, IReadOnlyList<LotRef> open, IReadOnlyDictionary<Guid, TradeEpisode>? episodes)
    {
        var first = open[0];
        var account = NormAccount(first.AccountType);
        var symbol = NormSymbol(first.Symbol);
        var total = open.Sum(l => l.Shares);
        var swing = open.Where(l => l.IsSwing).Sum(l => l.Shares);
        var mixed = open.Any(l => !l.IsSwing);

        var dated = open.Where(l => l.OpenDate.HasValue).Select(l => l.OpenDate!.Value.Date).ToList();
        DateTime? firstPurchase = dated.Count > 0 ? dated.Min() : null;
        DateTime? lastExec = dated.Count > 0 ? dated.Max() : null;

        Guid? episodeId = null;
        DateTime? episodeOpen = firstPurchase;
        string resolution;
        string? note = null;

        var ids = open.Select(l => l.TradeEpisodeId).Distinct().ToList();
        if (ids.Count == 1 && ids[0].HasValue)
        {
            episodeId = ids[0];
            resolution = EpisodeResolutions.Persisted;
            if (episodes is not null && episodes.TryGetValue(episodeId!.Value, out var ep)) episodeOpen = ep.OpenDate.Date;
        }
        else if (ids.Count > 1)
        {
            resolution = EpisodeResolutions.Unresolved;
            note = "Open lots belong to different or partially assigned trade episodes.";
        }
        else if (firstPurchase is null)
        {
            resolution = EpisodeResolutions.Unresolved;
            note = "An open lot has no open date.";
        }
        else if (mixed)
        {
            resolution = EpisodeResolutions.Unresolved;
            note = "Account holds Swing and non-Swing lots; the trade boundary is not inferred without a stored episode id.";
        }
        else
        {
            var overlapping = allLots.FirstOrDefault(l =>
                l.IsClose && NormSymbol(l.Symbol) == symbol
                && (l.CloseDate is null || l.CloseDate.Value.Date >= firstPurchase.Value));
            if (overlapping is not null)
            {
                resolution = EpisodeResolutions.Unresolved;
                note = SameScope(overlapping, account, symbol)
                    ? $"A CLOSE record (#{overlapping.Id}) in this account overlaps the open period; it cannot be proven to belong to this trade."
                    : $"A CLOSE record (#{overlapping.Id}) for this symbol in another account overlaps the open period; not linked.";
            }
            else
            {
                resolution = EpisodeResolutions.Derived;
            }
        }

        return new ResolvedPosition(account, symbol, open, total, swing, mixed, episodeId, episodeOpen,
            firstPurchase, lastExec, resolution, note);
    }

    /// <summary>
    /// Plans (never applies) episode ids for history without one. Only unambiguous relationships are linked:
    /// an open lot chain of one role with no overlapping close anywhere for the symbol, and CLOSE records whose
    /// date ranges are disjoint. Everything else is reported as unresolved.
    /// </summary>
    public static BackfillPlan PlanBackfill(IReadOnlyList<LotRef> allLots)
    {
        var episodes = new List<BackfillEpisode>();
        var assignments = new List<BackfillAssignment>();
        var unresolved = new List<BackfillUnresolved>();
        var lots = allLots.Where(l => l.TradeEpisodeId is null && (l.IsOpen || l.IsClose)).ToList();

        foreach (var g in lots.GroupBy(l => (Account: NormAccount(l.AccountType).ToUpperInvariant(), Symbol: NormSymbol(l.Symbol))))
        {
            var rows = g.ToList();
            var opens = rows.Where(l => l.IsOpen).ToList();
            var closes = rows.Where(l => l.IsClose).ToList();
            var account = NormAccount(rows[0].AccountType);

            if (opens.Count > 0)
            {
                var reason = OpenChainAmbiguity(allLots, opens, g.Key.Symbol);
                if (reason is null)
                {
                    var open = opens.Min(l => l.OpenDate!.Value.Date);
                    var ep = new BackfillEpisode(Guid.NewGuid(), opens[0].UserId, account, g.Key.Symbol, open, null, EpisodeStatuses.Open);
                    episodes.Add(ep);
                    assignments.AddRange(opens.Select(l => new BackfillAssignment(l.Id, ep.Id)));
                }
                else
                {
                    unresolved.AddRange(opens.Select(l => new BackfillUnresolved(l.Id, g.Key.Symbol, account, reason)));
                }
            }

            if (closes.Count == 0) continue;

            var scopeOpen = allLots.Where(l => l.IsOpen && NormSymbol(l.Symbol) == g.Key.Symbol
                && NormAccount(l.AccountType).Equals(account, StringComparison.OrdinalIgnoreCase)).ToList();
            var scopeMinOpen = scopeOpen.Where(l => l.OpenDate.HasValue).Select(l => (DateTime?)l.OpenDate!.Value.Date).Min();
            var withDates = closes.Where(l => l.OpenDate.HasValue && l.CloseDate.HasValue).OrderBy(l => l.OpenDate).ToList();
            foreach (var l in closes.Except(withDates))
                unresolved.Add(new BackfillUnresolved(l.Id, g.Key.Symbol, account, "CLOSE record is missing its open or close date."));

            foreach (var l in withDates)
            {
                // Still-open lots are only safe to separate from a CLOSE record when the exit finished before the first open lot.
                if (scopeOpen.Count > 0 && (scopeMinOpen is null || l.CloseDate!.Value.Date >= scopeMinOpen.Value))
                {
                    unresolved.Add(new BackfillUnresolved(l.Id, g.Key.Symbol, account,
                        "CLOSE record overlaps lots that are still open; cannot be proven to belong to the open trade or an earlier one."));
                    continue;
                }
                var overlaps = withDates.Any(o => o.Id != l.Id
                    && o.OpenDate!.Value.Date <= l.CloseDate!.Value.Date && l.OpenDate!.Value.Date <= o.CloseDate!.Value.Date);
                if (overlaps)
                {
                    unresolved.Add(new BackfillUnresolved(l.Id, g.Key.Symbol, account,
                        "CLOSE records with overlapping dates (possible partial exits); not merged or split."));
                    continue;
                }
                // Same symbol active in another account over the same dates may be an account transfer; not linked.
                var otherAccount = allLots.Any(o => o.Id != l.Id && NormSymbol(o.Symbol) == g.Key.Symbol
                    && !NormAccount(o.AccountType).Equals(account, StringComparison.OrdinalIgnoreCase)
                    && o.OpenDate.HasValue && o.OpenDate.Value.Date <= l.CloseDate!.Value.Date
                    && (o.IsOpen || (o.CloseDate.HasValue && o.CloseDate.Value.Date >= l.OpenDate!.Value.Date)));
                if (otherAccount)
                {
                    unresolved.Add(new BackfillUnresolved(l.Id, g.Key.Symbol, account,
                        "Records for the same symbol in another account overlap these dates (possible transfer); not linked."));
                    continue;
                }
                var ep = new BackfillEpisode(Guid.NewGuid(), l.UserId, account, g.Key.Symbol,
                    l.OpenDate!.Value.Date, l.CloseDate!.Value.Date, EpisodeStatuses.Closed);
                episodes.Add(ep);
                assignments.Add(new BackfillAssignment(l.Id, ep.Id));
            }
        }
        return new BackfillPlan(episodes, assignments, unresolved);
    }

    private static string? OpenChainAmbiguity(IReadOnlyList<LotRef> all, IReadOnlyList<LotRef> opens, string symbol)
    {
        if (opens.Any(l => !l.OpenDate.HasValue)) return "An open lot has no open date.";
        if (opens.Select(l => (l.HoldingRole ?? string.Empty).ToUpperInvariant()).Distinct().Count() > 1)
            return "Open lots have different roles; additions are not linked automatically.";
        var minOpen = opens.Min(l => l.OpenDate!.Value.Date);
        var overlapping = all.FirstOrDefault(l => l.IsClose && NormSymbol(l.Symbol) == symbol
            && (l.CloseDate is null || l.CloseDate.Value.Date >= minOpen));
        return overlapping is null ? null
            : $"CLOSE record #{overlapping.Id} for this symbol overlaps the open period and cannot be proven related or unrelated.";
    }

    /// <summary>Whole calendar days in America/Toronto from the episode open date; never negative.</summary>
    public static int DaysHeld(DateTime? openDate, DateTime utcNow)
    {
        if (openDate is null) return 0;
        var today = TradingCalendar.TorontoDate(utcNow);
        var days = today.DayNumber - DateOnly.FromDateTime(openDate.Value).DayNumber;
        return Math.Max(0, days);
    }
}
