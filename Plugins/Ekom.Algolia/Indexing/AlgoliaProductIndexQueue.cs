using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Channels;

namespace Ekom.Algolia.Indexing;

internal interface IAlgoliaProductIndexQueue
{
    ChannelReader<AlgoliaProductIndexJob> Reader { get; }
    bool TryEnqueue(AlgoliaProductIndexJob job);
    ValueTask EnqueueAsync(AlgoliaProductIndexJob job, CancellationToken ct = default);
}

internal sealed class AlgoliaProductIndexQueue : IAlgoliaProductIndexQueue
{
    private readonly Channel<AlgoliaProductIndexJob> _channel;
    private readonly ILogger<AlgoliaProductIndexQueue> _logger;

    public AlgoliaProductIndexQueue(
        ILogger<AlgoliaProductIndexQueue> logger,
        IOptions<AlgoliaOptions> options)
    {
        _logger = logger;

        var dispatch = options.Value.Indexing.Dispatching;
        var capacity = dispatch.MaxQueueSize <= 0 ? 10_000 : dispatch.MaxQueueSize;

        _channel = Channel.CreateBounded<AlgoliaProductIndexJob>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    public ChannelReader<AlgoliaProductIndexJob> Reader => _channel.Reader;

    public bool TryEnqueue(AlgoliaProductIndexJob job)
    {
        var ok = _channel.Writer.TryWrite(job);

        if (!ok)
        {
            _logger.LogWarning(
                "Algolia index queue full. Dropped {Type} for store {Store} with {Count} product keys.",
                job.Type,
                job.StoreAlias,
                job.ProductKeys.Count);
        }

        return ok;
    }

    public ValueTask EnqueueAsync(AlgoliaProductIndexJob job, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(job, ct);
}
