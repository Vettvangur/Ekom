using Ekom.Algolia.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;

namespace Ekom.Algolia.Indexing;

internal sealed class AlgoliaPriceIndexWorker(
    AlgoliaPriceIndexQueue queue,
    IServiceScopeFactory scopeFactory,
    IOptions<AlgoliaOptions> options,
    ILogger<AlgoliaPriceIndexWorker> logger,
    TimeProvider? timeProvider = null) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var dispatch = options.Value.Indexing.Dispatching;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await queue.Reader.WaitToReadAsync(stoppingToken).ConfigureAwait(false)) break;
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, dispatch.FlushIntervalSeconds)), timeProvider ?? TimeProvider.System, stoppingToken).ConfigureAwait(false);
                var changes = queue.Drain(dispatch.MaxBatchSize > 0 ? dispatch.MaxBatchSize : 100);
                while (changes.Count > 0)
                {
                    stoppingToken.ThrowIfCancellationRequested();
                    await DispatchAsync(changes, stoppingToken).ConfigureAwait(false);
                    changes = queue.Drain(dispatch.MaxBatchSize > 0 ? dispatch.MaxBatchSize : 100);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Algolia price refresh failed; reconcile affected products");
            }
        }
    }

    internal async Task DispatchAsync(IReadOnlyList<AlgoliaPriceChange> changes, CancellationToken ct)
    {
        if (changes.Count == 0 || !options.Value.Enabled) return;
        using var scope = scopeFactory.CreateScope();
        var contentService = scope.ServiceProvider.GetRequiredService<IContentService>();
        var resolver = scope.ServiceProvider.GetRequiredService<AlgoliaStoreResolver>();
        var output = scope.ServiceProvider.GetRequiredService<IAlgoliaProductIndexQueue>();
        var byId = new Dictionary<int, IContent?>();
        var byKey = new Dictionary<Guid, IContent?>();
        IContent? GetById(int id)
        {
            if (!byId.TryGetValue(id, out var content)) byId[id] = content = contentService.GetById(id);
            return content;
        }
        foreach (var storeChanges in changes.GroupBy(change => change.StoreAlias, StringComparer.OrdinalIgnoreCase))
        {
            ct.ThrowIfCancellationRequested();
            var store = resolver.Resolve(storeChanges.Key);
            if (!store.Indexing.Enabled || !store.Indexing.Products) continue;
            var products = new HashSet<Guid>();
            foreach (var change in storeChanges)
            {
                ct.ThrowIfCancellationRequested();
                if (!byKey.TryGetValue(change.NodeKey, out var content))
                {
                    byKey[change.NodeKey] = content = contentService.GetById(change.NodeKey);
                    if (content != null) byId[content.Id] = content;
                }
                if (content == null || !content.Published) continue;
                if (content.ContentType.Alias == "ekmProductVariant")
                {
                    var group = GetById(content.ParentId);
                    content = group == null ? null : GetById(group.ParentId);
                }
                if (content?.Published == true && content.ContentType.Alias == "ekmProduct") products.Add(content.Key);
            }
            if (products.Count > 0)
                await output.EnqueueAsync(new AlgoliaProductIndexJob(AlgoliaProductIndexJobType.Upsert, store.Alias, products.ToArray()), ct).ConfigureAwait(false);
        }
    }
}
