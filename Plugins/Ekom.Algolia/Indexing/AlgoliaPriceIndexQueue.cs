using Microsoft.Extensions.Options;
using System.Threading.Channels;

namespace Ekom.Algolia.Indexing;

internal sealed record AlgoliaPriceChange(Guid NodeKey, string StoreAlias);

/// <summary>Coalesces pending currency/property changes without reading Umbraco content on the writer.</summary>
internal sealed class AlgoliaPriceIndexQueue
{
    private readonly Channel<AlgoliaPriceChange> _channel;
    private readonly HashSet<AlgoliaPriceChange> _pending = new();
    private readonly object _gate = new();

    public AlgoliaPriceIndexQueue(IOptions<AlgoliaOptions> options)
    {
        var capacity = options.Value.Indexing.Dispatching.MaxQueueSize;
        _channel = Channel.CreateBounded<AlgoliaPriceChange>(new BoundedChannelOptions(capacity > 0 ? capacity : 10_000)
        {
            SingleReader = true, SingleWriter = false, FullMode = BoundedChannelFullMode.Wait,
        });
    }

    public ChannelReader<AlgoliaPriceChange> Reader => _channel.Reader;

    public bool TryEnqueue(Guid nodeKey, string storeAlias)
    {
        var change = new AlgoliaPriceChange(nodeKey, storeAlias.ToUpperInvariant());
        lock (_gate)
        {
            if (_pending.Contains(change)) return true;
            if (!_channel.Writer.TryWrite(change)) return false;
            _pending.Add(change);
            return true;
        }
    }

    public IReadOnlyList<AlgoliaPriceChange> Drain(int limit)
    {
        var changes = new List<AlgoliaPriceChange>();
        lock (_gate)
        {
            while (changes.Count < limit && _channel.Reader.TryRead(out var change))
            {
                _pending.Remove(change);
                changes.Add(change);
            }
        }
        // Updates arriving after draining create another job, including during indexing.
        return changes;
    }
}
