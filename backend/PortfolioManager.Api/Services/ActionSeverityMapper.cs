namespace PortfolioManager.Api.Services;

public static class ActionSeverityMapper
{
    public static string Get(string action, bool allocationBlocked = false)
    {
        if (allocationBlocked || action is "ENTRY CANDIDATE" or "ADD CANDIDATE" or "REVIEW" or "TRIM" or "EXIT REVIEW" or "STOP/EXIT" or "STOP / EXIT" or "DO NOT CHASE" or "TECHNICAL REVIEW")
            return "REQUIRED";
        if (action is "BUY WATCH" or "ADD WATCH" or "REVERSAL WATCH" or "WATCH CHANNEL" or "WAIT FOR REVERSAL" or "TRIM WATCH" or "TECHNICAL CAUTION")
            return "DEVELOPING";
        return action == "AVOID" ? "INFORMATIONAL" : "INFORMATIONAL";
    }
}