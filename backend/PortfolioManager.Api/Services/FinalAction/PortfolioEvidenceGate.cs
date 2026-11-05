using System.Globalization;

namespace PortfolioManager.Api.Services.FinalAction;

/// <summary>
/// Read-only stop-risk facts for one open Swing lot, produced by the existing StopRiskEvaluator through IOpenRiskReader.
/// The gate never recalculates a stop; it only reacts to this summary and never implies a broker execution.
/// </summary>
public sealed record StopRiskEvidence(
    /// <summary>Breached | Clear | Unverified | NoPlan (StopRiskEvaluator PlannedStopStates).</summary>
    string PlannedStopState,
    decimal? PlannedStop,
    decimal? BrokerStop,
    bool PlannedBreached,
    bool BrokerBreached,
    decimal? ObservedPrice,
    DateTime? ObservedAtUtc,
    /// <summary>Live | EOD | Stale | Unavailable. Only Live/EOD observations can prove a breach or a clear state.</summary>
    string PriceBasis,
    string CoverageState,
    string Protection,
    IReadOnlyList<string> BrokerVerification,
    bool PlanAvailable,
    string Account,
    DateTime? EpisodeOpenDate);

/// <summary>Whether the portfolio valuation behind an allocation percentage is in one currency. No conversion is ever assumed.</summary>
public sealed record ValuationCheck(string Status, string Detail)
{
    public const string SingleCurrency = "SingleCurrency";
    public const string MixedCurrency = "MixedCurrency";
    public const string UnknownCurrency = "UnknownCurrency";
    public bool Reliable => Status == SingleCurrency;
}

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
    bool SectorKnown = true,
    StopRiskEvidence? Risk = null,
    /// <summary>Null means the valuation is treated as reliable (legacy callers).</summary>
    ValuationCheck? Valuation = null)
{
    public bool AllocationReliable => Valuation?.Reliable ?? true;
}

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
    public const string ProfitNoIndependentTrigger = "PROFIT_SIGNAL_NO_INDEPENDENT_TRIGGER";
    public const string OverboughtCaution = "OVERBOUGHT_CAUTION_NO_ADD";
    public const string PlannedStopBreached = "PLANNED_STOP_BREACHED";
    public const string BrokerStopBreached = "BROKER_STOP_BREACHED";
    public const string StopEvidenceUnverified = "STOP_EVIDENCE_UNVERIFIED";
    public const string StopCoveragePartial = "STOP_COVERAGE_PARTIAL";
    public const string StopCoverageUnknown = "STOP_COVERAGE_UNKNOWN";
    public const string AllocationUnavailable = "ALLOCATION_EVIDENCE_UNAVAILABLE";
    public const string LossNotIndependent = "LOSS_NOT_INDEPENDENT_EXIT_REASON";
    public const string ProfitProtectedByStop = "SELL_TRIGGER_PROFIT_PROTECTED_BY_STOP";
    public const string LabelCorrectedForLoss = "TAKE_PROFIT_LABEL_CORRECTED_FOR_LOSS";
}

/// <summary>
/// Single reconciliation step between the technical role rules and the portfolio decision. Permission to reduce is
/// decided by the catalog category and group of the action, never by its wording:
/// <list type="bullet">
/// <item>A technical-only reduction (Trim / Exit / Cut Loss / Close) is promoted to a recommendation only when a
/// portfolio-level trigger exists: a post-entry loss, a confirmed structural breakdown, a breach of a configured
/// allocation limit, or (for short-horizon roles) a technical exit confirmed after the entry. Otherwise it becomes an
/// advisory Hold and the original technical reading stays visible.</item>
/// <item>Profit-taking overrides (gain, 52-week-high, TFSA rules) are only retained for the same independent triggers.
/// A gain, a high or an overbought RSI alone is advisory evidence, never an instruction to sell.</item>
/// <item>Purchases are conditioned on configured limits and on a reliable (single-currency) allocation.</item>
/// <item>A verified Planned Stop or broker-stop breach on an open Swing position escalates to "Review Stop Breach"
/// unless an independently justified exit already stands.</item>
/// </list>
/// </summary>
public static class PortfolioEvidenceGate
{
    public const string ReviewStopBreach = "Review Stop Breach";
    public const string ProfitWatchNoAdd = "Hold / Profit Watch — No Add";
    public const string TrailStopNoAdd = "Hold / Trail Stop — No Add";
    public const string SpecTrailStopNoAdd = "Hold Spec / Trail Stop — No Add";

    private enum Kind { None, Add, Trim, ProfitExit, LossExit, ProfitTake, ProfitWatch }

    private static readonly HashSet<string> ShortHorizonRoles = new(StringComparer.OrdinalIgnoreCase)
        { "Swing", "Speculative", "Options" };

    private static readonly HashSet<string> LossExitIds = new(StringComparer.Ordinal)
        { "EXIT/CUT LOSS", "CUT LOSS/EXIT", "CUT LOSS/THESIS REVIEW", "CLOSE/PROTECT" };

    private static readonly HashSet<string> ProfitExitIds = new(StringComparer.Ordinal)
        { "EXIT/TAKE PROFIT", "TAKE PROFIT/EXIT", "CLOSE/TAKE PROFIT" };

    public static bool IsShortHorizon(string? role) => role is not null && ShortHorizonRoles.Contains(role.Trim());

    public static GateOutcome Apply(FinalActionDecision d, string? role, PortfolioEvidence e)
    {
        var (adjusted, correctedFrom) = CorrectTakeProfitLabelOnLoss(d, e);
        var reduced = ApplyReduction(adjusted, role, e);
        if (correctedFrom is not null)
            reduced = reduced with { OriginalAction = reduced.OriginalAction ?? correctedFrom, ReasonCodes = [.. reduced.ReasonCodes, GateReasons.LabelCorrectedForLoss] };
        return ApplyStopRisk(reduced, adjusted, e);
    }

    /// <summary>A position at a loss is never told to "Take Profit": the sell trigger stands, only the label is corrected.</summary>
    private static (FinalActionDecision Decision, string? CorrectedFrom) CorrectTakeProfitLabelOnLoss(FinalActionDecision d, PortfolioEvidence e)
    {
        if (e.UnrealizedGainPct is not < 0) return (d, null);
        var corrected = d.FinalAction switch
        {
            "Exit / Take Profit" => "Exit / Cut Loss",
            "Take Profit / Exit" => "Cut Loss / Exit",
            "Close / Take Profit" => "Close / Protect",
            _ => null,
        };
        return corrected is null
            ? (d, null)
            : (d with { FinalAction = corrected, FinalActionClass = FinalActionEngine.FinalActionClass(corrected) }, d.FinalAction);
    }

    /// <summary>Cooling or Breakdown without waterfall/warning evidence: weakness worth watching, not by itself a reason to exit.</summary>
    private static bool IsMildWeakness(FinalActionDecision d) =>
        (d.TrendSetup == "Cooling" || d.MomentumShift == "Breakdown")
        && d.MomentumShift is not ("Active Sell Trigger" or "Bearish Shift" or "Warning")
        && d.TrendSetup != "Waterfall / Falling Knife";

    /// <summary>A profitable position whose planned stop is verified clear is managed by that stop, not exited on a technical sell alone.</summary>
    private static bool IsProfitProtectedByStop(PortfolioEvidence e) =>
        e.UnrealizedGainPct is > 0
        && e.Risk is { PlannedStopState: "Clear", PlannedBreached: false, BrokerBreached: false };

    private static GateOutcome ApplyReduction(FinalActionDecision d, string? role, PortfolioEvidence e)
    {
        var kind = Classify(d.FinalAction);
        if (kind == Kind.None) return new GateOutcome(d, [], null);

        if (kind == Kind.Add) return ConditionAdd(d, e);
        if (kind == Kind.ProfitWatch) return ProfitAdvisory(d, role, e, []);

        var entryDay = e.EntryDate is { } entry && entry >= e.EvidenceDate;
        var codes = new List<string>();
        if (e.EntryDate is null) codes.Add(GateReasons.EntryDateUnknown);

        var allocationBreach = e.AllocationReliable
            && e.PositionSizePct is { } size && e.PositionLimitPct is { } limit && size > limit;
        var loss = e.UnrealizedGainPct is < 0;
        var structural = e.HardStructuralNegative && !entryDay;
        var shortRole = IsShortHorizon(role);
        var sellTrigger = d.MomentumShift == "Active Sell Trigger";

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
        else if (kind == Kind.ProfitTake)
        {
            // Gain, 52-week-high proximity, TFSA timing or overbought RSI are not independent exit reasons.
            // Only a confirmed sell trigger on a short-horizon role after entry keeps the reduction.
            if (shortRole && sellTrigger && !entryDay)
            {
                pass = GateReasons.TechnicalExitPostEntry;
                detail = $"Confirmed sell trigger evaluated on {e.EvidenceDate:yyyy-MM-dd}, after the entry.";
            }
        }
        else if (kind == Kind.ProfitExit && IsProfitProtectedByStop(e))
        {
            // No pass: handled as a stop-management advisory below.
        }
        else if (shortRole && kind == Kind.LossExit && loss && !IsMildWeakness(d))
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

        if (kind == Kind.ProfitTake) return ProfitAdvisory(d, role, e, codes);
        if (kind == Kind.ProfitExit && IsProfitProtectedByStop(e)) return ProfitProtectedAdvisory(d, role, e, codes);

        codes.Add(GateReasons.TechnicalOnly);
        if (entryDay) codes.Add(GateReasons.EntryDayEvidence);
        var lossOnly = shortRole && kind == Kind.LossExit && loss && IsMildWeakness(d);
        if (lossOnly) codes.Add(GateReasons.LossNotIndependent);

        var catalyst = e.DecisionSource?.Contains("Catalyst", StringComparison.OrdinalIgnoreCase) == true;
        var replacement = catalyst ? "Hold / Catalyst Monitoring" : "Hold / Monitor";
        var why = entryDay
            ? $"Technical signal ({d.MomentumShift} / {d.TrendSetup}) was evaluated on {e.EvidenceDate:yyyy-MM-dd}, on or before the entry of {e.EntryDate:yyyy-MM-dd}; it is not post-entry evidence."
            : lossOnly
                ? string.Create(CultureInfo.InvariantCulture,
                    $"A post-entry loss ({e.UnrealizedGainPct:F1}%) with only mild weakness ({d.MomentumShift} / {d.TrendSetup}) is risk evidence, not an independent exit reason; no stop breach, confirmed breakdown or allocation breach exists for a {role ?? "Strategic"} holding.")
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

    /// <summary>A confirmed sell trigger on a profitable position whose planned stop is verified clear: protect the gain with the stop instead of exiting.</summary>
    private static GateOutcome ProfitProtectedAdvisory(FinalActionDecision d, string? role, PortfolioEvidence e, List<string> codes)
    {
        var replacement = string.Equals(role?.Trim(), "Speculative", StringComparison.OrdinalIgnoreCase) ? SpecTrailStopNoAdd : TrailStopNoAdd;
        codes.Add(GateReasons.ProfitProtectedByStop);
        var stop = e.Risk?.PlannedStop is { } s ? string.Create(CultureInfo.InvariantCulture, $" {s:0.####}") : "";
        var reason = string.Create(CultureInfo.InvariantCulture,
            $"Confirmed sell trigger on a profitable position (unrealized gain {e.UnrealizedGainPct:F1}%) with the planned stop{stop} verified clear: tighten or trail the stop to protect the gain rather than exit. " +
            $"No stop breach, confirmed breakdown or allocation breach exists. Original rule output: {d.FinalAction}. Evidence as of {e.EvidenceAsOfUtc:yyyy-MM-dd HH:mm} UTC.");
        var gated = d with
        {
            FinalAction = replacement,
            FinalActionClass = FinalActionEngine.FinalActionClass(replacement),
            HoverDescription = "Protect the gain: tighten or trail the stop. Advisory only — no exit trigger is independently confirmed.",
            FinalActionReason = reason,
        };
        return new GateOutcome(gated, codes, d.FinalAction);
    }

    /// <summary>A profit-taking rule fired without an independent exit reason: keep the evidence, drop the instruction to sell.</summary>
    private static GateOutcome ProfitAdvisory(FinalActionDecision d, string? role, PortfolioEvidence e, List<string> codes)
    {
        var overextended = FinalActionEngine.IsOverextended(d.TrendSetup, d.MomentumShift);
        var shortRole = IsShortHorizon(role);
        var replacement = overextended
            ? shortRole ? (string.Equals(role?.Trim(), "Speculative", StringComparison.OrdinalIgnoreCase) ? SpecTrailStopNoAdd : TrailStopNoAdd)
                        : FinalActionEngine.OverboughtCaution
            : ProfitWatchNoAdd;

        codes.Add(GateReasons.ProfitNoIndependentTrigger);
        if (overextended) codes.Add(GateReasons.OverboughtCaution);
        var gain = e.UnrealizedGainPct is { } g ? string.Create(CultureInfo.InvariantCulture, $" (unrealized gain {g:F1}%)") : "";
        var reason = $"Profit-taking evidence{gain} is not an independent exit reason for a {(string.IsNullOrWhiteSpace(role) ? "Strategic" : role)} holding: " +
            $"no allocation breach, confirmed structural breakdown or confirmed sell trigger. Do not add; manage with a stop. Original rule output: {d.FinalAction}. " +
            $"Evidence as of {e.EvidenceAsOfUtc:yyyy-MM-dd HH:mm} UTC.";
        var gated = d with
        {
            FinalAction = replacement,
            FinalActionClass = FinalActionEngine.FinalActionClass(replacement),
            HoverDescription = "Advisory only — no independent exit reason is confirmed. No new buying; review stop-loss levels.",
            FinalActionReason = reason,
        };
        return new GateOutcome(gated, codes, d.FinalAction);
    }

    /// <summary>A watchlist entry for a ticker that is already held is an ADD: the same limit, sector and allocation-reliability checks apply.</summary>
    public static GateOutcome ConditionOwnedEntry(FinalActionDecision d, PortfolioEvidence e) => ConditionAdd(d, e);

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

        // Without a single-currency valuation no allocation percentage is trustworthy, so no limit can approve an Add.
        if (!e.AllocationReliable)
        {
            var unverified = d with
            {
                FinalAction = "Add / Needs Review",
                FinalActionClass = "ma-standby",
                FinalActionReason = Join(d.FinalActionReason,
                    $"Allocation cannot be verified: {e.Valuation?.Detail ?? "the valuation currency is not single or known"}. Position and sector limits were not checked. Not an instruction to buy. Original technical action: {d.FinalAction}."),
            };
            return new GateOutcome(unverified, [GateReasons.AllocationUnavailable], d.FinalAction);
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

    /// <summary>
    /// Adds observed stop-risk facts. A breach never becomes a claim that an order executed; stale or unavailable
    /// observations are reported as unverified, never as clear and never as breached.
    /// </summary>
    private static GateOutcome ApplyStopRisk(GateOutcome o, FinalActionDecision original, PortfolioEvidence e)
    {
        if (e.Risk is not { } r) return o;
        var codes = o.ReasonCodes.ToList();
        var d = o.Decision;
        var breached = r.PlannedBreached || r.BrokerBreached;

        if (!breached)
        {
            if (r.PlannedStopState == "Unverified" || r.PriceBasis is "Stale" or "Unavailable")
                codes.Add(GateReasons.StopEvidenceUnverified);
            if (r.CoverageState == "Partial") codes.Add(GateReasons.StopCoveragePartial);
            else if (r.CoverageState is "Unknown" or "Ambiguous") codes.Add(GateReasons.StopCoverageUnknown);
            return codes.Count == o.ReasonCodes.Count ? o : o with { ReasonCodes = codes };
        }

        var sb = new System.Text.StringBuilder();
        if (r.PlannedBreached)
        {
            codes.Add(GateReasons.PlannedStopBreached);
            sb.Append(string.Create(CultureInfo.InvariantCulture, $"Observed price {r.ObservedPrice:0.####} is at or below the Planned Stop {r.PlannedStop:0.####}. "));
        }
        if (r.BrokerBreached)
        {
            codes.Add(GateReasons.BrokerStopBreached);
            sb.Append(string.Create(CultureInfo.InvariantCulture, $"Observed price {r.ObservedPrice:0.####} is at or below the recorded broker stop {r.BrokerStop:0.####}. "));
        }
        sb.Append($"This is an observed price breach ({r.PriceBasis}, as of {r.ObservedAtUtc:yyyy-MM-dd HH:mm} UTC), not a confirmed brokerage execution: verify the actual order status at the broker. ");
        var verification = r.BrokerVerification.Count == 0 ? "no broker stop recorded" : string.Join(", ", r.BrokerVerification.Distinct());
        sb.Append($"Protection: {r.Protection}; coverage {r.CoverageState}; broker verification: {verification}.");
        var detail = sb.ToString();

        // An independently justified reduction stays as it is: the breach is added as evidence, never used to weaken it.
        var kind = Classify(d.FinalAction);
        if (kind is Kind.Trim or Kind.ProfitExit or Kind.LossExit or Kind.ProfitTake)
            return new GateOutcome(d with { FinalActionReason = Join(d.FinalActionReason, detail) }, codes, o.OriginalAction);

        var escalated = d with
        {
            FinalAction = ReviewStopBreach,
            FinalActionClass = FinalActionEngine.FinalActionClass(ReviewStopBreach),
            HoverDescription = "Observed price is at or below a recorded stop. Verify the brokerage order status; nothing has been sold by this application.",
            FinalActionReason = Join(d.FinalActionReason, detail)
                + $" Original action: {o.OriginalAction ?? original.FinalAction}.",
        };
        return new GateOutcome(escalated, codes, o.OriginalAction ?? original.FinalAction);
    }

    private static Kind Classify(string action)
    {
        var def = FinalActionCatalog.Describe(action);
        if (def.Group is not ("Portfolio" or "Base" or "Profit-taking")) return Kind.None;
        if (def.Group == "Profit-taking")
        {
            if (def.Category == "TrimExit") return Kind.ProfitTake;
            return def.Id == "PROFIT WATCH/NO ADD/TRAIL STOP" ? Kind.ProfitWatch : Kind.None;
        }
        if (def.Category == "Entry") return Kind.Add;
        if (def.Category != "TrimExit" || def.Id == "TRIM/EXIT WATCH") return Kind.None;
        if (LossExitIds.Contains(def.Id)) return Kind.LossExit;
        if (ProfitExitIds.Contains(def.Id)) return Kind.ProfitExit;
        return Kind.Trim;
    }

    private static string Join(string? existing, string text) => string.IsNullOrEmpty(existing) ? text : $"{existing} {text}";
}
