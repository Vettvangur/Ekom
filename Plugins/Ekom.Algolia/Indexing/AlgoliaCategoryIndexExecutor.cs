using Algolia.Search.Clients;
using Ekom.Algolia.Mappers;
using Ekom.Algolia.Models.Indexing;
using Ekom.Algolia.Services;
using Ekom.API;
using Ekom.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ekom.Algolia.Indexing;

internal sealed class AlgoliaCategoryIndexExecutor
{
    private const string CategoriesEntity = "categories";

    private readonly ISearchClient _client;
    private readonly AlgoliaOptions _options;
    private readonly AlgoliaStoreResolver _storeResolver;
    private readonly IndexNameBuilder _indexNameBuilder;
    private readonly AlgoliaSearchCacheVersionProvider _searchCacheVersions;
    private readonly IAlgoliaCategoryIndexMapper _mapper;
    private readonly ILogger<AlgoliaCategoryIndexExecutor> _logger;

    public AlgoliaCategoryIndexExecutor(
        ISearchClient client,
        IOptions<AlgoliaOptions> options,
        AlgoliaStoreResolver storeResolver,
        IndexNameBuilder indexNameBuilder,
        AlgoliaSearchCacheVersionProvider searchCacheVersions,
        IAlgoliaCategoryIndexMapper mapper,
        ILogger<AlgoliaCategoryIndexExecutor> logger)
    {
        _client = client;
        _options = options.Value;
        _storeResolver = storeResolver;
        _indexNameBuilder = indexNameBuilder;
        _searchCacheVersions = searchCacheVersions;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task HandleAsync(IReadOnlyCollection<AlgoliaCategoryIndexJob> jobs, CancellationToken ct)
    {
        if (jobs.Count == 0)
            return;

        if (!_options.Enabled || !_options.Indexing.Enabled || !_options.Indexing.Categories)
            return;

        var byStore = jobs.GroupBy(j => j.StoreAlias, StringComparer.OrdinalIgnoreCase);

        foreach (var storeGroup in byStore)
        {
            ct.ThrowIfCancellationRequested();

            var store = _storeResolver.Resolve(storeGroup.Key);
            var storeJobs = storeGroup.ToList();

            if (storeJobs.Any(j => j.Type == AlgoliaCategoryIndexJobType.RebuildStore))
            {
                await RebuildStoreAsync(store, ct).ConfigureAwait(false);
                continue;
            }

            var upsertKeys = new HashSet<Guid>();
            var deleteKeys = new HashSet<Guid>();

            foreach (var job in storeJobs)
            {
                if (job.Type == AlgoliaCategoryIndexJobType.Delete)
                {
                    foreach (var key in job.CategoryKeys)
                        deleteKeys.Add(key);

                    continue;
                }

                foreach (var key in job.CategoryKeys)
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

    private async Task RebuildStoreAsync(AlgoliaResolvedStore store, CancellationToken ct)
    {
        var categories = await Catalog.Instance.GetAllCategoriesAsync(store.Alias, ct).ConfigureAwait(false);
        if (categories.Count == 0)
            return;

        foreach (var target in ExpandTargets(store))
        {
            ct.ThrowIfCancellationRequested();

            var indexName = _indexNameBuilder.BuildPrimary(CategoriesEntity, target, currencyOverride: string.Empty);
            var records = categories
                .Select(category => _mapper.Map(category, target, indexName))
                .Where(record => record is not null)
                .Cast<AlgoliaCategoryRecord>()
                .ToList();

            if (records.Count == 0)
                continue;

            var batchSize = _options.Indexing.BatchSize <= 0 ? 1000 : _options.Indexing.BatchSize;

            await _client.ReplaceAllObjectsAsync(
                indexName: indexName,
                objects: records,
                batchSize: batchSize,
                cancellationToken: ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Algolia rebuild store {Store} locale {Locale} -> {IndexName}. Categories={Count}",
                target.Alias,
                target.Locale,
                indexName,
                records.Count);
        }

        _searchCacheVersions.InvalidateStore(store.Alias);
    }

    private async Task UpsertAsync(AlgoliaResolvedStore store, IReadOnlyCollection<Guid> keys, CancellationToken ct)
    {
        var missingKeys = new List<Guid>();
        var categories = new List<ICategory>(keys.Count);

        foreach (var key in keys)
        {
            ct.ThrowIfCancellationRequested();

            var category = await Catalog.Instance.GetCategoryAsync(key, store.Alias, raiseEvent: false, ct: ct).ConfigureAwait(false);
            if (category == null)
            {
                missingKeys.Add(key);
                continue;
            }

            categories.Add(category);
        }

        if (categories.Count == 0)
        {
            if (missingKeys.Count > 0)
                await DeleteAsync(store, missingKeys, ct).ConfigureAwait(false);

            return;
        }

        var batchSize = _options.Indexing.BatchSize <= 0 ? 1000 : _options.Indexing.BatchSize;

        foreach (var target in ExpandTargets(store))
        {
            ct.ThrowIfCancellationRequested();

            var indexName = _indexNameBuilder.BuildPrimary(CategoriesEntity, target, currencyOverride: string.Empty);
            var records = categories
                .Select(category => _mapper.Map(category, target, indexName))
                .Where(record => record is not null)
                .Cast<AlgoliaCategoryRecord>()
                .ToList();

            if (records.Count > 0)
            {
                await _client.SaveObjectsAsync(
                    indexName: indexName,
                    objects: records,
                    waitForTasks: false,
                    batchSize: batchSize,
                    options: null,
                    cancellationToken: ct).ConfigureAwait(false);
            }
        }

        if (missingKeys.Count > 0)
            await DeleteAsync(store, missingKeys, ct).ConfigureAwait(false);

        _searchCacheVersions.InvalidateStore(store.Alias);
    }

    private async Task DeleteAsync(AlgoliaResolvedStore store, IReadOnlyCollection<Guid> keys, CancellationToken ct)
    {
        if (keys.Count == 0)
            return;

        var objectIds = keys.Select(x => x.ToString()).ToList();

        foreach (var target in ExpandTargets(store))
        {
            ct.ThrowIfCancellationRequested();

            var indexName = _indexNameBuilder.BuildPrimary(CategoriesEntity, target, currencyOverride: string.Empty);
            await _client.DeleteObjectsAsync(
                indexName,
                objectIds,
                waitForTasks: false,
                options: null,
                cancellationToken: ct).ConfigureAwait(false);
        }

        _searchCacheVersions.InvalidateStore(store.Alias);
    }

    private static IReadOnlyList<AlgoliaResolvedStore> ExpandTargets(AlgoliaResolvedStore store)
    {
        var locales = store.Locales.Count > 0
            ? store.Locales
            : [store.Locale ?? string.Empty];

        return locales
            .Select(locale => store.WithSelection(locale, currency: null))
            .DistinctBy(x => x.Locale, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
