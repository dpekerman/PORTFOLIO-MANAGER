using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services.FinalAction;

/// <summary>What the evaluation pipeline last did, read from existing records only (no new storage).</summary>
public sealed record EvaluationStatus(DateTime? LastCompletePassUtc, DateTime? LastAttemptUtc, bool LastAttemptFailed, EvaluationRun Run)
{
    public static EvaluationStatus Unknown { get; } = new(null, null, false, EvaluationRun.None);
}

public static class EvaluationStatusReader
{
    /// <summary>A run that started longer ago than this is history, not an in-flight evaluation.</summary>
    private static readonly TimeSpan RunRelevance = TimeSpan.FromHours(24);

    /// <summary>
    /// Last complete / failed Signal Tracker evaluation (user preference settings) and the latest EOD automation run.
    /// A Running run only counts as Evaluating while its heartbeat is recent; otherwise it is Interrupted, so the state
    /// can never be stuck after a crashed or abandoned job.
    /// </summary>
    public static async Task<EvaluationStatus> ReadAsync(AppDbContext db, string userId, DateTime utcNow, CancellationToken ct)
    {
        DateTime? lastComplete = null, lastAttempt = null;
        var failed = false;
        var pref = await db.UserPreferences.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId && p.PreferenceKey == SignalsAuditSettings.PreferenceKey, ct);
        if (pref is not null)
        {
            try
            {
                var s = JsonSerializer.Deserialize<SignalsAuditSettings>(pref.PreferenceValue, SignalsAuditSettings.Json);
                if (s is not null)
                {
                    lastComplete = s.LastCompleteEvaluationUtc;
                    lastAttempt = s.LastEodEvaluationUtc;
                    failed = lastAttempt is not null && !s.LastEodEvaluationComplete;
                }
            }
            catch (JsonException) { /* unreadable settings are treated as never evaluated */ }
        }

        var run = EvaluationRun.None;
        var latest = await db.AutomationRunLogs.AsNoTracking()
            .OrderByDescending(r => r.ActualStartUtc).FirstOrDefaultAsync(ct);
        if (latest is not null && utcNow - latest.ActualStartUtc <= RunRelevance)
            run = FinalActionStatusResolver.RunState(latest.OverallStatus, latest.LastHeartbeatAtUtc, latest.ActualStartUtc, utcNow);

        return new EvaluationStatus(lastComplete, lastAttempt, failed, run);
    }
}
