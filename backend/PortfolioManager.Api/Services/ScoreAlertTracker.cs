namespace PortfolioManager.Api.Services;

/// <summary>
/// Singleton that remembers which symbols already triggered a score-alert email, per user and per
/// Eastern trading day, so each symbol alerts at most once a day even if its score fluctuates
/// around the threshold between refreshes. A new trading day starts clean.
/// </summary>
public sealed class ScoreAlertTracker
{
    private readonly Dictionary<string, (string Day, HashSet<string> Symbols)> _state = [];
    private readonly object _lock = new();

    /// <summary>
    /// Records <paramref name="qualifying"/> as the current set for the user and returns only the
    /// symbols that were not already notified today.
    /// </summary>
    public List<string> GetNewAndSync(string userId, string day, IEnumerable<string> qualifying)
    {
        var current = qualifying.ToHashSet(StringComparer.OrdinalIgnoreCase);
        lock (_lock)
        {
            if (!_state.TryGetValue(userId, out var entry) || entry.Day != day)
                entry = (day, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            var added = current.Where(s => !entry.Symbols.Contains(s)).OrderBy(s => s).ToList();
            entry.Symbols.UnionWith(added);
            _state[userId] = entry;
            return added;
        }
    }

    /// <summary>Releases symbols after a failed send so the next refresh retries them.</summary>
    public void Forget(string userId, IEnumerable<string> symbols)
    {
        lock (_lock)
        {
            if (!_state.TryGetValue(userId, out var entry)) return;
            foreach (var s in symbols) entry.Symbols.Remove(s);
        }
    }
}
