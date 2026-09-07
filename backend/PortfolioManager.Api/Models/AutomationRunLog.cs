namespace PortfolioManager.Api.Models;

/// <summary>One row per EOD Automation run (Scheduled/ManualRunNow/TestWake). Sole persistence for the
/// automation pipeline — no per-symbol archiving. Step statuses use a shared vocabulary so expected
/// non-execution (NotEligible/NotScheduled/NotObserved) is never confused with a real Failed.</summary>
public class AutomationRunLog
{
    public int Id { get; set; }

    /// <summary>External identifier returned to callers by the 202 Accepted trigger/run-now/test-wake
    /// endpoints and used for status polling — never expose the internal int Id.</summary>
    public Guid RunId { get; set; }

    /// <summary>Trading date in ET, formatted as "YYYY-MM-DD".</summary>
    public string TradingDate { get; set; } = "";

    /// <summary>"Scheduled" | "ManualRunNow" | "TestWake".</summary>
    public string TriggerType { get; set; } = "";

    public DateTime? ScheduledStartUtc { get; set; }
    public DateTime ActualStartUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    /// <summary>"Running" | "Success" | "PartialSuccess" | "Failed" | "SkippedNonTradingDay".</summary>
    public string OverallStatus { get; set; } = "Running";

    public string OwnerUserId { get; set; } = "";

    /// <summary>"Succeeded" | "Failed" — DataRefreshService.RefreshAllAsync has no business-time gate,
    /// so this always runs and a failure here is meaningful (e.g. Yahoo unreachable).</summary>
    public string RefreshStatus { get; set; } = "";
    public DateTime? RefreshStartedAtUtc { get; set; }
    public DateTime? RefreshCompletedAtUtc { get; set; }
    public int PortfolioSymbolCount { get; set; }
    public int WatchlistSymbolCount { get; set; }

    /// <summary>"NotEligible" | "Succeeded" | "NotObserved" — an empty DailySignals result for today is
    /// a valid Succeeded outcome (zero confirmed Bull/Bear-turn signals is normal), never a failure.</summary>
    public string RsiStatus { get; set; } = "";
    public DateTime? RsiCompletedAtUtc { get; set; }
    public int EodSignalsPersistedCount { get; set; }

    /// <summary>"NotEligible" | "Succeeded" | "Failed" — exactly one PortfolioValueHistory row per
    /// trading day is expected by design, so absence after the window genuinely closed is a real failure.</summary>
    public string SnapshotStatus { get; set; } = "";
    public DateTime? SnapshotCompletedAtUtc { get; set; }
    public string? SnapshotSource { get; set; }

    /// <summary>Informational only — "NotScheduled" | "NotObserved" | "Succeeded". Never factors into
    /// OverallStatus; Value Screener keeps its own independent schedule.</summary>
    public string ValueScreenerStatus { get; set; } = "";
    public DateTime? ValueScreenerLastRunAtUtc { get; set; }

    /// <summary>Handle-based Windows power-request lease acquire/release timestamps (PowerCreateRequest/
    /// PowerSetRequest/PowerClearRequest) — not SetThreadExecutionState.</summary>
    public DateTime? PowerRequestAcquiredAtUtc { get; set; }
    public DateTime? PowerRequestReleasedAtUtc { get; set; }

    public string? ErrorStep { get; set; }
    public string? ErrorMessage { get; set; }

    public string MachineName { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public record AutomationRunLogDto(
    Guid RunId,
    string TradingDate,
    string TriggerType,
    DateTime? ScheduledStartUtc,
    DateTime ActualStartUtc,
    DateTime? CompletedAtUtc,
    string OverallStatus,
    string RefreshStatus,
    DateTime? RefreshStartedAtUtc,
    DateTime? RefreshCompletedAtUtc,
    int PortfolioSymbolCount,
    int WatchlistSymbolCount,
    string RsiStatus,
    DateTime? RsiCompletedAtUtc,
    int EodSignalsPersistedCount,
    string SnapshotStatus,
    DateTime? SnapshotCompletedAtUtc,
    string? SnapshotSource,
    string ValueScreenerStatus,
    DateTime? ValueScreenerLastRunAtUtc,
    DateTime? PowerRequestAcquiredAtUtc,
    DateTime? PowerRequestReleasedAtUtc,
    string? ErrorStep,
    string? ErrorMessage,
    string MachineName);
