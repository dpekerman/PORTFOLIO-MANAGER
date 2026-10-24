namespace PortfolioManager.Api.Models;

/// <summary>Single-row settings for the AI pre-market agent. Id is always 1.</summary>
public class PreMarketAgentConfig
{
    public int Id { get; set; } = 1;
    public bool ScheduleEnabled { get; set; }
    /// <summary>America/Toronto local start time, HH:mm.</summary>
<<<<<<< HEAD
    public string StartTimeLocal { get; set; } = "07:30";
=======
    public string StartTimeLocal { get; set; } = "07:00";
>>>>>>> e274b8de818b34342deac708b3471d1570cf03bb
    /// <summary>Scheduler ignores days before this date.</summary>
    public DateOnly? FirstRunDate { get; set; }
    /// <summary>Test override: treated as "today" for data cut-off, report date and run key.</summary>
    public DateOnly? AsOfDate { get; set; }
    /// <summary>America/Toronto local hard SMTP cutoff, HH:mm.</summary>
    public string DeadlineLocal { get; set; } = "09:25";
    public bool ShadowMode { get; set; } = true;
    /// <summary>Owner of the holdings/watchlist analysed; null = all users.</summary>
    public string? UserId { get; set; }
<<<<<<< HEAD
    /// <summary>APPROVED_ONLY is the only mode implemented; SMART/FULL need a verified free search source.</summary>
    public string DiscoveryMode { get; set; } = "APPROVED_ONLY";
    public bool AiEnabled { get; set; } = true;
    /// <summary>Upper bound of Ollama calls per run (CPU budget).</summary>
    public int AiMaxCalls { get; set; } = 20;
=======
>>>>>>> e274b8de818b34342deac708b3471d1570cf03bb
}

public class PreMarketAgentRun
{
    public int Id { get; set; }
    /// <summary>Idempotency key: yyyy-MM-dd|REAL or yyyy-MM-dd|TEST.</summary>
    public string RunKey { get; set; } = string.Empty;
    public DateOnly AsOfDate { get; set; }
    public bool IsTestRun { get; set; }
    /// <summary>RUNNING | COMPLETE | PARTIAL | FAILED</summary>
    public string Status { get; set; } = "RUNNING";
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
    public int DurationMs { get; set; }
    public string? ReportJson { get; set; }
    public string? ReportHtml { get; set; }
<<<<<<< HEAD
    /// <summary>NOT_SENT | SMTP_ACCEPTED | FAILED | SKIPPED_DEADLINE. SMTP acceptance is not confirmed delivery.</summary>
=======
    /// <summary>NOT_SENT | SMTP_ACCEPTED | FAILED. SMTP acceptance is not confirmed delivery.</summary>
>>>>>>> e274b8de818b34342deac708b3471d1570cf03bb
    public string EmailStatus { get; set; } = "NOT_SENT";
    public string? EmailMessage { get; set; }
    public DateTime? EmailSubmittedAtUtc { get; set; }
    public string? Error { get; set; }
<<<<<<< HEAD
    public string? AiModel { get; set; }
    public int AiCalls { get; set; }
    public int AiDurationMs { get; set; }
    public int NewsDurationMs { get; set; }
    public int ArticlesRetrieved { get; set; }
}

public class NewsSourceConfiguration
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>https URL. Company sources may contain {symbol}.</summary>
    public string Url { get; set; } = string.Empty;
    public string ProviderType { get; set; } = "RSS";
    /// <summary>COMPANY | GLOBAL | OFFICIAL | DISCOVERY</summary>
    public string Category { get; set; } = "GLOBAL";
    public int Priority { get; set; } = 50;
    public bool Enabled { get; set; } = true;
    /// <summary>APPROVED | PENDING. Only APPROVED sources are used.</summary>
    public string ApprovalStatus { get; set; } = "APPROVED";
    public string Markets { get; set; } = "TSX,US";
    public int RefreshMinutes { get; set; } = 60;
    public int TimeoutSeconds { get; set; } = 10;
    public int MaxItems { get; set; } = 15;
    public bool IsArchived { get; set; }
    public DateTime? LastSuccessUtc { get; set; }
    public DateTime? LastFailureUtc { get; set; }
    public string? LastError { get; set; }
    public int AvgDurationMs { get; set; }
    public int UsefulArticleCount { get; set; }
}

public class NewsArticleReference
{
    public int Id { get; set; }
    /// <summary>SHA-256 of the normalised URL; unique, used for de-duplication.</summary>
    public string Fingerprint { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Headline { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public DateTime PublishedUtc { get; set; }
    public DateTime RetrievedUtc { get; set; } = DateTime.UtcNow;
    public string? Symbol { get; set; }
    public string Category { get; set; } = "COMPANY";
    public string Summary { get; set; } = string.Empty;
    public int? SourceId { get; set; }
}

/// <summary>User feedback on a report. Never alters transactions.</summary>
public class PreMarketFeedback
{
    public int Id { get; set; }
    public int RunId { get; set; }
    public string? Symbol { get; set; }
    /// <summary>REVIEWED | USEFUL | NOT_USEFUL | ACTION_TAKEN | NO_ACTION_TAKEN</summary>
    public string Rating { get; set; } = "REVIEWED";
    public string? Comment { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Cached sector per symbol so watchlist symbols can be mapped to global events without repeated lookups.</summary>
public class SecuritySectorCache
{
    public string Symbol { get; set; } = string.Empty;
    public string Sector { get; set; } = string.Empty;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
=======
>>>>>>> e274b8de818b34342deac708b3471d1570cf03bb
}

public sealed record PreMarketRunOptions(
    DateOnly? AsOfDate = null,
    bool DryRun = true,
    bool UseMockNews = false,
    bool SendEmail = false,
    bool Force = false,
    bool IsTest = true);

public sealed record NewsItemDto(
    string EvidenceId, string Headline, string Publisher, string Url,
    DateTime PublishedUtc, DateTime RetrievedUtc, string Summary, bool IsMock);

public sealed record NewsLookupResult(string Status, IReadOnlyList<NewsItemDto> Items, string? Note = null);

<<<<<<< HEAD
public sealed record AiAnalysisDto(
    IReadOnlyList<string> Bullish, IReadOnlyList<string> Bearish, IReadOnlyList<string> Risks,
    IReadOnlyList<string> EvidenceIds);

=======
>>>>>>> e274b8de818b34342deac708b3471d1570cf03bb
public sealed record StockAuditDto(
    string Symbol, string CompanyName, string Kind, string Sector,
    decimal? Shares, decimal? AverageCost, decimal? LastClose, DateOnly? PriceDate,
    decimal? UnrealizedPct, decimal? Rsi14, decimal? Rsi9, decimal? Change5dPct,
    string NewsStatus, IReadOnlyList<NewsItemDto> News,
<<<<<<< HEAD
    string Action, string Priority, string Rationale, IReadOnlyList<string> Warnings,
    IReadOnlyList<string>? EventIds = null, AiAnalysisDto? Ai = null, decimal? WeightPct = null, string? Currency = null,
    string? Change = null, int? EarningsInDays = null);

public sealed record GlobalEventDto(
    string EventId, string Category, string Title, string Mechanism, string Counterargument,
    IReadOnlyList<string> AffectedSectors, IReadOnlyList<string> AffectedSymbols,
    IReadOnlyList<NewsItemDto> Evidence, string? AiSummary, string? AiPortfolioNote);

public sealed record RunMetricsDto(
    int ArticlesRetrieved, int Events, int AiCalls, int AiDurationMs, int NewsDurationMs, string? AiModel);
=======
    string Action, string Priority, string Rationale, IReadOnlyList<string> Warnings);
>>>>>>> e274b8de818b34342deac708b3471d1570cf03bb

public sealed record PreMarketReportDto(
    DateOnly AsOfDate, DateTime GeneratedUtc, bool IsPartial, bool IsTestRun,
    bool ShadowMode, string AiStatus,
    IReadOnlyList<StockAuditDto> Holdings, IReadOnlyList<StockAuditDto> Watchlist,
<<<<<<< HEAD
    IReadOnlyList<string> DataQuality,
    IReadOnlyList<GlobalEventDto>? Events = null, RunMetricsDto? Metrics = null);
=======
    IReadOnlyList<string> DataQuality);
>>>>>>> e274b8de818b34342deac708b3471d1570cf03bb
