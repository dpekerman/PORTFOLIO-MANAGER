using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

/// <summary>
/// Singleton that tracks which CONFIRMED RSI signals have already triggered an email alert.
/// Ensures each signal fires an email ONCE. When a symbol drops off the confirmed list,
/// its key is removed so it can fire again if it re-enters a confirmed state.
/// </summary>
public class SignalNotificationTracker
{
    // Key: "SYMBOL|ScanType" e.g. "RY.TO|Overbought"
    private readonly HashSet<string> _notifiedKeys = [];
    private readonly object _lock = new();

    /// <summary>
    /// Computes which confirmed/EOD-confirmed signals are NEW (not yet notified) and
    /// updates the tracker to match the current scan result set.
    /// Returns only the newly-confirmed signals that should trigger an email.
    /// </summary>
    public List<RsiScanResult> GetNewlyConfirmedAndSync(IEnumerable<RsiScanResult> allResults)
    {
        var confirmedNow = allResults
            .Where(r => r.Status == SignalStatus.Confirmed || r.Status == SignalStatus.EodConfirm)
            .ToList();

        var confirmedKeys = confirmedNow
            .Select(r => $"{r.Symbol}|{r.ScanType}")
            .ToHashSet();

        lock (_lock)
        {
            // Find newly confirmed: in current set but NOT yet in our notified set
            var newlyConfirmed = confirmedNow
                .Where(r => !_notifiedKeys.Contains($"{r.Symbol}|{r.ScanType}"))
                .ToList();

            // Mark all current confirmed signals as notified
            foreach (var key in confirmedKeys)
                _notifiedKeys.Add(key);

            // Remove keys that are no longer confirmed (so they can trigger again if they return).
            // IMPORTANT: do NOT remove EOD-prefixed keys here — those are managed separately by
            // GetNewlyEodConfirmedAndSync and must survive across regular scan cycles so that
            // the EOD email doesn't re-fire every scan during the EOD window.
            _notifiedKeys.RemoveWhere(k => !k.StartsWith("EOD|") && !confirmedKeys.Contains(k));

            return newlyConfirmed;
        }
    }

    /// <summary>Returns count of currently tracked/notified signals.</summary>
    public int TrackedCount
    {
        get { lock (_lock) return _notifiedKeys.Count; }
    }

    /// <summary>
    /// Computes which EOD Confirm signals are NEW (not yet notified via EOD path) and
    /// updates the tracker. Used by the background service during the EOD window.
    /// </summary>
    public List<RsiScanResult> GetNewlyEodConfirmedAndSync(IEnumerable<RsiScanResult> allResults)
    {
        var eodNow = allResults
            .Where(r => r.Status == SignalStatus.EodConfirm)
            .ToList();

        var eodKeys = eodNow
            .Select(r => $"EOD|{r.Symbol}|{r.ScanType}")
            .ToHashSet();

        lock (_lock)
        {
            var newlyEod = eodNow
                .Where(r => !_notifiedKeys.Contains($"EOD|{r.Symbol}|{r.ScanType}"))
                .ToList();

            foreach (var key in eodKeys)
                _notifiedKeys.Add(key);

            _notifiedKeys.RemoveWhere(k => k.StartsWith("EOD|") && !eodKeys.Contains(k));

            return newlyEod;
        }
    }

    /// <summary>
    /// Clears all tracked keys so that all currently CONFIRMED signals will fire emails again
    /// on the next notification check. Used by the manual "scan-now" endpoint.
    /// </summary>
    public void ResetAll()
    {
        lock (_lock) _notifiedKeys.Clear();
    }
}
