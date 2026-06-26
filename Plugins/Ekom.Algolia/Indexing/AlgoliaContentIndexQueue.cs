using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Channels;

namespace Ekom.Algolia.Indexing;

internal interface IAlgoliaContentIndexQueue
{
    ChannelReader<AlgoliaContentIndexJob> Reader { get; }
    bool TryEnqueue(AlgoliaContentIndexJob job);
    ValueTask EnqueueAsync(AlgoliaContentIndexJob job, CancellationToken ct = default);
}

internal sealed class AlgoliaContentIndexQueue : IAlgoliaContentIndexQueue
{
    private readonly Channel<AlgoliaContentIndexJob> _channel;
    private readonly ILogger<AlgoliaContentIndexQueue> _logger;

    public AlgoliaContentIndexQueue(
        ILogger<AlgoliaContentIndexQueue> logger,
        IOptions<AlgoliaOptions> options)
    {
        _logger = logger;
        var capacity = options.Value.ContentIndexing.Dispatching.MaxQueueSize <= 0
            ? 10_000
            : options.Value.ContentIndexing.Dispatching.MaxQueueSize;

        _channel = Channel.CreateBounded<AlgoliaContentIndexJob>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    public ChannelReader<AlgoliaContentIndexJob> Reader => _channel.Reader;

    public bool TryEnqueue(AlgoliaContentIndexJob job)
    {
        var accepted = _channel.Writer.TryWrite(job);
        if (!accepted)
            _logger.LogWarning("Algolia content index queue full. Dropped {Type} job with {Count} nodes.", job.Type, job.NodeIds.Count + job.NodeKeys.Count);

        return accepted;
    }

    public ValueTask EnqueueAsync(AlgoliaContentIndexJob job, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(job, ct);
}
