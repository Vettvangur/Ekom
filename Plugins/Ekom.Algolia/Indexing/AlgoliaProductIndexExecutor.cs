using Ekom.Algolia.Mappers;
using Ekom.Algolia.Models.Indexing;
using Ekom.API;
using Ekom.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Algolia.Search.Clients;

namespace Ekom.Algolia.Indexing;

internal sealed class AlgoliaProductIndexExecutor
{
    private readonly ISearchClient _client;
    private readonly AlgoliaOptions _options;
    private readonly IndexNameBuilder _indexNameBuilder;
    private readonly IAlgoliaProductIndexMapper _mapper;
    private readonly ILogger<AlgoliaProductIndexExecutor> _logger;

    public AlgoliaProductIndexExecutor(
        ISearchClient client,
        IOptions<AlgoliaOptions> options,
        IndexNameBuilder indexNameBuilder,
        IAlgoliaProductIndexMapper mapper,
        ILogger<AlgoliaProductIndexExecutor> logger)
    {
        _client = client;
        _options = options.Value;
        _indexNameBuilder = indexNameBuilder;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task HandleAsync(IReadOnlyCollection<AlgoliaProductIndexJob> jobs, CancellationToken ct)
    {
        if (jobs.Count == 0)
            return;

        if (!_options.Enabled || !_options.Indexing.Enabled || !_options.Indexing.Products)
            return;

        var byStore = jobs.GroupBy(j => j.StoreAlias, StringComparer.OrdinalIgnoreCase);

        foreach (var storeGroup in byStore)
        {
            ct.ThrowIfCancellationRequested();

            var storeAlias = storeGroup.Key;
            var store = ResolveStoreOptions(storeAlias);

            if (storeGroup.Any(j => j.Type == AlgoliaProductIndexJobType.RebuildStore))
            {
                await RebuildStoreAsync(store, ct).ConfigureAwait(false);
                continue;
            }

            var upsertKeys = new HashSet<Guid>();
            var deleteKeys = new HashSet<Guid>();

            foreach (var job in storeGroup)
            {
                if (job.Type == AlgoliaProductIndexJobType.Delete)
                {
                    foreach (var key in job.ProductKeys)
                        deleteKeys.Add(key);

                    continue;
                }

                foreach (var key in job.ProductKeys)
                    upsertKeys.Add(key);
            }

            foreach (var key in deleteKeys)
                upsertKeys.Remove(key);

            if (upsertKeys.Count > 0)
                await UpsertAsync(store, upsertKeys, ct).ConfigureAwait(false);

            if (deleteKeys.Count > 0)
                await DeleteAsync(store, deleteKeys, ct).ConfigureAwait(false);
        }
    }

    private AlgoliaStoreOptions ResolveStoreOptions(string storeAlias)
    {
        var store = _options.Stores.FirstOrDefault(s => s.Alias.Equals(storeAlias, StringComparison.OrdinalIgnoreCase));
        if (store != null)
            return store;

        return new AlgoliaStoreOptions { Alias = storeAlias };
    }

    private async Task RebuildStoreAsync(AlgoliaStoreOptions store, CancellationToken ct)
    {
        if (!_options.Indexing.Enabled || !_options.Indexing.Products)
            return;

        var query = new ProductQuery { RaiseEvents = false };
        var response = Catalog.Instance.GetAllProducts(store.Alias, query);
        var products = response.Products?.ToList() ?? [];

        if (products.Count == 0)
            return;

        var indexName = _indexNameBuilder.BuildPrimary("products", store);

        var records = new List<AlgoliaProductRecord>(products.Count);
        foreach (var product in products)
        {
            ct.ThrowIfCancellationRequested();
            var record = _mapper.Map(product, store, indexName);
            if (record != null)
                records.Add(record);
        }

        if (records.Count == 0)
            return;

        var batchSize = _options.Indexing.BatchSize <= 0 ? 1000 : _options.Indexing.BatchSize;

        _logger.LogInformation("Algolia rebuild store {Store} -> {IndexName}. Records={Count}", store.Alias, indexName, records.Count);

        await _client.ReplaceAllObjectsAsync(
            indexName: indexName,
            objects: records,
            batchSize: batchSize,
            cancellationToken: ct).ConfigureAwait(false);
    }

    private async Task UpsertAsync(AlgoliaStoreOptions store, IReadOnlyCollection<Guid> keys, CancellationToken ct)
    {
        var indexName = _indexNameBuilder.BuildPrimary("products", store);
        var records = new List<AlgoliaProductRecord>(keys.Count);
        var missingKeys = new List<Guid>();

        foreach (var key in keys)
        {
            ct.ThrowIfCancellationRequested();

            var product = Catalog.Instance.GetProduct(key, store.Alias, raiseEvent: false);
            if (product == null)
            {
                _logger.LogDebug("Algolia: product {Key} not found for store {Store}, enqueue delete.", key, store.Alias);
                missingKeys.Add(key);
                continue;
            }

            var record = _mapper.Map(product, store, indexName);
            if (record != null)
                records.Add(record);
        }

        if (records.Count == 0)
        {
            if (missingKeys.Count > 0)
                await DeleteAsync(store, missingKeys, ct).ConfigureAwait(false);
            return;
        }

        var batchSize = _options.Indexing.BatchSize <= 0 ? 1000 : _options.Indexing.BatchSize;

        _logger.LogDebug("Algolia upsert {Count} products to {IndexName}", records.Count, indexName);

        await _client.SaveObjectsAsync(
            indexName: indexName,
            objects: records,
            batchSize: batchSize,
            waitForTasks: true,
            options: null,
            cancellationToken: ct).ConfigureAwait(false);

        if (missingKeys.Count > 0)
            await DeleteAsync(store, missingKeys, ct).ConfigureAwait(false);
    }

    private async Task DeleteAsync(AlgoliaStoreOptions store, IReadOnlyCollection<Guid> keys, CancellationToken ct)
    {
        if (keys.Count == 0)
            return;

        var indexName = _indexNameBuilder.BuildPrimary("products", store);
        var ids = keys.Select(k => k.ToString()).ToList();

        var batchSize = _options.Indexing.BatchSize <= 0 ? 1000 : _options.Indexing.BatchSize;

        _logger.LogDebug("Algolia delete {Count} products from {IndexName}", ids.Count, indexName);

        await _client.DeleteObjectsAsync(
            indexName: indexName,
            objectIDs: ids,
            batchSize: batchSize,
            waitForTasks: false,
            options: null,
            cancellationToken: ct).ConfigureAwait(false);
    }
}
