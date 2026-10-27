using System.Globalization;

namespace PortfolioManager.Api.Services.FinalAction;

/// <summary>Portfolio-level facts a technical signal must be reconciled with before it may become a trade recommendation.</summary>
public sealed record PortfolioEvidence(
    double? UnrealizedGainPct,
    /// <summary>Most recent addition (latest open lot) of the symbol; null when unrecorded.</summary>
    DateOnly? EntryDate,
    /// <summary>America/New_York trading date of the scan that produced the technical evidence.</summary>
    DateOnly EvidenceDate,
    DateTime EvidenceAsOfUtc,
    /// <summary>Whole-symbol position size across all lots, % of the portfolio total.</summary>
    double? PositionSizePct,
    /// <summary>Configured single-position limit for the holding role, when one exists.</summary>
    double? PositionLimitPct,
    bool SectorOverTarget,
    bool HardStructuralNegative,
    string? DecisionSource,
    /// <summary>The target lot's role has no configured limit while the symbol's other lots hold roles that do: the applicable policy is unclear.</summary>
    bool RolePolicyAmbiguous = false,
    string? RolePolicyDetail = null,
    double? SectorPct = null,
    double? SectorTargetPct = null,
    bool SectorKnown = true);

public sealed record GateOutcome(FinalActionDecision Decision, IReadOnlyList<string> ReasonCodes, string? OriginalAction);

public static class GateReasons
{
    public const string TechnicalOnly = "TECHNICAL_ONLY_NO_PORTFOLIO_TRIGGER";
    public const string EntryDayEvidence = "EVIDENCE_NOT_POST_ENTRY";
    public const string EntryDateUnknown = "ENTRY_DATE_UNKNOWN";
    public const string AllocationBreach = "ALLOCATION_LIMIT_BREACH";
    public const string LossBreach = "POST_ENTRY_LOSS";
    public const string StructuralBreakdown = "CONFIRMED_STRUCTURAL_BREAKDOWN";
    public const string TechnicalExitPostEntry = "TECHNICAL_EXIT_POST_ENTRY";
    public const string AddBlockedPositionLimit = "ADD_BLOCKED_POSITION_LIMIT";
    public const string AddBlockedSectorOver = "ADD_BLOCKED_SECTOR_OVER";
    public const string AddRolePolicyAmbiguous = "ADD_ROLE_POLICY_AMBIGUOUS";
}

/// <summary>
/// Single reconciliation step between the technical role rules and the portfolio decision. A technical-only
/// reduction (Trim / Exit / Cut Loss / Close) is promoted to a recommendation only when a portfolio-level trigger
/// exists: a post-entry loss, a confirmed structural breakdown, a breach of a configured allocation limit, or (for
/// short-horizon roles) a technical exit confirmed after the entry. Otherwise it becomes an advisory Hold and the
/// original technical reading stays visible as a reminder. Purchases are conditioned on configured limits.
/// Profit-taking overrides, advisory actions and watchlist decisions are never touched.
/// </summary>
public static class PortfolioEvidenceGate
{
    private enum Kind { None, Add, Trim, ProfitExit, LossExit }

    private static readonly HashSet<string> ShortHorizonRoles = new(StringComparer.OrdinalIgnoreCase)
        { "Swing", "Speculative", "Options" };

    public static bool IsShortHorizon(string? role) => role is not null && ShortHorizonRoles.Contains(role.Trim());

    public static GateOutcome Apply(FinalActionDecision d, string? role, PortfolioEvidence e)
    {
        var kind = Classify(d.FinalAction);
        if (kind == Kind.None) return new GateOutcome(d, [], null);

        if (kind == Kind.Add) return ConditionAdd(d, e);

        var entryDay = e.EntryDate is { } entry && entry >= e.EvidenceDate;
        var codes = new List<string>();
        if (e.EntryDate is null) codes.Add(GateReasons.EntryDateUnknown);

        var allocationBreach = e.PositionSizePct is { } size && e.PositionLimitPct is { } limit && size > limit;
        var loss = e.UnrealizedGainPct is < 0;
        var structural = e.HardStructuralNegative && !entryDay;
        var shortRole = IsShortHorizon(role);

        string? pass = null;
        string? detail = null;
        if (allocationBreach && kind != Kind.LossExit)
        {
            pass = GateReasons.AllocationBreach;
            detail = string.Create(CultureInfo.InvariantCulture,
                $"Position {e.PositionSizePct:F1}% exceeds the configured {role} limit of {e.PositionLimitPct:F1}%.");
        }
        else if (structural)
        {
            pass = GateReasons.StructuralBreakdown;
            detail = "Confirmed structural breakdown after entry.";
        }
        else if (shortRole && kind == Kind.LossExit && loss)
        {
            pass = GateReasons.LossBreach;
            detail = string.Create(CultureInfo.InvariantCulture, $"Price is below cost ({e.UnrealizedGainPct:F1}%) with technical weakness.");
        }
        else if (shortRole && kind != Kind.LossExit && !entryDay)
        {
            pass = GateReasons.TechnicalExitPostEntry;
            detail = $"Technical exit signal evaluated on {e.EvidenceDate:yyyy-MM-dd}, after the entry.";
        }

        if (pass is not null)
        {
            codes.Add(pass);
            var reason = $"{detail} Evidence as of {e.EvidenceAsOfUtc:yyyy-MM-dd HH:mm} UTC.";
            return new GateOutcome(d with { FinalActionReason = Join(d.FinalActionReason, reason) }, codes, null);
        }

        codes.Add(GateReasons.TechnicalOnly);
        if (entryDay) codes.Add(GateReasons.EntryDayEvidence);

        var catalyst = e.DecisionSource?.Contains("Catalyst", StringComparison.OrdinalIgnoreCase) == true;
        var replacement = catalyst ? "Hold / Catalyst Monitoring" : "Hold / Monitor";
        var why = entryDay
            ? $"Technical signal ({d.MomentumShift} / {d.TrendSetup}) was evaluated on {e.EvidenceDate:yyyy-MM-dd}, on or before the entry of {e.EntryDate:yyyy-MM-dd}; it is not post-entry evidence."
            : $"Technical signal ({d.MomentumShift} / {d.TrendSetup}) is not backed by a post-entry loss, confirmed breakdown or allocation breach for a {role ?? "Strategic"} holding.";
        var reasonText = $"{why} Original technical action: {d.FinalAction}. Evidence as of {e.EvidenceAsOfUtc:yyyy-MM-dd HH:mm} UTC.";

        var gated = d with
        {
            FinalAction = replacement,
            FinalActionClass = FinalActionEngine.FinalActionClass(replacement),
            HoverDescription = "Review stop-loss levels. Reminder only — no sell trigger is confirmed for this holding.",
            FinalActionReason = reasonText,
        };
        return new GateOutcome(gated, codes, d.FinalAction);
    }

    private static GateOutcome ConditionAdd(FinalActionDecision d, PortfolioEvidence e)
    {
        // The ADD belongs to the target lot's role: its limit applies to the symbol's total exposure. When that role has
        // no configured policy but the symbol's other roles do, no limit is guessed — the result needs review instead.
        if (e.RolePolicyAmbiguous)
        {
            const string review = "Add / Needs Review";
            var needsReview = d with
            {
                FinalAction = review,
                FinalActionClass = "ma-standby",
                FinalActionReason = Join(d.FinalActionReason,
                    $"The role policy for this addition is ambiguous ({e.RolePolicyDetail ?? "mixed roles"}). Not an instruction to buy; confirm the intended role. Original technical action: {d.FinalAction}."),
            };
            return new GateOutcome(needsReview, [GateReasons.AddRolePolicyAmbiguous], d.FinalAction);
        }

        string? code = null;
        string? text = null;
        if (e.PositionSizePct is { } size && e.PositionLimitPct is { } limit && size >= limit)
        {
            code = GateReasons.AddBlockedPositionLimit;
            text = string.Create(CultureInfo.InvariantCulture, $"Position {size:F1}% is at or above the configured limit of {limit:F1}%.");
        }
        else if (e.SectorOverTarget)
        {
            code = GateReasons.AddBlockedSectorOver;
            text = "Sector exposure is above its configured target.";
        }

        if (code is null) return new GateOutcome(d, [], null);
        const string replacement = "Hold / No Add";
        var gated = d with
        {
            FinalAction = replacement,
            FinalActionClass = "ma-hold", // the shared word classifier would read "No Add" as a buy
            FinalActionReason = Join(d.FinalActionReason, $"{text} Original technical action: {d.FinalAction}."),
        };
        return new GateOutcome(gated, [code], d.FinalAction);
    }

    private static Kind Classify(string action)
    {
        var def = FinalActionCatalog.Describe(action);
        if (def.Group is not ("Portfolio" or "Base")) return Kind.None;
        if (def.Category == "Entry") return Kind.Add;
        if (def.Category != "TrimExit" || def.Id == "TRIM/EXIT WATCH") return Kind.None;
        var a = action.ToLowerInvariant();
        if (a.Contains("cut loss") || a.Contains("close / protect") || a.Contains("thesis review")) return Kind.LossExit;
        if (a.Contains("take profit")) return Kind.ProfitExit;
        return Kind.Trim;
    }

    private static string Join(string? existing, string text) => string.IsNullOrEmpty(existing) ? text : $"{existing} {text}";
}
