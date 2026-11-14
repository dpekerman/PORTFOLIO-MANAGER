using System.Globalization;

namespace PortfolioManager.Api.Services.FinalAction;

/// <summary>
/// One short, understandable rationale for consequential portfolio decisions (actionable Add / Exit, "Hold / No Add",
/// warnings). Ordinary neutral Holds stay empty. Pure: it only describes what the engine and the evidence gate already
/// decided and never changes an action. Gate outcomes keep their own reason codes and text.
/// </summary>
public static class FinalActionExplainer
{
    public const string AddEligible = "ADD_ELIGIBLE";
    public const string ProfitTakingRule = "PROFIT_TAKING_RULE";
    public const string NoAddCooling = "NO_ADD_TECHNICAL_COOLING";
    public const string NoAddOverbought = "NO_ADD_OVERBOUGHT";
    public const string NoAddTechnical = "NO_ADD_TECHNICAL";
    public const string WarningNotInstruction = "WARNING_NOT_INSTRUCTION";

    private static string F(double? v) => v is { } x ? x.ToString("0.0", CultureInfo.InvariantCulture) : "n/a";

    public static (IReadOnlyList<string> Codes, string? Rationale) ExplainPortfolio(
        FinalActionDecision d, GateOutcome gate, PortfolioEvidence e, string? role)
    {
        // The gate already explained anything it passed, changed or conditioned.
        if (gate.ReasonCodes.Count > 0) return (gate.ReasonCodes, d.FinalActionReason);

        var def = FinalActionCatalog.Describe(d.FinalAction);
        var roleName = string.IsNullOrWhiteSpace(role) ? "Strategic" : role;

        if (def.Category == "Entry" && def.Group is "Portfolio" or "Base")
        {
            var limit = e.PositionLimitPct is { } l
                ? $"within the {roleName} limit of {F(l)}%"
                : $"no position limit is configured for {roleName}";
            var sector = !e.SectorKnown ? "sector unknown, no sector limit applied"
                : e.SectorTargetPct is { } t ? $"sector {F(e.SectorPct)}% vs target {F(t)}%" : "no sector target configured";
            return ([AddEligible],
                $"Add eligible: {d.MomentumShift} / {d.TrendSetup}. Symbol exposure {F(e.PositionSizePct)}% ({limit}); {sector}.");
        }

        if (def.Category == "TrimExit" && def.Group == "Profit-taking")
            return ([ProfitTakingRule], $"Profit-taking rule: unrealized gain {F(e.UnrealizedGainPct)}% on this lot. {d.FinalAction}.");

        if (d.FinalAction == "Hold / No Add")
        {
            var code = d.TrendSetup == "Cooling" ? NoAddCooling
                : d.MomentumShift.Contains("Overbought", StringComparison.Ordinal) ? NoAddOverbought : NoAddTechnical;
            var why = code switch
            {
                NoAddCooling => "short-term momentum is cooling (EMA10 below EMA20)",
                NoAddOverbought => "the position is extended after an overbought run",
                _ => "the technical setup does not support new buying",
            };
            return ([code], $"No add: {why}. Existing position is held.");
        }

        if (d.FinalAction is "Hold / Risk Review" or "Hold / Technical Caution" or "Hold / Review")
            return ([WarningNotInstruction],
                $"Risk warning ({d.MomentumShift} / {d.TrendSetup}). A review reminder, not an instruction to trade.");

        return ([], null);
    }
}
