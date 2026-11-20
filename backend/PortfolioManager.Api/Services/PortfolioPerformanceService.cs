using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

/// <summary>Net external cash flow for one (baseline, end] window, or the reason it cannot be trusted.
/// <see cref="Items"/> lists the individual external flows so a time-weighted return can split the period at each one.</summary>
public sealed record NetExternalFlowResult(bool Available, decimal NetFlow, string? Reason, IReadOnlyList<(DateOnly Date, decimal Amount)>? Items = null)
{
    public static NetExternalFlowResult Ok(decimal net, IReadOnlyList<(DateOnly Date, decimal Amount)>? items = null) => new(true, net, null, items);
    public static NetExternalFlowResult Unavailable(string reason) => new(false, 0m, reason);
}

public interface IPortfolioPerformanceService
{
    /// <summary>Read-only. Net Deposit minus Withdrawal for the user's ledger rows whose effective date
    /// (TransactionDate, else AddedAt, date-only) lies in (<paramref name="baselineDate"/>, <paramref name="endDate"/>].
    /// Returns Unavailable when classification/currency/coverage is ambiguous. Never writes.</summary>
    Task<NetExternalFlowResult> GetNetExternalFlowAsync(string userId, DateOnly baselineDate, DateOnly endDate, CancellationToken ct);

    /// <summary>Day/Week/Month/YTD flow-adjusted performance against the same live total the Dashboard shows.</summary>
    Task<IReadOnlyList<PeriodPerformanceDto>> BuildAsync(
        string userId, IReadOnlyList<PortfolioValueHistory> valuesAscending, decimal endTotal, DateOnly today, CancellationToken ct);
}

/// <summary>
/// Investment Gain/Loss = Ending Value − Starting Value − Net External Cash Flow.
/// Conventions: the ledger is date-only and the ending valuation is live (includes every cash row), so a
/// row dated on the end date is inside the window and a row dated on the baseline date is already inside the
/// baseline snapshot (backdated edits recalculate snapshots). Ledger rows carry no currency and no transfer
/// type, so currency/transfer ambiguity is detected heuristically and reported as Unavailable, never guessed.
/// </summary>
public sealed class PortfolioPerformanceService(AppDbContext db, IMutationClock mutationClock) : IPortfolioPerformanceService
{
    public const string Available = "Available";
    public const string Updating = "Updating";
    public const string Unavailable = "Unavailable";

    /// <summary>A baseline older than this many days before the period start is treated as stale.</summary>
    internal const int MaxBaselineGapDays = 7;

    private static readonly Regex UsdMarker = new(@"(?<![A-Za-z])(USD|US\$|USDOLLAR)(?![A-Za-z])|_USD\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private sealed record LedgerRow(string? Type, decimal Amount, DateOnly Date, string? Account, string? Description);

    public async Task<NetExternalFlowResult> GetNetExternalFlowAsync(
        string userId, DateOnly baselineDate, DateOnly endDate, CancellationToken ct)
    {
        var raw = await db.CashItems.AsNoTracking()
            .Where(c => c.UserId == userId || c.UserId == null)
            .Select(c => new { c.CashFlowType, c.Amount, Date = c.TransactionDate ?? c.AddedAt, c.AccountType, c.Description })
            .ToListAsync(ct);
        var rows = raw
            .Select(c => new LedgerRow(c.CashFlowType, c.Amount, DateOnly.FromDateTime(c.Date), c.AccountType, c.Description))
            .ToList();
        return Classify(rows, baselineDate, endDate);
    }

    private static NetExternalFlowResult Classify(IReadOnlyList<LedgerRow> rows, DateOnly baseline, DateOnly end)
    {
        // The live valuation sums every cash row regardless of date, so a future-dated row would be in the
        // ending value but outside every window.
        if (rows.Any(r => r.Date > end))
            return NetExternalFlowResult.Unavailable("A future-dated cash row is included in the live value.");

        var window = rows.Where(r => r.Date > baseline && r.Date <= end).ToList();

        if (window.Any(r => !CashFlowTypeRules.IsKnownType(r.Type)))
            return NetExternalFlowResult.Unavailable("Unclassified cash rows exist in this period.");

        var candidates = window.Where(r => CashFlowTypeRules.IsExternalFlow(r.Type)).ToList();

        if (candidates.Any(r => UsdMarker.IsMatch(r.Account ?? "") || UsdMarker.IsMatch(r.Description ?? "")))
            return NetExternalFlowResult.Unavailable("A USD or non-reporting-currency cash flow is not converted.");

        if (candidates.Any(r => (r.Description ?? "").Contains("transfer", StringComparison.OrdinalIgnoreCase)))
            return NetExternalFlowResult.Unavailable("A Deposit/Withdrawal is labelled as a transfer; external status is ambiguous.");

        // An internal move between two accounts can be keyed as a Withdrawal in one and a Deposit in the other.
        var possibleInternalTransfer = candidates
            .Where(r => r.Amount > 0)
            .Any(dep => candidates.Any(wd => wd.Amount < 0
                && wd.Date == dep.Date
                && wd.Amount == -dep.Amount
                && !string.Equals(wd.Account, dep.Account, StringComparison.OrdinalIgnoreCase)));
        if (possibleInternalTransfer)
            return NetExternalFlowResult.Unavailable("A matching Deposit/Withdrawal pair across accounts may be an internal transfer.");

        return NetExternalFlowResult.Ok(candidates.Sum(r => r.Amount), candidates.Select(r => (r.Date, r.Amount)).ToList());
    }

    /// <summary>Dashboard-only extra period: from the first snapshot on/after the cash-ledger start (the earliest baseline whose
    /// flows are fully recorded) to today. Snapshots before the ledger start are not comparable (see 2026-09-05 step change).</summary>
    public const string SinceStartPeriod = "SinceStart";
    internal const string MethodSimple = "Simple (no external cash flow in period)";
    internal const string MethodTwr = "Time-weighted (daily snapshots, flows at end of day)";

    public async Task<IReadOnlyList<PeriodPerformanceDto>> BuildAsync(
        string userId, IReadOnlyList<PortfolioValueHistory> valuesAscending, decimal endTotal, DateOnly today, CancellationToken ct)
    {
        var settings = await db.CashLedgerSettings.AsNoTracking().SingleOrDefaultAsync(s => s.Id == 1, ct);
        DateOnly? ledgerStart = settings is null ? null : DateOnly.FromDateTime(settings.LedgerStartDate);
        var updating = mutationClock.IsTodayDirty;

        var daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
        var periods = new (string Name, DateOnly Start)[]
        {
            ("Day", today),
            ("Week", today.AddDays(-daysSinceMonday)),
            ("Month", new DateOnly(today.Year, today.Month, 1)),
            ("YTD", new DateOnly(today.Year, 1, 1)),
        };

        var result = new List<PeriodPerformanceDto>(periods.Length + 1);
        foreach (var (name, start) in periods)
        {
            var baseline = valuesAscending.LastOrDefault(h => DateOnly.Parse(h.RecordedDate) < start);
            var gate = CheckBaseline(baseline, start, ledgerStart, updating);
            result.Add(await EvaluateWithFlowsAsync(userId, name, baseline, gate, valuesAscending, endTotal, today, ct));
        }

        var sinceBaseline = SinceStartBaseline(valuesAscending, ledgerStart, today);
        (string Status, string Reason)? sinceGate = sinceBaseline is null && !updating
            ? (Unavailable, "No snapshot exists on or after the cash ledger start.")
            : CheckBaseline(sinceBaseline, DateOnly.Parse(sinceBaseline?.RecordedDate ?? today.ToString("yyyy-MM-dd")), ledgerStart, updating);
        result.Add(await EvaluateWithFlowsAsync(userId, SinceStartPeriod, sinceBaseline, sinceGate, valuesAscending, endTotal, today, ct));
        return result;
    }

    internal static PortfolioValueHistory? SinceStartBaseline(IReadOnlyList<PortfolioValueHistory> valuesAscending, DateOnly? ledgerStart, DateOnly today) =>
        ledgerStart is null ? null
            : valuesAscending.FirstOrDefault(h => DateOnly.Parse(h.RecordedDate) >= ledgerStart && DateOnly.Parse(h.RecordedDate) < today);

    private async Task<PeriodPerformanceDto> EvaluateWithFlowsAsync(
        string userId, string name, PortfolioValueHistory? baseline, (string Status, string Reason)? gate,
        IReadOnlyList<PortfolioValueHistory> values, decimal endTotal, DateOnly today, CancellationToken ct)
    {
        NetExternalFlowResult? flow = null;
        if (gate is null)
            flow = await GetNetExternalFlowAsync(userId, DateOnly.Parse(baseline!.RecordedDate), today, ct);
        decimal? twr = flow is { Available: true, NetFlow: not 0m } && baseline is not null
            ? TimeWeightedReturnPct(values, baseline, endTotal, today, flow.Items ?? [])
            : null;
        return Evaluate(name, baseline, endTotal, flow, gate, twr);
    }

    /// <summary>
    /// Time-weighted return over stored daily snapshots: each interval's growth is (end value − flows in the interval) / start value,
    /// i.e. a flow is assumed to arrive at the end of its day and earns nothing until the next interval. Chained across intervals.
    /// Returns null (never a guess) when an interval containing a flow spans more than <see cref="MaxFlowIntervalDays"/> days
    /// (a missing snapshot) or a start value is not positive.
    /// </summary>
    internal static decimal? TimeWeightedReturnPct(
        IReadOnlyList<PortfolioValueHistory> valuesAscending, PortfolioValueHistory baseline, decimal endTotal, DateOnly end,
        IReadOnlyList<(DateOnly Date, decimal Amount)> flows)
    {
        var baselineDate = DateOnly.Parse(baseline.RecordedDate);
        var points = new List<(DateOnly Date, decimal Value)> { (baselineDate, baseline.TotalValue) };
        points.AddRange(valuesAscending
            .Select(v => (Date: DateOnly.Parse(v.RecordedDate), Value: v.TotalValue))
            .Where(p => p.Date > baselineDate && p.Date < end));
        points.Add((end, endTotal));

        var growth = 1m;
        for (var i = 1; i < points.Count; i++)
        {
            var (prevDate, prevValue) = points[i - 1];
            var (date, value) = points[i];
            if (prevValue <= 0m) return null;
            var flow = flows.Where(f => f.Date > prevDate && f.Date <= date).Sum(f => f.Amount);
            if (flow != 0m && date.DayNumber - prevDate.DayNumber > MaxFlowIntervalDays) return null;
            growth *= (value - flow) / prevValue;
        }
        return Math.Round((growth - 1m) * 100m, 2);
    }

    /// <summary>A flow may sit in an interval spanning a long weekend or holiday (Fri to Tue), not a missing week.</summary>
    internal const int MaxFlowIntervalDays = 5;

    /// <summary>Returns the status/reason that blocks the calculation, or null when the baseline is usable.</summary>
    internal static (string Status, string Reason)? CheckBaseline(
        PortfolioValueHistory? baseline, DateOnly periodStart, DateOnly? ledgerStart, bool updating)
    {
        if (updating) return (Updating, "Snapshot reseal is pending.");
        if (baseline is null) return (Unavailable, "No baseline snapshot before the period start.");
        var baselineDate = DateOnly.Parse(baseline.RecordedDate);
        if (periodStart.DayNumber - baselineDate.DayNumber > MaxBaselineGapDays)
            return (Unavailable, "Baseline snapshot is stale.");
        if (baseline.TotalValue != baseline.StocksValue + baseline.OptionsValue + baseline.CashValue)
            return (Unavailable, "Baseline snapshot is internally inconsistent.");
        if (ledgerStart is null || baselineDate < ledgerStart)
            return (Unavailable, "Cash ledger does not cover this period.");
        return null;
    }

    internal static PeriodPerformanceDto Evaluate(
        string period, PortfolioValueHistory? baseline, decimal endTotal, NetExternalFlowResult? flow, (string Status, string Reason)? gate,
        decimal? timeWeightedPercent = null)
    {
        if (gate is { } g)
            return new PeriodPerformanceDto(period, g.Status, g.Reason, baseline?.RecordedDate, null, null, null, null);
        if (flow is null || !flow.Available || baseline is null)
            return new PeriodPerformanceDto(period, Unavailable, flow?.Reason, baseline?.RecordedDate, null, null, null, null);

        var valueChange = endTotal - baseline.TotalValue;
        var gain = valueChange - flow.NetFlow;
        // No external flow: simple return equals the time-weighted one. With flows: time-weighted, or N/A when snapshots are insufficient.
        decimal? percent;
        string? method;
        if (flow.NetFlow == 0m)
        {
            percent = baseline.TotalValue > 0m ? Math.Round(gain / baseline.TotalValue * 100m, 2) : null;
            method = MethodSimple;
        }
        else
        {
            percent = timeWeightedPercent;
            method = timeWeightedPercent is null ? null : MethodTwr;
        }
        return new PeriodPerformanceDto(period, Available, null, baseline.RecordedDate, valueChange, flow.NetFlow, gain, percent,
            baseline.TotalValue, method);
    }
}
