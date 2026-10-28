namespace PortfolioManager.Api.Services.FinalAction;

public sealed record PriorityEligibility(bool Eligible, string PriorityLevel, IReadOnlyList<string> ReasonsExcluded);

/// <summary>
/// Backend port of <c>resolvePriorityCandidateEligibility</c> (priority-candidate-eligibility.ts): a scored
/// watchlist candidate is eligible only when its canonical Watchlist Final Action qualifies for new capital,
/// there is no hard structural negative and the score clears the watch threshold. One definition, used by the
/// Action Center widget and the Signals Audit alike.
/// </summary>
public static class PriorityCandidateEligibility
{
    public const decimal ScoreThresholdWatch = 50m;
    public const decimal ScoreThresholdHigh = 75m;

    private static readonly HashSet<string> EligibleActions =
        ["ENTRY CANDIDATE", "STARTER ENTRY", "BUY WATCH", "REVERSAL WATCH"];

    private static readonly Dictionary<string, string> ReasonByAction = new()
    {
        ["WATCH/NO CHASE"] = "FINAL_ACTION_WATCH_NO_CHASE",
        ["WAIT FOR PULLBACK"] = "FINAL_ACTION_WAIT_FOR_PULLBACK",
        ["WAIT FOR REVERSAL"] = "FINAL_ACTION_WAIT_FOR_REVERSAL",
        ["WAIT FOR RECLAIM"] = "FINAL_ACTION_WAIT_FOR_RECLAIM",
        ["AVOID"] = "FINAL_ACTION_AVOID",
        ["WATCH"] = "FINAL_ACTION_WATCH",
        ["AWAITING DATA"] = "AWAITING_DATA",
    };

    /// <param name="decision">The candidate's canonical watchlist decision, or null when it has no technical data.</param>
    public static PriorityEligibility Resolve(FinalActionResultDto? decision, decimal totalScore)
    {
        if (decision is null) return new PriorityEligibility(false, "NONE", ["MISSING_TECHNICAL_DATA"]);

        var reasons = new List<string>();
        if (decision.HasHardStructuralNegative) reasons.Add("HARD_NEGATIVE_STRUCTURE");
        if (!EligibleActions.Contains(decision.FinalActionId))
            reasons.Add(ReasonByAction.GetValueOrDefault(decision.FinalActionId, "FINAL_ACTION_INELIGIBLE"));
        if (totalScore < ScoreThresholdWatch) reasons.Add("SCORE_BELOW_THRESHOLD");

        var eligible = reasons.Count == 0;
        var level = !eligible ? "NONE" : totalScore >= ScoreThresholdHigh ? "HIGH" : "WATCH";
        return new PriorityEligibility(eligible, level, reasons);
    }
}
