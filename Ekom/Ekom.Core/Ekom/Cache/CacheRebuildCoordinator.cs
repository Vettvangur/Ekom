namespace Ekom.Cache;

/// <summary>
/// Builds cache dictionaries and indexes in an isolated execution context, then publishes
/// them together. A concurrent live update wins: the stale rebuild is discarded instead.
/// </summary>
internal sealed class CacheRebuildCoordinator
{
    private static readonly IDisposable Noop = new Scope(() => { });
    private readonly object _gate = new();
    private readonly AsyncLocal<Rebuild?> _rebuild = new();
    private readonly AsyncLocal<Dictionary<(object Owner, string Name), object>?> _read = new();
    private Dictionary<(object Owner, string Name), object> _live = new();
    private long _revision;
    private int _updates;
    private bool _building;
    private Rebuild? _current;

    public bool IsRebuilding => _rebuild.Value is { Finished: false };

    public T Get<T>(object owner, string name, Func<T> create) where T : class
    {
        var key = (owner, name);
        if (_rebuild.Value is { Finished: false } rebuild)
        {
            // Child tasks may inherit the rebuild context.
            lock (rebuild.Values)
            {
                if (!rebuild.Values.TryGetValue(key, out var value))
                    rebuild.Values[key] = value = create();
                return (T)value;
            }
        }

        var snapshot = _read.Value ?? Volatile.Read(ref _live);
        if (snapshot.TryGetValue(key, out var existing))
            return (T)existing;

        lock (_gate)
        {
            if (_live.TryGetValue(key, out existing))
                return (T)existing;

            var value = create();
            var next = new Dictionary<(object, string), object>(_live) { [key] = value };
            Volatile.Write(ref _live, next);
            return value;
        }
    }

    public Rebuild Begin()
    {
        lock (_gate)
        {
            if (_building)
                throw new InvalidOperationException("An Ekom cache rebuild is already running.");
            _building = true;
            var rebuild = new Rebuild(this, _revision);
            _rebuild.Value = rebuild;
            _current = rebuild;
            return rebuild;
        }
    }

    /// <summary>Pin the dictionaries used by a multi-index lookup to one published generation.</summary>
    public IDisposable Read()
    {
        if (IsRebuilding) return Noop;
        var previous = _read.Value;
        _read.Value ??= Volatile.Read(ref _live);
        return new Scope(() => _read.Value = previous);
    }

    /// <summary>
    /// Mark the entire live mutation, including asynchronous I/O, so a rebuild cannot
    /// replace its result. This does not block storefront reads or the mutation itself.
    /// </summary>
    public IDisposable Update()
    {
        if (IsRebuilding)
            return Noop;

        var previousRead = _read.Value;
        _read.Value = null;
        lock (_gate)
        {
            _revision++;
            _updates++;
        }
        return new Scope(() =>
        {
            lock (_gate)
            {
                _revision++;
                _updates--;
            }
            _read.Value = previousRead;
        });
    }

    /// <summary>
    /// Mark a live mutation whose source of truth is outside the caches (a committed database row).
    /// It never invalidates a running rebuild: <paramref name="refresh"/> is run once the rebuild
    /// is published, so the new caches end up with the committed value. Refreshes with the same
    /// <paramref name="refreshKey"/> are collapsed into the latest one.
    /// </summary>
    public IDisposable UpdateRefreshable(string refreshKey, Func<Task> refresh)
    {
        if (IsRebuilding)
            return Noop;

        var previousRead = _read.Value;
        _read.Value = null;
        lock (_gate)
        {
            if (_building)
                _current!.Refreshes[refreshKey] = refresh;
        }
        return new Scope(() => _read.Value = previousRead);
    }

    private static async Task RunRefreshesAsync(Func<Task>[] refreshes)
    {
        foreach (var refresh in refreshes)
        {
            try
            {
                await refresh().ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Each refresh reports its own failure; one must not stop the rest.
            }
        }
    }

    internal sealed class Rebuild : IDisposable
    {
        private readonly CacheRebuildCoordinator _owner;
        private readonly long _revision;
        internal readonly Dictionary<(object Owner, string Name), object> Values = new();
        internal readonly Dictionary<string, Func<Task>> Refreshes = new();
        internal volatile bool Finished;

        /// <summary>Completes when the refreshes registered during this rebuild have run.</summary>
        internal Task Refreshed { get; private set; } = Task.CompletedTask;

        internal Rebuild(CacheRebuildCoordinator owner, long revision)
        {
            _owner = owner;
            _revision = revision;
        }

        public void Commit()
        {
            Func<Task>[] refreshes;
            lock (_owner._gate)
            {
                if (Finished)
                    throw new InvalidOperationException("The cache rebuild has already finished.");
                if (_owner._updates != 0 || _owner._revision != _revision)
                    throw new CacheRebuildConflictException("Ekom caches changed during the rebuild. The live caches were retained; retry the rebuild.");

                var next = new Dictionary<(object, string), object>(_owner._live);
                lock (Values)
                {
                    foreach (var pair in Values)
                        next[pair.Key] = pair.Value;
                }
                Volatile.Write(ref _owner._live, next);
                refreshes = Refreshes.Values.ToArray();
                Finish();
            }

            if (refreshes.Length == 0)
                return;

            // Run as ordinary live mutations, outside the rebuild's execution context.
            using (ExecutionContext.SuppressFlow())
            {
                Refreshed = Task.Run(() => RunRefreshesAsync(refreshes));
            }
        }

        public void Dispose()
        {
            lock (_owner._gate)
            {
                if (!Finished) Finish();
            }
        }

        private void Finish()
        {
            Finished = true;
            _owner._rebuild.Value = null;
            _owner._building = false;
            _owner._current = null;
        }
    }

    private sealed class Scope(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;
        public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
    }
}

/// <summary>A live cache mutation overlapped the rebuild, so it was discarded and can be retried.</summary>
internal sealed class CacheRebuildConflictException(string message) : InvalidOperationException(message);
