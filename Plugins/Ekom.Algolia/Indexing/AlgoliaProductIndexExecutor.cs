using Ekom.Algolia.Mappers;
using Ekom.Algolia.Models.Indexing;
using Ekom.Algolia.Services;
using Ekom.API;
using Ekom.Models;
using Algolia.Search.Models.Search;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Algolia.Search.Clients;

namespace Ekom.Algolia.Indexing;

internal sealed class AlgoliaProductIndexExecutor
{
    private readonly ISearchClient _client;
    private readonly AlgoliaIndexReplacementService _indexReplacementService;
    private readonly AlgoliaOptions _options;
    private readonly AlgoliaStoreResolver _storeResolver;
    private readonly IndexNameBuilder _indexNameBuilder;
    private readonly IAlgoliaQuerySuggestionsConfigurator _querySuggestionsConfigurator;
    private readonly AlgoliaSearchCacheVersionProvider _searchCacheVersions;
    private readonly IAlgoliaProductIndexMapper _mapper;
    private readonly ILogger<AlgoliaProductIndexExecutor> _logger;

    public AlgoliaProductIndexExecutor(
        ISearchClient client,
        AlgoliaIndexReplacementService indexReplacementService,
        IOptions<AlgoliaOptions> options,
        AlgoliaStoreResolver storeResolver,
        IndexNameBuilder indexNameBuilder,
        IAlgoliaQuerySuggestionsConfigurator querySuggestionsConfigurator,
        AlgoliaSearchCacheVersionProvider searchCacheVersions,
        IAlgoliaProductIndexMapper mapper,
        ILogger<AlgoliaProductIndexExecutor> logger)
    {
        _client = client;
        _indexReplacementService = indexReplacementService;
        _options = options.Value;
        _storeResolver = storeResolver;
        _indexNameBuilder = indexNameBuilder;
        _querySuggestionsConfigurator = querySuggestionsConfigurator;
        _searchCacheVersions = searchCacheVersions;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task HandleAsync(IReadOnlyCollection<AlgoliaProductIndexJob> jobs, CancellationToken ct)
    {
        if (jobs.Count == 0)
            return;

        if (!_options.Enabled || !_options.Indexing.Enabled || !_options.Indexing.Products)
            return;

        _logger.LogDebug("Algolia executor handling {Count} queued jobs.", jobs.Count);

        var byStore = jobs.GroupBy(j => j.StoreAlias, StringComparer.OrdinalIgnoreCase);

        foreach (var storeGroup in byStore)
        {
            ct.ThrowIfCancellationRequested();

            var storeAlias = storeGroup.Key;
            var store = _storeResolver.Resolve(storeAlias);
            var storeJobs = storeGroup.ToList();

            _logger.LogDebug(
                "Algolia executor resolved store {Store}. Locale={Locale}, Currency={Currency}, Locales={LocaleCount}, Currencies={CurrencyCount}, Jobs={JobCount}",
                store.Alias,
                store.Locale,
                store.Currency,
                store.Locales.Count,
                store.Currencies.Count,
                storeJobs.Count);

            if (storeJobs.Any(j => j.Type == AlgoliaProductIndexJobType.RebuildStore))
            {
                _logger.LogDebug("Algolia executor starting rebuild for store {Store}.", store.Alias);
                await RebuildStoreAsync(store, ct).ConfigureAwait(false);
                continue;
            }

            var upsertKeys = new HashSet<Guid>();
            var deleteKeys = new HashSet<Guid>();

            foreach (var job in storeJobs)
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

            _logger.LogDebug(
                "Algolia executor prepared store {Store} with {UpsertCount} upserts and {DeleteCount} deletes.",
                store.Alias,
                upsertKeys.Count,
                deleteKeys.Count);

            if (upsertKeys.Count > 0)
                await UpsertAsync(store, upsertKeys, ct).ConfigureAwait(false);

            if (deleteKeys.Count > 0)
                await DeleteAsync(store, deleteKeys, ct).ConfigureAwait(false);
        }
    }

    private async Task RebuildStoreAsync(AlgoliaResolvedStore store, CancellationToken ct)
    {
        if (!_options.Indexing.Enabled || !_options.Indexing.Products)
            return;

        var query = new ProductQuery { RaiseEvents = false };
        var response = await Catalog.Instance.GetAllProductsAsync(store.Alias, query, ct: ct);
        var products = response.Products?.ToList() ?? [];

        _logger.LogDebug("Algolia rebuild fetched {Count} products for store {Store}.", products.Count, store.Alias);

        if (products.Count == 0)
            return;

        foreach (var target in store.ExpandIndexTargets())
        {
            ct.ThrowIfCancellationRequested();
            var indexName = _indexNameBuilder.BuildPrimary("products", target);
            var records = new List<AlgoliaProductRecord>(products.Count);

            var skippedProducts = 0;

            foreach (var product in products)
            {
                ct.ThrowIfCancellationRequested();
                var mappedRecords = _mapper.MapRecords(product, target, indexName);
                if (mappedRecords.Count > 0)
                    records.AddRange(mappedRecords);
                else
                    skippedProducts++;
            }

            _logger.LogDebug(
                "Algolia rebuild mapped store {Store} locale {Locale} currency {Currency} to {RecordCount} records. Skipped={SkippedCount}",
                target.Alias,
                target.Locale,
                target.Currency,
                records.Count,
                skippedProducts);

            var batchSize = _options.Indexing.BatchSize <= 0 ? 1000 : _options.Indexing.BatchSize;

            _logger.LogInformation(
                "Algolia rebuild store {Store} locale {Locale} currency {Currency} -> {IndexName}. Records={Count}",
                target.Alias,
                target.Locale,
                target.Currency,
                indexName,
                records.Count);

            await _indexReplacementService.ReplaceAllAsync(indexName, records, batchSize, ct).ConfigureAwait(false);

            await EnsureIndexSettingsAsync(target, indexName, ct).ConfigureAwait(false);
            await EnsureQuerySuggestionsAsync(target, indexName, ct).ConfigureAwait(false);
        }

        _searchCacheVersions.InvalidateStore(store.Alias);
    }

    private async Task UpsertAsync(AlgoliaResolvedStore store, IReadOnlyCollection<Guid> keys, CancellationToken ct)
    {
        _logger.LogDebug("Algolia upsert requested for store {Store} with {Count} product keys.", store.Alias, keys.Count);

        var missingKeys = new List<Guid>();
        var products = new List<IProduct>(keys.Count);

        foreach (var key in keys)
        {
            ct.ThrowIfCancellationRequested();

            var product = await Catalog.Instance.GetProductAsync(key, store.Alias, raiseEvent: false, ct: ct);
            if (product == null)
            {
                _logger.LogDebug("Algolia: product {Key} not found for store {Store}, enqueue delete.", key, store.Alias);
                missingKeys.Add(key);
                continue;
            }

            products.Add(product);
        }

        if (products.Count == 0)
        {
            _logger.LogDebug("Algolia upsert resolved no products for store {Store}. MissingKeys={MissingCount}", store.Alias, missingKeys.Count);

            if (missingKeys.Count > 0)
                await DeleteAsync(store, missingKeys, ct).ConfigureAwait(false);
            return;
        }

        var batchSize = _options.Indexing.BatchSize <= 0 ? 1000 : _options.Indexing.BatchSize;

        foreach (var target in store.ExpandIndexTargets())
        {
            ct.ThrowIfCancellationRequested();
            var indexName = _indexNameBuilder.BuildPrimary("products", target);
            await EnsureIndexSettingsAsync(target, indexName, ct).ConfigureAwait(false);
            var records = new List<AlgoliaProductRecord>(products.Count);

            var skippedProducts = 0;


            foreach (var product in products)
            {
                var mappedRecords = _mapper.MapRecords(product, target, indexName);
                if (mappedRecords.Count > 0)
                    records.AddRange(mappedRecords);
                else
                    skippedProducts++;
            }

            _logger.LogDebug(
                "Algolia upsert mapped store {Store} locale {Locale} currency {Currency} to {RecordCount} records. Skipped={SkippedCount}",
                target.Alias,
                target.Locale,
                target.Currency,
                records.Count,
                skippedProducts);

            if (records.Count == 0)
            {
                _logger.LogDebug(
                    "Algolia upsert produced no records for store {Store} locale {Locale} currency {Currency}; skipping save.",
                    target.Alias,
                    target.Locale,
                    target.Currency);
                continue;
            
            }

            _logger.LogDebug(
                "Algolia upsert {Count} products to {IndexName} for locale {Locale} currency {Currency}",
                records.Count,
                indexName,
                target.Locale,
                target.Currency);

            if (_options.Indexing.Variants)
                await DeleteByProductIdsAsync(indexName, products.Select(x => x.Key), waitForTasks: true, ct).ConfigureAwait(false);

            await _client.SaveObjectsAsync(
                indexName: indexName,
                objects: records,
                batchSize: batchSize,
                waitForTasks: true,
                options: null,
                cancellationToken: ct).ConfigureAwait(false);

            await EnsureQuerySuggestionsAsync(target, indexName, ct).ConfigureAwait(false);
        }

        _searchCacheVersions.InvalidateStore(store.Alias);

        if (missingKeys.Count > 0)
            await DeleteAsync(store, missingKeys, ct).ConfigureAwait(false);
    }

    private async Task DeleteAsync(AlgoliaResolvedStore store, IReadOnlyCollection<Guid> keys, CancellationToken ct)
    {
        if (keys.Count == 0)
            return;

        _logger.LogDebug("Algolia delete requested for store {Store} with {Count} product keys.", store.Alias, keys.Count);

        var ids = keys.Select(k => k.ToString()).ToList();
        var batchSize = _options.Indexing.BatchSize <= 0 ? 1000 : _options.Indexing.BatchSize;

        foreach (var target in store.ExpandIndexTargets())
        {
            ct.ThrowIfCancellationRequested();
            var indexName = _indexNameBuilder.BuildPrimary("products", target);
            await EnsureIndexSettingsAsync(target, indexName, ct).ConfigureAwait(false);

            _logger.LogDebug(
                "Algolia delete {Count} products from {IndexName} for locale {Locale} currency {Currency}",
                ids.Count,
                indexName,
                target.Locale,
                target.Currency);

            if (_options.Indexing.Variants)
            {
                await DeleteByProductIdsAsync(indexName, keys, waitForTasks: false, ct).ConfigureAwait(false);
            }
            else
            {
                await _client.DeleteObjectsAsync(
                    indexName: indexName,
                    objectIDs: ids,
                    batchSize: batchSize,
                    waitForTasks: false,
                    options: null,
                    cancellationToken: ct).ConfigureAwait(false);
            }

            await EnsureQuerySuggestionsAsync(target, indexName, ct).ConfigureAwait(false);
        }

        _searchCacheVersions.InvalidateStore(store.Alias);
    }

    private async Task EnsureIndexSettingsAsync(AlgoliaResolvedStore store, string primaryIndexName, CancellationToken ct)
    {
        if (_options.Indexing.SortedReplicas.Count == 0 && !_options.Indexing.Variants)
            return;

        var replicas = _options.Indexing.SortedReplicas
            .Where(x => !string.IsNullOrWhiteSpace(x.Attribute))
            .Select(x => new
            {
                Options = x,
                Name = _indexNameBuilder.BuildReplica("products", x, store)
            })
            .DistinctBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (replicas.Count == 0 && !_options.Indexing.Variants)
            return;

        _logger.LogDebug(
            "Algolia configuring {ReplicaCount} replicas for {IndexName} in store {Store}.",
            replicas.Count,
            primaryIndexName,
            store.Alias);

        await _client.SetSettingsAsync(
            primaryIndexName,
            new IndexSettings
            {
                Replicas = replicas.Select(x => x.Name).ToList(),
                AttributeForDistinct = _options.Indexing.Variants ? "ProductId" : null,
                AttributesForFaceting = _options.Indexing.Variants ? ["filterOnly(ProductId)"] : null
            },
            forwardToReplicas: false,
            options: null,
            cancellationToken: ct).ConfigureAwait(false);

        foreach (var replica in replicas)
        {
            await _client.SetSettingsAsync(
                replica.Name,
                new IndexSettings
                {
                    Ranking = BuildReplicaRanking(replica.Options)
                },
                forwardToReplicas: false,
                options: null,
                cancellationToken: ct).ConfigureAwait(false);
        }
    }

    private async Task DeleteByProductIdsAsync(string indexName, IEnumerable<Guid> productKeys, bool waitForTasks, CancellationToken ct)
    {
        foreach (var productKey in productKeys.Distinct())
        {
            ct.ThrowIfCancellationRequested();

            var response = await _client.DeleteByAsync(
                indexName,
                new DeleteByParams
                {
                    Filters = $"ProductId:{productKey}"
                },
                options: null,
                cancellationToken: ct).ConfigureAwait(false);

            if (waitForTasks)
                await _client.WaitForTaskAsync(indexName, response.TaskID, 100, null, null, ct).ConfigureAwait(false);
        }
    }

    private Task EnsureQuerySuggestionsAsync(AlgoliaResolvedStore store, string primaryIndexName, CancellationToken ct)
        => _querySuggestionsConfigurator.EnsureConfiguredAsync(store, primaryIndexName, ct);

    private static List<string> BuildReplicaRanking(AlgoliaSortedReplicaOptions replica)
    {
        var direction = replica.Direction == AlgoliaSortDirection.Desc ? "desc" : "asc";

        return
        [
            $"{direction}({replica.Attribute})",
            "typo",
            "geo",
            "words",
            "filters",
            "proximity",
            "attribute",
            "exact",
            "custom"
        ];
    }
}
