using System.Text.Json;

namespace PortfolioManager.Api.Models;

/// <summary>
/// One historical recommendation captured point-in-time. Facts (date, price, score, role, source,
/// context) are written once at capture and never rewritten; only the investor-decision fields and
/// <see cref="EpisodeEndedDate"/> change afterwards.
/// </summary>
public class SignalAuditEvent
{
    public long Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    /// <summary>Trading ticker as shown in the app (e.g. LMT.TO).</summary>
    public string Symbol { get; set; } = string.Empty;
    /// <summary>Ticker whose prices the signal price was measured against (differs from Symbol for CDRs).</summary>
    public string AnalysisSymbol { get; set; } = string.Empty;
    public string? Currency { get; set; }
    /// <summary>Stable canonical key from <see cref="SignalTypeCatalog"/>, never a display label.</summary>
    public string SignalType { get; set; } = string.Empty;
    public string SignalCategory { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    /// <summary>Toronto market date of the signal.</summary>
    public DateOnly SignalDate { get; set; }
    public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;
    public decimal SignalPrice { get; set; }
    public decimal? Score { get; set; }
    public string? Role { get; set; }
    /// <summary>Portfolio | Watchlist, classified at signal time.</summary>
    public string Source { get; set; } = string.Empty;
    /// <summary>Live (captured going forward) | Backfill (recovered from original point-in-time records).</summary>
    public string Origin { get; set; } = SignalOrigins.Live;
    public string? RuleVersion { get; set; }
    /// <summary>Deterministic episode id: type|symbol|yyyyMMdd|sequence. Unique per user.</summary>
    public string EpisodeKey { get; set; } = string.Empty;
    /// <summary>Set when the qualifying condition stops holding; null while the episode is active.</summary>
    public DateOnly? EpisodeEndedDate { get; set; }
    /// <summary>Point-in-time score breakdown and technical facts as captured.</summary>
    public string? ContextJson { get; set; }

    public string MyAction { get; set; } = SignalMyActions.Pending;
    public string? MyActionNote { get; set; }
    public DateTime? MyActionUpdatedUtc { get; set; }
    /// <summary>FK-by-convention to PortfolioItems.Id. The transaction itself is never modified.</summary>
    public int? LinkedTransactionId { get; set; }

    /// <summary>Verified = passed the same Final Action eligibility the Action Center applies at capture; Unverified = captured before that check existed.</summary>
    public string EligibilityStatus { get; set; } = SignalEligibility.Unverified;
    /// <summary>The provider's close for <see cref="SignalDate"/> when captured; later compared to detect split/adjustment basis changes.</summary>
    public decimal? BasisClose { get; set; }

    /// <summary>System lifecycle (see <see cref="SignalStatuses"/>); independent of <see cref="MyAction"/>.</summary>
    public string Status { get; set; } = SignalStatuses.Active;
    public DateTime? StatusChangedAtUtc { get; set; }
    public string? StatusReason { get; set; }
    /// <summary>The newer recommendation that replaced this one. The predecessor is found by querying this column.</summary>
    public long? SupersededByEventId { get; set; }
    public SignalAuditEvent? SupersededBy { get; set; }
    /// <summary>Last complete, successful EOD evaluation that still produced this recommendation. Null = never revalidated.</summary>
    public DateTime? LastValidatedAtUtc { get; set; }
    /// <summary>Counts in performance analytics only. Never affects Action Center eligibility.</summary>
    public bool PerformanceAudited { get; set; } = true;
}

public static class SignalStatuses
{
    public const string Active = "Active";
    public const string Acted = "Acted";
    public const string Dismissed = "Dismissed";
    public const string Expired = "Expired";
    public const string Invalidated = "Invalidated";
    public const string Superseded = "Superseded";

    public static readonly IReadOnlyList<string> All = [Active, Acted, Dismissed, Expired, Invalidated, Superseded];
    public static bool IsValid(string? s) => s is not null && All.Contains(s);
}

/// <summary>The only place allowed to change <see cref="SignalAuditEvent.Status"/>. Terminal states never reopen.</summary>
public static class RecommendationLifecycle
{
    public static bool CanTransition(string from, string to) =>
        from == SignalStatuses.Active && to != SignalStatuses.Active && SignalStatuses.IsValid(to);

    public static bool TryTransition(SignalAuditEvent ev, string to, string reason, DateOnly today, DateTime nowUtc, SignalAuditEvent? supersededBy = null)
    {
        if (!CanTransition(ev.Status, to)) return false;
        ev.Status = to;
        ev.StatusChangedAtUtc = nowUtc;
        ev.StatusReason = reason.Length > 200 ? reason[..200] : reason;
        if (to == SignalStatuses.Superseded) ev.SupersededBy = supersededBy;
        // Acted / Dismissed are the user's call and keep the legacy "episode still running" semantics until the condition ends.
        if (to is not (SignalStatuses.Acted or SignalStatuses.Dismissed)) ev.EpisodeEndedDate ??= today;
        return true;
    }

    /// <summary>Entry/Add 5 and Trim/Exit/Risk 3 weekdays without a successful revalidation.</summary>
    public static int ExpiryWindowDays(string category) => category == SignalCategories.Entry ? 5 : 3;

    public static int WeekdaysBetween(DateOnly from, DateOnly to)
    {
        var n = 0;
        for (var d = from.AddDays(1); d <= to; d = d.AddDays(1))
            if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) n++;
        return n;
    }
}

public static class SignalEligibility
{
    public const string Verified = "Verified";
    public const string Unverified = "Unverified";
}

public static class SignalPriceBasis
{
    /// <summary>Provider history still matches the price basis the signal was recorded on.</summary>
    public const string Verified = "Verified";
    /// <summary>History was re-based (e.g. split); the signal price was scaled by the same factor before computing returns.</summary>
    public const string Adjusted = "Adjusted";
    /// <summary>No reference close to compare, so the basis could not be proven consistent.</summary>
    public const string Unverified = "Unverified";
}

public static class SignalOrigins
{
    public const string Live = "Live";
    public const string Backfill = "Backfill";
    /// <summary>
    /// Already qualifying when auditing first ran. Dated with the actual capture date: it is NOT a genuine new
    /// transition and says nothing about when the recommendation first appeared.
    /// </summary>
    public const string InitialBaseline = "INITIAL_BASELINE";
}

public static class SignalSources
{
    public const string Portfolio = "Portfolio";
    public const string Watchlist = "Watchlist";
}

public static class SignalMyActions
{
    public const string Pending = "Pending";
    public const string Actioned = "Actioned";
    public const string Partial = "Partial";
    public const string NotActioned = "Not Actioned";

    public static readonly IReadOnlyList<string> All = [Pending, Actioned, Partial, NotActioned];

    public static bool IsValid(string? value) => value is not null && All.Contains(value);
    public static bool IsFollowed(string value) => value is Actioned or Partial;
}

public static class SignalCategories
{
    public const string Entry = "Entry";
    public const string TrimExit = "TrimExit";
    public const string Advisory = "Advisory";
}

public static class SignalDirections
{
    public const string Buy = "Buy";
    public const string Sell = "Sell";
    public const string Neutral = "Neutral";
}

public sealed record SignalTypeDefinition(string Key, string Label, string Category, string Direction);

/// <summary>
/// Single backend catalog of auditable signal types. Category/direction metadata lives here so
/// dashboard analytics never infer meaning from names. Final Action types are added here when
/// Final Action capture ships (the Final Action engine currently runs only in the Angular app).
/// </summary>
public static class SignalTypeCatalog
{
    public const string PriorityCandidateKey = "PRIORITY_CANDIDATE";

    public static readonly SignalTypeDefinition PriorityCandidate = new(
        PriorityCandidateKey, "Priority Candidate", SignalCategories.Entry, SignalDirections.Buy);

    public static IReadOnlyList<SignalTypeDefinition> All { get; } = [PriorityCandidate];

    /// <summary>Prefix of Final Action signal types: <c>FA:&lt;canonical action id&gt;</c>.</summary>
    public const string FinalActionPrefix = "FA:";

    public static string FinalActionKey(string canonicalId) => FinalActionPrefix + canonicalId;

    public static SignalTypeDefinition? Find(string key)
    {
        var fixedType = All.FirstOrDefault(d => string.Equals(d.Key, key, StringComparison.OrdinalIgnoreCase));
        if (fixedType is not null) return fixedType;
        if (!key.StartsWith(FinalActionPrefix, StringComparison.Ordinal)) return null;
        var def = Services.FinalAction.FinalActionCatalog.Describe(key[FinalActionPrefix.Length..]);
        return new SignalTypeDefinition(key, def.Label, def.Category, def.Direction);
    }
}

/// <summary>Per-user capture settings, persisted as JSON in UserPreferences.</summary>
public sealed record SignalsAuditSettings
{
    public const string PreferenceKey = "signalsAudit.settings";
    public const decimal DefaultThreshold = 75m;

    public bool PriorityEnabled { get; init; } = true;
    /// <summary>A candidate qualifies when its score is strictly greater than this value.</summary>
    public decimal PriorityThreshold { get; init; } = DefaultThreshold;
    /// <summary>
    /// Symbols already qualifying when auditing was (re)activated or the threshold changed. They do not
    /// generate events until they leave the qualifying set, so activation never floods history.
    /// </summary>
    public List<string> BaselineSymbols { get; init; } = [];
    public DateTime? ActivatedAtUtc { get; init; }

    /// <summary>Master switch for Final Action auditing.</summary>
    public bool FinalActionEnabled { get; init; } = true;
    /// <summary>
    /// The audited Final Action list: user-editable (add / edit / delete / enable). Null means "never saved",
    /// which resolves to the six business defaults.
    /// </summary>
    public List<AuditedActionEntry>? Entries { get; init; }
    /// <summary><c>SYMBOL|ACTION_ID</c> pairs already present at activation; suppressed until they leave and re-enter.</summary>
    public List<string> FinalActionBaseline { get; init; } = [];
    /// <summary>True until a capture records the current pairs as baseline (activation without a live evaluation).</summary>
    public bool FinalActionBaselineNeeded { get; init; } = true;
    /// <summary>
    /// False until the first successful capture records everything that already qualifies as INITIAL_BASELINE
    /// events. After that only genuine transitions are captured.
    /// </summary>
    public bool InitialBaselineDone { get; init; }
    /// <summary>
    /// False until the first lifecycle-aware EOD capture. Unaudited recommendations already present at that point
    /// are recorded as INITIAL_BASELINE (not as new Live transitions).
    /// </summary>
    public bool LifecycleBaselineDone { get; init; }

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
}

/// <summary>One row of the audited Final Action list. <see cref="Id"/> is canonical; <see cref="Label"/> is display only.</summary>
public sealed record AuditedActionEntry(string Id, string Label, bool Enabled, bool Custom);

public sealed record AuditedActionEntryDto(string Id, string Label, bool Enabled, bool Custom, bool KnownToRules, string Category, string Direction);

public sealed record SignalsAuditSettingsDto(
    bool PriorityEnabled,
    decimal PriorityThreshold,
    DateTime? ActivatedAtUtc,
    int BaselineCount,
    bool FinalActionEnabled,
    IReadOnlyList<AuditedActionEntryDto> Entries,
    int FinalActionBaselineCount);

public sealed record SaveSignalsAuditSettingsRequest(
    bool PriorityEnabled,
    decimal PriorityThreshold,
    bool? FinalActionEnabled = null,
    IReadOnlyList<AuditedActionEntry>? Entries = null);

public sealed record FinalActionCatalogItemDto(string Id, string Label, string Category, string Direction, string Group);

public sealed record SignalTypeOptionDto(string Key, string Label, string Category, string Direction);

public sealed record SignalHorizonDto(decimal? Pct, string? EvalDate, decimal? EvalPrice, string Status);

public sealed record SignalAuditRowDto(
    long Id,
    string Symbol,
    string? Role,
    string SignalDate,
    int DaysSince,
    bool DateAnomaly,
    string SignalType,
    string SignalTypeLabel,
    string Category,
    string Direction,
    string Source,
    decimal SignalPrice,
    string? Currency,
    decimal? Score,
    SignalHorizonDto Return5D,
    SignalHorizonDto Return20D,
    SignalHorizonDto Return1M,
    SignalHorizonDto Today,
    string MyAction,
    bool HasNote,
    int? LinkedTransactionId,
    string LinkKind,
    string Origin,
    bool EpisodeActive,
    string EligibilityStatus,
    string PriceBasis,
    decimal? ActionedPrice = null,
    decimal? TodayVsActionedPct = null,
    string Status = "Active",
    bool PerformanceAudited = true,
    DateTime? LastValidatedAtUtc = null,
    string? StatusReason = null,
    long? SupersededByEventId = null);

public sealed record SignalAuditPagedResponse(
    IReadOnlyList<SignalAuditRowDto> Items,
    int Total,
    int Page,
    int PageSize);

public sealed record SignalEffectivenessRowDto(
    string SignalType,
    string Label,
    string Direction,
    int Count,
    int Observations5D,
    decimal? Avg5D,
    int Observations20D,
    decimal? Avg20D,
    int Observations1M,
    decimal? Avg1M,
    int FavourableObservations,
    decimal? FavourablePct,
    int ActionedCount);

public sealed record SignalAuditSummaryDto(
    int Total,
    int PriorityCandidate,
    int EntryAdd,
    int TrimExit,
    int Actioned,
    int PendingReview,
    IReadOnlyList<SignalEffectivenessRowDto> Effectiveness);

public sealed record SignalLinkedTransactionDto(
    int Id,
    string Symbol,
    string? TransactionType,
    string? AccountType,
    string? OpenDate,
    string? CloseDate,
    decimal Shares,
    decimal Price,
    string? DecisionSource);

public sealed record SignalAuditDetailDto(
    SignalAuditRowDto Row,
    DateTime CapturedAtUtc,
    string? RuleVersion,
    string? Note,
    DateTime? MyActionUpdatedUtc,
    DateOnly? EpisodeEndedDate,
    JsonElement? Context,
    SignalLinkedTransactionDto? LinkedTransaction);

public sealed record SignalTransactionCandidateDto(
    int Id,
    string Symbol,
    string? TransactionType,
    string? AccountType,
    string? Date,
    decimal Shares,
    decimal Price,
    string? DecisionSource,
    int DaysFromSignal,
    bool IsLinked);

public sealed record SaveSignalActionRequest(string MyAction, string? Note, int? LinkedTransactionId);

public sealed record DismissSignalRequest(string? Note);

public sealed record SignalCaptureResultDto(int Created, int Closed, bool Skipped, string? Reason);
