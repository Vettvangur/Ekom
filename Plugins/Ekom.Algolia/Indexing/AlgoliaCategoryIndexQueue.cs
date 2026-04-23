using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Channels;

namespace Ekom.Algolia.Indexing;

internal interface IAlgoliaCategoryIndexQueue
{
    ChannelReader<AlgoliaCategoryIndexJob> Reader { get; }
    bool TryEnqueue(AlgoliaCategoryIndexJob job);
    ValueTask EnqueueAsync(AlgoliaCategoryIndexJob job, CancellationToken ct = default);
}

internal sealed class AlgoliaCategoryIndexQueue : IAlgoliaCategoryIndexQueue
{
    private readonly Channel<AlgoliaCategoryIndexJob> _channel;
    private readonly ILogger<AlgoliaCategoryIndexQueue> _logger;

    public AlgoliaCategoryIndexQueue(
        ILogger<AlgoliaCategoryIndexQueue> logger,
        IOptions<AlgoliaOptions> options)
    {
        _logger = logger;

        var dispatch = options.Value.Indexing.Dispatching;
        var capacity = dispatch.MaxQueueSize <= 0 ? 10_000 : dispatch.MaxQueueSize;

        _channel = Channel.CreateBounded<AlgoliaCategoryIndexJob>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    public ChannelReader<AlgoliaCategoryIndexJob> Reader => _channel.Reader;

    public bool TryEnqueue(AlgoliaCategoryIndexJob job)
    {
        var ok = _channel.Writer.TryWrite(job);

        if (ok)
        {
            _logger.LogDebug(
                "Algolia queue accepted {Type} for store {Store} with {Count} category keys.",
                job.Type,
                job.StoreAlias,
                job.CategoryKeys.Count);
        }

        if (!ok)
        {
            _logger.LogWarning(
                "Algolia category index queue full. Dropped {Type} for store {Store} with {Count} category keys.",
                job.Type,
                job.StoreAlias,
                job.CategoryKeys.Count);
        }

        return ok;
    }

    public ValueTask EnqueueAsync(AlgoliaCategoryIndexJob job, CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Algolia queue waiting to accept {Type} for store {Store} with {Count} category keys.",
            job.Type,
            job.StoreAlias,
            job.CategoryKeys.Count);

        return _channel.Writer.WriteAsync(job, ct);
    }
}
