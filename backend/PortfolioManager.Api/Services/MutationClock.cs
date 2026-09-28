namespace PortfolioManager.Api.Services;

public interface IMutationClock
{
    /// <summary>Call after every Cash/Portfolio/Option mutation. Optimization/race-reduction cushion
    /// only — never required for historical correctness (that's guaranteed by transactional recompute).</summary>
    void Touch();
    double SecondsSinceLastMutation();
    /// <summary>Marks that today's PortfolioValueHistories row (if it exists) needs a reseal.</summary>
    void MarkTodayDirty();
    /// <summary>Atomically reads and clears the today-dirty flag. Returns true if a reseal is pending.</summary>
    bool TryTakeTodayDirty();
    /// <summary>Non-destructive peek at the today-dirty flag — does not clear it. Used to compute a
    /// live "PendingReseal" display status without interfering with the background service's own
    /// <see cref="TryTakeTodayDirty"/> consumption of the flag.</summary>
    bool IsTodayDirty { get; }
}

public sealed class MutationClock : IMutationClock
{
    /// <summary>Shared with PortfolioValueHistoryService's SnapshotStatus derivation so both agree on
    /// what counts as "still quiet" for today's row.</summary>
    public const double QuietPeriodSeconds = 90;

    private long _lastMutationTicks = DateTime.UtcNow.Ticks;
    private int _todayDirty;

    public void Touch() => Interlocked.Exchange(ref _lastMutationTicks, DateTime.UtcNow.Ticks);

    public double SecondsSinceLastMutation() =>
        (DateTime.UtcNow - new DateTime(Interlocked.Read(ref _lastMutationTicks), DateTimeKind.Utc)).TotalSeconds;

    public void MarkTodayDirty() => Interlocked.Exchange(ref _todayDirty, 1);

    public bool TryTakeTodayDirty() => Interlocked.Exchange(ref _todayDirty, 0) == 1;

    public bool IsTodayDirty => Interlocked.CompareExchange(ref _todayDirty, 0, 0) == 1;
}
