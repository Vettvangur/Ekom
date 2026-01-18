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
        if (!_opt.Enabled) return;

        var flushDelay = TimeSpan.FromSeconds(_opt.FlushIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var hasItem = await _channel.Reader.WaitToReadAsync(stoppingToken);
                if (!hasItem) continue;

                await Task.Delay(flushDelay, stoppingToken);

                var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var work = new List<ProductWork>(capacity: 200);

                while (work.Count < 200 && _channel.Reader.TryRead(out var w))
                {
                    var key = $"{w.StoreAlias}|{w.ProductId}";
                    if (set.Add(key))
                        work.Add(w);
                }

                if (work.Count == 0) continue;

                var items = new List<KlaviyoProductItem>(work.Count);

                foreach (var w in work)
                {
                    try
                    {
                        var product = Ekom.API.Catalog.Instance.GetProduct(w.ProductId, w.StoreAlias);
                        if (product is null) continue;

                        var item = product.ToKlaviyoCatalogItem(w.IsPublished);

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
                        _logger.LogWarning(ex,
                            "Klaviyo: failed mapping product {CatalogId} for store {StoreAlias}",
                            w.ProductId, w.StoreAlias);
                    }
                }

                // send in chunks of 100
                foreach (var chunk in items.Chunk(100))
                    await _client.BulkUpsertCatalogItemsAsync(chunk, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Catalog batch dispatch failed. Continuing.");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }
}
