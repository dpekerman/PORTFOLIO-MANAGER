namespace PortfolioManager.Api.Models;

/// <summary>Single-row settings for the AI pre-market agent. Id is always 1.</summary>
public class PreMarketAgentConfig
{
    public int Id { get; set; } = 1;
    public bool ScheduleEnabled { get; set; }
    /// <summary>America/Toronto local start time, HH:mm.</summary>
    public string StartTimeLocal { get; set; } = "07:00";
    /// <summary>Scheduler ignores days before this date.</summary>
    public DateOnly? FirstRunDate { get; set; }
    /// <summary>Test override: treated as "today" for data cut-off, report date and run key.</summary>
    public DateOnly? AsOfDate { get; set; }
    /// <summary>America/Toronto local hard SMTP cutoff, HH:mm.</summary>
    public string DeadlineLocal { get; set; } = "09:25";
    public bool ShadowMode { get; set; } = true;
    /// <summary>Owner of the holdings/watchlist analysed; null = all users.</summary>
    public string? UserId { get; set; }
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
    /// <summary>NOT_SENT | SMTP_ACCEPTED | FAILED. SMTP acceptance is not confirmed delivery.</summary>
    public string EmailStatus { get; set; } = "NOT_SENT";
    public string? EmailMessage { get; set; }
    public DateTime? EmailSubmittedAtUtc { get; set; }
    public string? Error { get; set; }
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

public sealed record StockAuditDto(
    string Symbol, string CompanyName, string Kind, string Sector,
    decimal? Shares, decimal? AverageCost, decimal? LastClose, DateOnly? PriceDate,
    decimal? UnrealizedPct, decimal? Rsi14, decimal? Rsi9, decimal? Change5dPct,
    string NewsStatus, IReadOnlyList<NewsItemDto> News,
    string Action, string Priority, string Rationale, IReadOnlyList<string> Warnings);

public sealed record PreMarketReportDto(
    DateOnly AsOfDate, DateTime GeneratedUtc, bool IsPartial, bool IsTestRun,
    bool ShadowMode, string AiStatus,
    IReadOnlyList<StockAuditDto> Holdings, IReadOnlyList<StockAuditDto> Watchlist,
    IReadOnlyList<string> DataQuality);
