namespace Ekom.Services;

/// <summary>
/// Hands out one lock per order and forgets locks that have been idle for a while.
/// A lock is only dropped while nobody holds it and nobody has asked for it recently,
/// so two callers can never end up with different locks for the same order.
/// </summary>
internal sealed class OrderLockRegistry
{
    private const int MinimumPruneSize = 256;

    private readonly Dictionary<Guid, Entry> _locks = new();
    private readonly object _gate = new();
    private readonly long _idleMilliseconds;
    private readonly Func<long> _clock;
    private int _pruneAt = MinimumPruneSize;

    public OrderLockRegistry()
        : this(TimeSpan.FromMinutes(10), () => Environment.TickCount64)
    {
    }

    internal OrderLockRegistry(TimeSpan idle, Func<long> clock, int pruneAt = MinimumPruneSize)
    {
        _idleMilliseconds = (long)idle.TotalMilliseconds;
        _clock = clock;
        _pruneAt = pruneAt;
    }

    internal int Count
    {
        get
        {
            lock (_gate)
            {
                return _locks.Count;
            }
        }
    }

    public SemaphoreSlim Get(Guid orderId)
    {
        lock (_gate)
        {
            long now = _clock();

            if (!_locks.TryGetValue(orderId, out Entry? entry))
            {
                if (_locks.Count >= _pruneAt)
                {
                    Prune(now);
                }

                entry = new Entry();
                _locks.Add(orderId, entry);
            }

            entry.LastUsed = now;
            return entry.Semaphore;
        }
    }

    private void Prune(long now)
    {
        List<Guid>? idle = null;

        foreach (KeyValuePair<Guid, Entry> pair in _locks)
        {
            // A held lock has a count of zero and is never dropped, however long it is held.
            if (pair.Value.Semaphore.CurrentCount == 1 && now - pair.Value.LastUsed > _idleMilliseconds)
            {
                (idle ??= new List<Guid>()).Add(pair.Key);
            }
        }

        if (idle != null)
        {
            foreach (Guid orderId in idle)
            {
                _locks.Remove(orderId);
            }
        }

        // Look again only after the table has grown, so pruning stays cheap per call.
        _pruneAt = Math.Max(MinimumPruneSize, _locks.Count * 2);
    }

    private sealed class Entry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public long LastUsed { get; set; }
    }
}
