namespace PortfolioManager.Api.Services.Opportunities;

/// <summary>
/// Every threshold of the PM-029 opportunity scoring, sector heat, trade setup and fit rules. Changing ANY value requires a new
/// <see cref="Version"/> so that stored snapshots stay reproducible. Specification: docs/pm-029-trading-opportunities.md.
/// Deterministic heuristics — not a statistically calibrated probability.
/// </summary>
public static class OpportunityScoringConfig
{
    public const string Version = "pm029-v2";
    public const string LegacyVersion = "pm029-v1";
    public const string SwingVersion = Version;
    public const string StrategicVersion = LegacyVersion;
    public const decimal SwingDenominatorMax = 80m;

    public const string Disclaimer =
        "Swing-v2 is an evidence-adjusted technical ranking, not a probability of success or an executable buy recommendation.";

    public const string LegacyDisclaimer =
        "Heuristic setup score (pm029-v1). Not a probability of success; not statistically validated.";

    public static string DisclaimerFor(string? version) =>
        string.Equals(version, LegacyVersion, StringComparison.Ordinal) ? LegacyDisclaimer : Disclaimer;

    // ── Status (§3.3) ────────────────────────────────────────────────────────
    public const decimal MinCompletenessPct = 70m;

    // ── Bands (§3.4), lower bounds inclusive ─────────────────────────────────
    public const int StrongMin = 85;
    public const int AttractiveMin = 70;
    public const int ModerateMin = 60;
    public const int CautionMin = 50;

    // ── Fundamentals ─────────────────────────────────────────────────────────
    public static readonly TimeSpan FundamentalsMaxAge = TimeSpan.FromDays(7);
    public const decimal F1Max = 3m, F2Max = 2m, F3Max = 1m, F4Max = 3m, F5Max = 1m;
    public const int MinValuationFactors = 2;
    public const int MinPiotroskiSignals = 4;

    // ── Sector Heat (§4) ─────────────────────────────────────────────────────
    public const int ShortWindowSessions = 20;
    public const int LongWindowSessions = 60;
    public const int ShortWindowMaxMissing = 1;
    public const int LongWindowMaxMissing = 3;
    public const decimal NeutralBandPp = 0.5m;
    public const decimal Intensity2Pp = 2.0m;
    public const decimal Intensity3Pp = 5.0m;
    /// <summary>A single-session move larger than this inside a window suggests an unadjusted split or other corporate action.</summary>
    public const decimal CorporateActionMovePct = 40m;
    public const string SectorMethodology = "Price return (split-adjusted, dividends excluded)";

    // ── Trade setup (§5) ─────────────────────────────────────────────────────
    public const decimal MinStopAtr = 0.5m;
    public const decimal MaxStopAtr = 4.0m;
    public const decimal MaxRiskPct = 15m;
    public const decimal MinTargetAtr = 0.5m;
    public const decimal ActionableRewardRisk = 1.5m;

    // ── Risk category (§5) ───────────────────────────────────────────────────
    public const decimal RiskLowMaxPct = 5m, RiskMediumMaxPct = 10m;
    public const decimal AtrLowMaxPct = 2.5m, AtrMediumMaxPct = 5m;

    // ── Portfolio Fit (§6) ───────────────────────────────────────────────────
    /// <summary>Same tolerance FinalActionService.BuildEvidence uses for SectorOverTarget.</summary>
    public const double SectorTolerancePp = 2.0;
    /// <summary>Position headroom below this many pp (but above zero) is Review rather than Eligible.</summary>
    public const double PositionNearLimitPp = 1.0;
}
