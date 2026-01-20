using Ekom.Klaviyo.Enrichers.ProductEnricher;
using Ekom.Klaviyo.Mappers;
using Ekom.Klaviyo.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Channels;

namespace Ekom.Klaviyo;

public interface IKlaviyoProductDispatcher
{
    ValueTask EnqueueAsync(string storeAlias, Guid catalogId, bool isPublished, CancellationToken ct = default);
}
internal sealed record ProductWork(string StoreAlias, Guid ProductId, bool IsPublished);

internal sealed class KlaviyoProductBatchingDispatcher : BackgroundService, IKlaviyoProductDispatcher
{
    private readonly IKlaviyoClient _client;
    private readonly KlaviyoOptions _opt;
    private readonly KlaviyoProductEnrichmentPipeline _pipeline;
    private readonly ILogger<KlaviyoProductBatchingDispatcher> _logger;

    private readonly Channel<ProductWork> _channel;

    public KlaviyoProductBatchingDispatcher(
        IKlaviyoClient client,
        IOptions<KlaviyoOptions> options,
        ILogger<KlaviyoProductBatchingDispatcher> logger,
        KlaviyoProductEnrichmentPipeline pipeline)
    {
        _client = client;
        _opt = options.Value;
        _logger = logger;

        _channel = Channel.CreateBounded<ProductWork>(new BoundedChannelOptions(_opt.MaxQueueSize)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
        _pipeline = pipeline;
    }

    public ValueTask EnqueueAsync(string storeAlias, Guid catalogId, bool isPublished, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(new ProductWork(storeAlias, catalogId, isPublished), ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Klaviyo dispatcher started. Enabled={Enabled}, ProductEventsEnabled={ProductEventsEnabled}, MaxQueueSize={MaxQueueSize}, FlushIntervalSeconds={FlushIntervalSeconds}, MaxBatchSize={MaxBatchSize}",
            _opt.Enabled,
            _opt.ProductEvents.Enabled,
            _opt.MaxQueueSize,
            _opt.FlushIntervalSeconds,
            _opt.MaxBatchSize);

        var flushDelay = TimeSpan.FromSeconds(Math.Max(1, _opt.FlushIntervalSeconds));
        var maxBatch = _opt.MaxBatchSize <= 0 ? 100 : _opt.MaxBatchSize;

        var maxDrain = Math.Max(maxBatch, 1) * 2;
        if (maxDrain < 1) maxDrain = 200;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Do NOT exit the service if disabled; just idle.
                if (!_opt.Enabled || !_opt.ProductEvents.Enabled)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    continue;
                }

                // Wait until something is available to read.
                var hasItem = await _channel.Reader.WaitToReadAsync(stoppingToken);
                if (!hasItem)
                    continue;

                // Coalesce a short burst of notifications into a single batch.
                await Task.Delay(flushDelay, stoppingToken);

                // Last-write-wins per (store, product) to avoid dropping quick publish/unpublish flips.
                var latest = new Dictionary<string, ProductWork>(StringComparer.OrdinalIgnoreCase);

                // Drain up to maxDrain items from the queue (bounded by what is available).
                while (latest.Count < maxDrain && _channel.Reader.TryRead(out var w))
                {
                    var key = $"{w.StoreAlias}|{w.ProductId}";
                    latest[key] = w; // overwrite = last state wins
                }

                if (latest.Count == 0)
                    continue;

                _logger.LogDebug(
                    "Klaviyo dispatcher draining {Count} unique items (coalesced) from queue.",
                    latest.Count);

                var items = new List<KlaviyoProductItem>(latest.Count);

                foreach (var w in latest.Values)
                {
                    try
                    {
                        // Fetch product
                        var product = Ekom.API.Catalog.Instance.GetProduct(w.ProductId, w.StoreAlias);

                        // If soft-delete mode, and product can't be loaded, skip.
                        // (In hard-delete mode we can still delete by ExternalId if you have it.)
                        if (_opt.ProductEvents.DeleteMode == KlaviyoDeleteMode.Soft && product is null)
                        {
                            _logger.LogDebug(
                                "Klaviyo: product not found (soft mode), skipping. Store={Store} ProductId={ProductId}",
                                w.StoreAlias, w.ProductId);
                            continue;
                        }

                        KlaviyoProductItem? item;

                        // If hard-delete mode and this work item represents "unpublished/deleted",
                        // create a minimal item that has ExternalId so the client can delete it.
                        if (_opt.ProductEvents.DeleteMode == KlaviyoDeleteMode.Hard && !w.IsPublished)
                        {
                            item = new KlaviyoProductItem
                            {
                                StoreAlias = w.StoreAlias,
                                Id = w.ProductId,
                                Published = false,
                                Title = string.Empty,
                                Description = string.Empty,
                                Sku = string.Empty
                            };
                        }
                        else
                        {
                            item = product?.ToKlaviyoCatalogItem(w.IsPublished, _opt.Host);
                        }

                        if (item is null)
                            continue;

                        await _pipeline.ApplyAsync(
                            item,
                            new KlaviyoProductEnrichmentContext
                            {
                                StoreAlias = w.StoreAlias,
                                ProductKey = w.ProductId,
                                SourceProduct = product,
                                IsPublished = w.IsPublished
                            },
                            stoppingToken);

                        items.Add(item);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Klaviyo: failed mapping/enrichment for product {ProductId} store {StoreAlias}",
                            w.ProductId, w.StoreAlias);
                    }
                }

                if (items.Count == 0)
                    continue;

                foreach (var chunk in items.Chunk(maxBatch))
                {
                    try
                    {
                        await _client.BulkUpsertCatalogItemsAsync(chunk, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        // Do not kill the loop; log and continue.
                        _logger.LogError(
                            ex,
                            "Klaviyo: bulk upsert failed for chunk size {ChunkSize}. Will continue processing future work.",
                            chunk.Length);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Klaviyo dispatcher loop crashed; retrying in 2 seconds.");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }

        _logger.LogInformation("Klaviyo dispatcher stopped.");
    }


}
