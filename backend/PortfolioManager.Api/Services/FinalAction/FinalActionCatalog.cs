using System.Text.RegularExpressions;

namespace PortfolioManager.Api.Services.FinalAction;

public sealed record FinalActionDefinition(string Id, string Label, string Category, string Direction, string Group);

/// <summary>
/// Single backend catalog of every Final Action the rules can emit. Identifiers are canonical (upper-case,
/// spacing around "/" removed) so persistence never depends on a display label. Category / direction are
/// declared here, never inferred from words, and drive audit analytics. A parity test fails if the engine
/// ever emits an action that is missing from this catalog.
/// </summary>
public static partial class FinalActionCatalog
{
    private const string Entry = "Entry";
    private const string TrimExit = "TrimExit";
    private const string Advisory = "Advisory";
    private const string Buy = "Buy";
    private const string Sell = "Sell";
    private const string Neutral = "Neutral";

    private static readonly (string Label, string Category, string Direction, string Group)[] Rows =
    [
        // Watchlist entry status
        ("ENTRY CANDIDATE", Entry, Buy, "Watchlist"),
        ("STARTER ENTRY", Entry, Buy, "Watchlist"),
        ("BUY WATCH", Advisory, Neutral, "Watchlist"),
        ("REVERSAL WATCH", Advisory, Neutral, "Watchlist"),
        ("WATCH / NO CHASE", Advisory, Neutral, "Watchlist"),
        ("WAIT FOR PULLBACK", Advisory, Neutral, "Watchlist"),
        ("WAIT FOR REVERSAL", Advisory, Neutral, "Watchlist"),
        ("WAIT FOR RECLAIM", Advisory, Neutral, "Watchlist"),
        ("AVOID", Advisory, Neutral, "Watchlist"),
        ("WATCH", Advisory, Neutral, "Watchlist"),

        // Portfolio role actions
        ("Add to Position", Entry, Buy, "Portfolio"),
        ("Add / Staged Entry", Entry, Buy, "Portfolio"),
        ("Add on Pullback", Entry, Buy, "Portfolio"),
        ("Add to Trade", Entry, Buy, "Portfolio"),
        ("Small Add", Entry, Buy, "Portfolio"),
        ("Hold / Add If Underweight", Advisory, Neutral, "Portfolio"),
        ("Hold / Add on Pullback", Advisory, Neutral, "Portfolio"),
        ("Hold / Watch for Add", Advisory, Neutral, "Portfolio"),
        ("Hold / No Chase / Profit Watch", Advisory, Neutral, "Portfolio"),
        ("No Chase / Extended / Profit Watch", Advisory, Neutral, "Portfolio"),
        ("No Chase / Profit Watch", Advisory, Neutral, "Portfolio"),
        ("Hold / No New Buy", Advisory, Neutral, "Portfolio"),
        ("Hold / No Add", Advisory, Neutral, "Portfolio"),
        ("Hold / No Chase", Advisory, Neutral, "Portfolio"),
        ("Hold / Review", Advisory, Neutral, "Portfolio"),
        ("Hold / Risk Review", Advisory, Neutral, "Portfolio"),
        ("Hold / Technical Caution", Advisory, Neutral, "Portfolio"),
        ("Hold / Trail Stop", Advisory, Neutral, "Portfolio"),
        ("Hold", Advisory, Neutral, "Portfolio"),
        ("Hold / Monitor", Advisory, Neutral, "Portfolio"),
        ("Add / Needs Review", Advisory, Neutral, "Portfolio"),
        ("Hold / Catalyst Monitoring", Advisory, Neutral, "Portfolio"),
        ("Hold Swing",  Advisory, Neutral, "Portfolio"),
        ("Hold Swing / Watch", Advisory, Neutral, "Portfolio"),
        ("Hold Spec", Advisory, Neutral, "Portfolio"),
        ("Hold Spec / Watch", Advisory, Neutral, "Portfolio"),
        ("Hold Spec / Trail Stop", Advisory, Neutral, "Portfolio"),
        ("Hold Option", Advisory, Neutral, "Portfolio"),
        ("Trim / Review", TrimExit, Sell, "Portfolio"),
        ("Trim / Exit Watch", TrimExit, Sell, "Portfolio"),
        ("Trim / Trail Stop", TrimExit, Sell, "Portfolio"),
        ("Trail Stop / Trim", TrimExit, Sell, "Portfolio"),
        ("Exit / Take Profit", TrimExit, Sell, "Portfolio"),
        ("Exit / Cut Loss", TrimExit, Sell, "Portfolio"),
        ("Cut Loss / Exit", TrimExit, Sell, "Portfolio"),
        ("Cut Loss / Thesis Review", TrimExit, Sell, "Portfolio"),
        ("Take Profit / Exit", TrimExit, Sell, "Portfolio"),
        ("Close / Take Profit", TrimExit, Sell, "Portfolio"),
        ("Close / Watch for Reversal", TrimExit, Sell, "Portfolio"),
        ("Close / Protect", TrimExit, Sell, "Portfolio"),
        ("Roll / Extend", Advisory, Neutral, "Portfolio"),
        ("Trail / Protect Gains", Advisory, Neutral, "Portfolio"),

        // Evaluation status (never a trading decision)
        ("Awaiting Data", Advisory, Neutral, "Status"),

        // Profit-taking and risk-control overrides
        ("Take Partial Profit / No Chase", TrimExit, Sell, "Profit-taking"),
        ("Take Partial Profit / Protect Gain", TrimExit, Sell, "Profit-taking"),
        ("Sell Majority / Keep Small Runner", TrimExit, Sell, "Profit-taking"),
        ("Sell 50% / Keep Runner / Rebuy on Pullback", TrimExit, Sell, "Profit-taking"),
        ("Trim 33–50% / Trail Remainder", TrimExit, Sell, "Profit-taking"),
        ("Trim 33–50% / Keep Runner", TrimExit, Sell, "Profit-taking"),
        ("Trim 25% / Keep Runner", TrimExit, Sell, "Profit-taking"),
        ("Trim 25–40% / Hold Runner", TrimExit, Sell, "Profit-taking"),
        ("Trim 20–33% / Hold Runner", TrimExit, Sell, "Profit-taking"),
        ("Trim 10–20% / Hold Runner", TrimExit, Sell, "Profit-taking"),
        ("Profit Watch / No Add / Trail Stop", Advisory, Neutral, "Profit-taking"),
        ("Hold Runner", Advisory, Neutral, "Profit-taking"),
        ("Risk Control: N% Closed — Hold or Exit Remaining", Advisory, Neutral, "Risk control"),
        ("Risk Control: N% Closed — Hold Runner", Advisory, Neutral, "Risk control"),
        ("Risk Control Started: N% Closed — Monitor Position", Advisory, Neutral, "Risk control"),

        // Base momentum actions (roles without a role table, scanner)
        ("Confirmed Buy Signal", Entry, Buy, "Base"),
        ("Buy / Accumulate", Entry, Buy, "Base"),
        ("Accumulate Starter", Entry, Buy, "Base"),
        ("Confirmed Sell Signal", TrimExit, Sell, "Base"),
        ("Reduce / Review", TrimExit, Sell, "Base"),
        ("Early Buy Watch", Advisory, Neutral, "Base"),
        ("Early Sell Watch", Advisory, Neutral, "Base"),
        ("Watch / Do Not Chase", Advisory, Neutral, "Base"),
        ("Avoid / Wait", Advisory, Neutral, "Base"),
        ("Hold Longs", Advisory, Neutral, "Base"),
        ("Stand By", Advisory, Neutral, "Base"),

        // Gap and Fibonacci modifiers ("Watch / No Chase" from the guard shares an id with the watchlist status above)
        ("Watch / Gap Failed — No Buy", Advisory, Neutral, "Modifier"),
        ("Watch / Starter Only (Gap Weak)", Advisory, Neutral, "Modifier"),
        ("Watch / Gap Strong (Confirm Setup)", Advisory, Neutral, "Modifier"),
        ("Wait", Advisory, Neutral, "Modifier"),
        ("Avoid / Trend Damage", Advisory, Neutral, "Modifier"),
    ];

    /// <summary>The six actions the business asked to audit by default, exactly as specified.</summary>
    public static IReadOnlyList<string> DefaultAuditedLabels { get; } =
        ["STARTER ENTRY", "ADD/STAGGED ENTRY", "SMALL ADD", "TRIM/EXIT", "TRIM/REVIEW", "CUT LOSS/EXIT"];

    /// <summary>Known spelling variants mapped to the canonical id the rules emit.</summary>
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.Ordinal)
    {
        ["ADD/STAGGED ENTRY"] = "ADD/STAGED ENTRY",
    };

    public static IReadOnlyList<FinalActionDefinition> All { get; } = Rows
        .Select(r => new FinalActionDefinition(CanonicalId(r.Label), r.Label, r.Category, r.Direction, r.Group))
        .ToList();

    private static readonly Dictionary<string, FinalActionDefinition> ById =
        All.ToDictionary(d => d.Id, StringComparer.Ordinal);

    [GeneratedRegex(@"\s*/\s*")]
    private static partial Regex SlashSpacing();
    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
    [GeneratedRegex(@"\d+(?:\.\d+)?%(?= Closed)")]
    private static partial Regex RiskControlPercent();

    /// <summary>Stable identifier for an action string: upper-case, no spaces around "/", dynamic risk-control % replaced by N%.</summary>
    public static string CanonicalId(string action)
    {
        var s = action.Trim();
        if (s.StartsWith("Risk Control", StringComparison.OrdinalIgnoreCase)) s = RiskControlPercent().Replace(s, "N%");
        s = SlashSpacing().Replace(s, "/");
        s = Whitespace().Replace(s, " ").ToUpperInvariant();
        return Aliases.GetValueOrDefault(s, s);
    }

    public static FinalActionDefinition? Find(string actionOrId) => ById.GetValueOrDefault(CanonicalId(actionOrId));

    public static bool IsKnown(string actionOrId) => ById.ContainsKey(CanonicalId(actionOrId));

    /// <summary>Metadata for an id; user-defined ids the rules never emit are Advisory / Neutral.</summary>
    public static FinalActionDefinition Describe(string actionOrId)
    {
        var id = CanonicalId(actionOrId);
        return ById.GetValueOrDefault(id) ?? new FinalActionDefinition(id, actionOrId.Trim(), Advisory, Neutral, "Custom");
    }
}
