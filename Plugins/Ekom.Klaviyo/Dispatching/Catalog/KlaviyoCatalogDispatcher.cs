using Ekom.Klaviyo.Enrichers.ProductEnricher;
using Ekom.Klaviyo.Http;
using Ekom.Klaviyo.Mappers;
using Ekom.Klaviyo.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Channels;

namespace Ekom.Klaviyo.Dispatching.Catalog;

public interface IKlaviyoCatalogDispatcher
{
    ValueTask EnqueueAsync(string storeAlias, Guid productId, bool isPublished, CancellationToken ct = default);
}

internal sealed record CatalogWork(string StoreAlias, Guid ProductId, bool IsPublished);

internal sealed class KlaviyoCatalogDispatcher : BackgroundService, IKlaviyoCatalogDispatcher
{
    private readonly IKlaviyoCatalogClient _client; // or IKlaviyoClient if you keep one client
    private readonly KlaviyoOptions _opt;
    private readonly KlaviyoProductEnrichmentPipeline _pipeline;
    private readonly ILogger<KlaviyoCatalogDispatcher> _logger;

    private readonly Channel<CatalogWork> _channel;

    public KlaviyoCatalogDispatcher(
        IKlaviyoCatalogClient client,
        IOptions<KlaviyoOptions> options,
        ILogger<KlaviyoCatalogDispatcher> logger,
        KlaviyoProductEnrichmentPipeline pipeline)
    {
        _client = client;
        _opt = options.Value;
        _logger = logger;
        _pipeline = pipeline;

        var dispatch = _opt.Catalog.Dispatching;

        _channel = Channel.CreateBounded<CatalogWork>(new BoundedChannelOptions(dispatch.MaxQueueSize)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public ValueTask EnqueueAsync(string storeAlias, Guid productId, bool isPublished, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(new CatalogWork(storeAlias, productId, isPublished), ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var dispatch = _opt.Catalog.Dispatching;

        _logger.LogInformation(
            "Klaviyo Catalog dispatcher started. Enabled={Enabled}, CatalogEnabled={CatalogEnabled}, SyncMode={SyncMode}, MaxQueueSize={MaxQueueSize}, FlushIntervalSeconds={FlushIntervalSeconds}, MaxBatchSize={MaxBatchSize}",
            _opt.Enabled,
            _opt.Catalog.Enabled,
            _opt.Catalog.SyncMode,
            dispatch.MaxQueueSize,
            dispatch.FlushIntervalSeconds,
            dispatch.MaxBatchSize);

        var flushDelay = TimeSpan.FromSeconds(Math.Max(1, dispatch.FlushIntervalSeconds));
        var maxBatch = dispatch.MaxBatchSize <= 0 ? 100 : dispatch.MaxBatchSize;

        var maxDrain = Math.Max(maxBatch, 1) * 2;
        if (maxDrain < 1) maxDrain = 200;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_opt.Enabled ||
                    !_opt.Events.Enabled)
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

                // Last-write-wins per (store, product)
                var latest = new Dictionary<string, CatalogWork>(StringComparer.OrdinalIgnoreCase);

                while (latest.Count < maxDrain && _channel.Reader.TryRead(out var w))
                {
                    // Optional store filter
                    if (_opt.Stores is { Count: > 0 } &&
                        !_opt.Stores.Contains(w.StoreAlias, StringComparer.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var key = $"{w.StoreAlias}|{w.ProductId}";
                    latest[key] = w;
                }

                if (latest.Count == 0)
                    continue;

                _logger.LogDebug(
                    "Klaviyo Catalog dispatcher draining {Count} unique items (coalesced) from queue.",
                    latest.Count);

                var items = new List<KlaviyoProductItem>(latest.Count);

                foreach (var w in latest.Values)
                {
                    try
                    {
                        var product = Ekom.API.Catalog.Instance.GetProduct(w.ProductId, w.StoreAlias);

                        // Soft delete mode + missing product => skip
                        if (_opt.Catalog.DeleteMode == KlaviyoDeleteMode.Soft && product is null)
                        {
                            _logger.LogDebug(
                                "Klaviyo: product not found (soft mode), skipping. Store={Store} ProductId={ProductId}",
                                w.StoreAlias, w.ProductId);
                            continue;
                        }

                        KlaviyoProductItem? item;

                        // Hard delete mode + unpublished => minimal delete item
                        if (_opt.Catalog.DeleteMode == KlaviyoDeleteMode.Hard && !w.IsPublished)
                        {
                            item = new KlaviyoProductItem
                            {
                                StoreAlias = w.StoreAlias,
                                Id = w.ProductId,
                                Published = false,

                                // Minimal but safe defaults
                                Title = string.Empty,
                                Description = string.Empty,
                                Sku = string.Empty,
                            };
                        }
                        else
                        {
                            item = product?.ToKlaviyoCatalogItem(w.IsPublished, _opt.SiteBaseUrl);
                        }

                        if (item is null)
                            continue;

                        // Enrichment pipeline (same as your product dispatcher)
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
                        // Use delete mode as part of upsert semantics
                        await _client.BulkUpsertCatalogItemsAsync(chunk, _opt.Catalog.DeleteMode, stoppingToken);
                    }
                    catch (KlaviyoCatalogSyncLockException ex)
                    {
                        // Not transient: feed sync is active in Klaviyo, stop hammering.
                        _logger.LogError(ex,
                            "Klaviyo Catalog API is locked by active Catalog Sync. Disable feed sync in Klaviyo or set Catalog.Method=Feed. Body={Body}",
                            ex.ResponseBody);
                        break; // break current flush cycle
                    }
                    catch (Exception ex)
                    {
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
                _logger.LogError(ex, "Klaviyo Catalog dispatcher loop crashed; retrying in 2 seconds.");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }

        _logger.LogInformation("Klaviyo Catalog dispatcher stopped.");
    }
}
