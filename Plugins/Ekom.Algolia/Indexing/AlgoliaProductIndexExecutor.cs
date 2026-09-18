using Ekom.Algolia.Mappers;
using Ekom.Algolia.Models.Indexing;
using Ekom.Algolia.Services;
using Ekom.API;
using Ekom.Models;
using Algolia.Search.Models.Search;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Algolia.Search.Clients;
using System.Diagnostics;

namespace Ekom.Algolia.Indexing;

internal sealed class AlgoliaProductIndexExecutor
{
    private readonly ISearchClient _client;
    private readonly AlgoliaIndexReplacementService _indexReplacementService;
    private readonly IAlgoliaTransformationWriteService _transformationWrites;
    private readonly AlgoliaOptions _options;
    private readonly AlgoliaStoreResolver _storeResolver;
    private readonly IndexNameBuilder _indexNameBuilder;
    private readonly IAlgoliaQuerySuggestionsConfigurator _querySuggestionsConfigurator;
    private readonly AlgoliaSearchCacheVersionProvider _searchCacheVersions;
    private readonly IAlgoliaProductIndexMapper _mapper;
    private readonly IReadOnlyList<IAlgoliaProductIndexFilter> _filters;
    private readonly ILogger<AlgoliaProductIndexExecutor> _logger;

    public AlgoliaProductIndexExecutor(
        ISearchClient client,
        AlgoliaIndexReplacementService indexReplacementService,
        IAlgoliaTransformationWriteService transformationWrites,
        IOptions<AlgoliaOptions> options,
        AlgoliaStoreResolver storeResolver,
        IndexNameBuilder indexNameBuilder,
        IAlgoliaQuerySuggestionsConfigurator querySuggestionsConfigurator,
        AlgoliaSearchCacheVersionProvider searchCacheVersions,
        IAlgoliaProductIndexMapper mapper,
        IEnumerable<IAlgoliaProductIndexFilter>? filters,
        ILogger<AlgoliaProductIndexExecutor> logger)
    {
        _client = client;
        _indexReplacementService = indexReplacementService;
        _transformationWrites = transformationWrites;
        _options = options.Value;
        _storeResolver = storeResolver;
        _indexNameBuilder = indexNameBuilder;
        _querySuggestionsConfigurator = querySuggestionsConfigurator;
        _searchCacheVersions = searchCacheVersions;
        _mapper = mapper;
        _filters = (filters ?? Array.Empty<IAlgoliaProductIndexFilter>()).ToList();
        _logger = logger;
    }

    public async Task HandleAsync(IReadOnlyCollection<AlgoliaProductIndexJob> jobs, CancellationToken ct)
    {
        if (jobs.Count == 0)
            return;

        if (!_options.Enabled)
            return;

        _logger.LogDebug("Algolia executor handling {Count} queued jobs.", jobs.Count);

        var byStore = jobs.GroupBy(j => j.StoreAlias, StringComparer.OrdinalIgnoreCase);

        foreach (var storeGroup in byStore)
        {
            ct.ThrowIfCancellationRequested();

            var storeAlias = storeGroup.Key;
            var store = _storeResolver.Resolve(storeAlias);
            var storeJobs = storeGroup.ToList();

            if (!store.Indexing.Enabled || !store.Indexing.Products)
                continue;

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
        if (!store.Indexing.Enabled || !store.Indexing.Products)
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
                if (!ShouldIndex(product, target))
                {
                    skippedProducts++;
                    continue;
                }

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

            var batchSize = store.Indexing.BatchSize <= 0 ? 1000 : store.Indexing.BatchSize;

            var stopwatch = Stopwatch.StartNew();
            var configuredWriteMode = target.Collections.Enabled ? "CollectionsPreferred" : "SearchApi";

            _logger.LogInformation(
                "Algolia product index rebuild started. IndexName={IndexName} Store={Store} Locale={Locale} Currency={Currency} Records={RecordCount} SkippedProducts={SkippedProductCount} ConfiguredWriteMode={ConfiguredWriteMode}",
                indexName,
                target.Alias,
                target.Locale,
                target.Currency,
                records.Count,
                skippedProducts,
                configuredWriteMode);

            try
            {
                await _indexReplacementService.ReplaceAllAsync(
                    indexName,
                    records,
                    batchSize,
                    target.Collections.Enabled,
                    ct).ConfigureAwait(false);

                await EnsureIndexSettingsAsync(target, indexName, ct).ConfigureAwait(false);
                await EnsureQuerySuggestionsAsync(target, indexName, ct).ConfigureAwait(false);

                _logger.LogInformation(
                    "Algolia product index rebuild completed. IndexName={IndexName} Store={Store} Locale={Locale} Currency={Currency} Records={RecordCount} ConfiguredWriteMode={ConfiguredWriteMode} DurationMilliseconds={DurationMilliseconds}",
                    indexName,
                    target.Alias,
                    target.Locale,
                    target.Currency,
                    records.Count,
                    configuredWriteMode,
                    stopwatch.ElapsedMilliseconds);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Algolia product index rebuild failed. IndexName={IndexName} Store={Store} Locale={Locale} Currency={Currency} Records={RecordCount} ConfiguredWriteMode={ConfiguredWriteMode} DurationMilliseconds={DurationMilliseconds}",
                    indexName,
                    target.Alias,
                    target.Locale,
                    target.Currency,
                    records.Count,
                    configuredWriteMode,
                    stopwatch.ElapsedMilliseconds);
                throw;
            }
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

        var batchSize = store.Indexing.BatchSize <= 0 ? 1000 : store.Indexing.BatchSize;

        foreach (var target in store.ExpandIndexTargets())
        {
            ct.ThrowIfCancellationRequested();
            var indexName = _indexNameBuilder.BuildPrimary("products", target);
            await EnsureIndexSettingsAsync(target, indexName, ct).ConfigureAwait(false);
            var records = new List<AlgoliaProductRecord>(products.Count);
            var indexedProductKeys = new List<Guid>(products.Count);

            var skippedProducts = 0;

            foreach (var product in products)
            {
                ct.ThrowIfCancellationRequested();
                if (!ShouldIndex(product, target))
                {
                    skippedProducts++;
                    continue;
                }

                var mappedRecords = _mapper.MapRecords(product, target, indexName);
                if (mappedRecords.Count > 0)
                {
                    records.AddRange(mappedRecords);
                    indexedProductKeys.Add(product.Key);
                }
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

            var stopwatch = Stopwatch.StartNew();
            var configuredWriteMode = target.Collections.Enabled ? "CollectionsPreferred" : "SearchApi";
            _logger.LogInformation(
                "Algolia product index update started. IndexName={IndexName} Store={Store} Locale={Locale} Currency={Currency} Records={RecordCount} ConfiguredWriteMode={ConfiguredWriteMode}",
                indexName,
                target.Alias,
                target.Locale,
                target.Currency,
                records.Count,
                configuredWriteMode);

            try
            {
                if (target.Indexing.Variants)
                    await DeleteByProductIdsAsync(indexName, indexedProductKeys, waitForTasks: true, ct).ConfigureAwait(false);

                await SaveProductRecordsAsync(
                    _client,
                    _transformationWrites,
                    indexName,
                    records,
                    batchSize,
                    target.Collections.Enabled,
                    ct).ConfigureAwait(false);

                await EnsureQuerySuggestionsAsync(target, indexName, ct).ConfigureAwait(false);

                _logger.LogInformation(
                    "Algolia product index update completed. IndexName={IndexName} Store={Store} Locale={Locale} Currency={Currency} Records={RecordCount} ConfiguredWriteMode={ConfiguredWriteMode} DurationMilliseconds={DurationMilliseconds}",
                    indexName,
                    target.Alias,
                    target.Locale,
                    target.Currency,
                    records.Count,
                    configuredWriteMode,
                    stopwatch.ElapsedMilliseconds);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Algolia product index update failed. IndexName={IndexName} Store={Store} Locale={Locale} Currency={Currency} Records={RecordCount} ConfiguredWriteMode={ConfiguredWriteMode} DurationMilliseconds={DurationMilliseconds}",
                    indexName,
                    target.Alias,
                    target.Locale,
                    target.Currency,
                    records.Count,
                    configuredWriteMode,
                    stopwatch.ElapsedMilliseconds);
                throw;
            }
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
        var batchSize = store.Indexing.BatchSize <= 0 ? 1000 : store.Indexing.BatchSize;

        foreach (var target in store.ExpandIndexTargets())
        {
            ct.ThrowIfCancellationRequested();
            var indexName = _indexNameBuilder.BuildPrimary("products", target);

            _logger.LogInformation(
                "Algolia product index delete started. IndexName={IndexName} Store={Store} Locale={Locale} Currency={Currency} Products={ProductCount}",
                indexName,
                target.Alias,
                target.Locale,
                target.Currency,
                ids.Count);

            await EnsureIndexSettingsAsync(target, indexName, ct).ConfigureAwait(false);

            if (target.Indexing.Variants)
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

            _logger.LogInformation(
                "Algolia product index delete submitted. IndexName={IndexName} Store={Store} Locale={Locale} Currency={Currency} Products={ProductCount}",
                indexName,
                target.Alias,
                target.Locale,
                target.Currency,
                ids.Count);

            _logger.LogDebug(
                "Algolia delete {Count} products from index {IndexName} for locale {Locale} currency {Currency}",
                ids.Count,
                indexName,
                target.Locale,
                target.Currency);

            await EnsureQuerySuggestionsAsync(target, indexName, ct).ConfigureAwait(false);
        }

        _searchCacheVersions.InvalidateStore(store.Alias);
    }

    private async Task EnsureIndexSettingsAsync(AlgoliaResolvedStore store, string primaryIndexName, CancellationToken ct)
    {
        var indexing = store.Indexing;
        var attributesForFaceting = BuildAttributesForFaceting(indexing);
        if (store.Collections.Enabled)
            EnsureCollectionsFacet(attributesForFaceting);

        var searchableAttributes = BuildSearchableAttributes(store.SearchableAttributes);
        var customRanking = AlgoliaCustomRankingSettings.Normalize(indexing.ProductCustomRanking);
        var hasLanguageSettings = HasLanguageSettings(store.LanguageSettings);
        if (!store.HasIndexingOverride
            && indexing.SortedReplicas.Count == 0
            && attributesForFaceting.Count == 0
            && searchableAttributes is null
            && customRanking is null
            && !hasLanguageSettings)
            return;

        var replicas = indexing.SortedReplicas
            .Where(x => !string.IsNullOrWhiteSpace(x.Attribute))
            .Select(x => new
            {
                Options = x,
                Name = _indexNameBuilder.BuildReplica("products", x, store)
            })
            .DistinctBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!store.HasIndexingOverride
            && replicas.Count == 0
            && attributesForFaceting.Count == 0
            && searchableAttributes is null
            && customRanking is null
            && !hasLanguageSettings)
            return;

        _logger.LogDebug(
            "Algolia configuring {ReplicaCount} replicas for index {IndexName} in store {Store}. ReplicaIndexNames={ReplicaIndexNames}",
            replicas.Count,
            primaryIndexName,
            store.Alias,
            replicas.Select(x => x.Name).ToArray());

        var primarySettings = new IndexSettings
        {
            Replicas = replicas.Select(x => BuildReplicaReference(x.Name, x.Options)).ToList(),
            AttributeForDistinct = indexing.Variants ? "ProductId" : null,
            AttributesForFaceting = attributesForFaceting.Count > 0 || store.HasIndexingOverride
                ? attributesForFaceting
                : null,
            SearchableAttributes = searchableAttributes,
            CustomRanking = customRanking,
        };
        ApplyLanguageSettings(primarySettings, store);

        await _client.SetSettingsAsync(
            primaryIndexName,
            primarySettings,
            forwardToReplicas: false,
            options: null,
            cancellationToken: ct).ConfigureAwait(false);

        foreach (var replica in replicas)
        {
            var replicaSettings = BuildReplicaSettings(
                replica.Options,
                indexing.Variants,
                attributesForFaceting,
                store,
                searchableAttributes);

            await _client.SetSettingsAsync(
                replica.Name,
                replicaSettings,
                forwardToReplicas: false,
                options: null,
                cancellationToken: ct).ConfigureAwait(false);
        }
    }

    internal static string BuildReplicaReference(string replicaName, AlgoliaSortedReplicaOptions replica)
        => replica.Type == AlgoliaReplicaType.Virtual
            ? $"virtual({replicaName})"
            : replicaName;

    internal static Task SaveProductRecordsAsync<T>(
        ISearchClient client,
        IAlgoliaTransformationWriteService transformationWrites,
        string indexName,
        IReadOnlyCollection<T> records,
        int batchSize,
        bool useTransformation,
        CancellationToken ct)
        where T : class
        => useTransformation
            ? transformationWrites.SaveAsync(
                indexName,
                records,
                batchSize,
                chunkedOptions: null,
                ct)
            : client.SaveObjectsAsync(
                indexName: indexName,
                objects: records,
                waitForTasks: true,
                batchSize: batchSize,
                options: null,
                cancellationToken: ct);

    internal static IndexSettings BuildReplicaSettings(
        AlgoliaSortedReplicaOptions replica,
        bool variants,
        List<string> attributesForFaceting,
        AlgoliaResolvedStore store,
        List<string>? searchableAttributes = null)
    {
        var settings = replica.Type == AlgoliaReplicaType.Virtual
            ? new IndexSettings
            {
                CustomRanking = [BuildReplicaSort(replica)],
            }
            : new IndexSettings
            {
                Ranking = BuildReplicaRanking(replica),
                AttributeForDistinct = variants ? "ProductId" : null,
                AttributesForFaceting = attributesForFaceting.Count > 0 || store.HasIndexingOverride
                    ? attributesForFaceting
                    : null,
                SearchableAttributes = searchableAttributes,
            };

        ApplyLanguageSettings(settings, store, includeIndexLanguages: replica.Type == AlgoliaReplicaType.Standard);
        return settings;
    }

    internal static List<string>? BuildSearchableAttributes(IReadOnlyCollection<string>? attributes)
    {
        if (attributes is null || attributes.Count == 0)
            return null;

        var searchableAttributes = attributes
            .Where(attribute => !string.IsNullOrWhiteSpace(attribute))
            .Select(attribute => attribute.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return searchableAttributes.Count > 0 ? searchableAttributes : null;
    }

    internal static void ApplyLanguageSettings(IndexSettings indexSettings, AlgoliaResolvedStore store)
        => ApplyLanguageSettings(indexSettings, store, includeIndexLanguages: true);

    private static void ApplyLanguageSettings(
        IndexSettings indexSettings,
        AlgoliaResolvedStore store,
        bool includeIndexLanguages)
    {
        var languageSettings = store.LanguageSettings;

        indexSettings.QueryLanguages = ParseLanguages(
            languageSettings.QueryLanguages,
            nameof(languageSettings.QueryLanguages),
            store.Alias);
        indexSettings.IndexLanguages = includeIndexLanguages
            ? ParseLanguages(
                languageSettings.IndexLanguages,
                nameof(languageSettings.IndexLanguages),
                store.Alias)
            : null;
        indexSettings.RemoveStopWords = languageSettings.RemoveStopWords.HasValue
            ? new RemoveStopWords(languageSettings.RemoveStopWords.Value)
            : null;
        indexSettings.IgnorePlurals = BuildIgnorePlurals(languageSettings, store.Alias);
    }

    internal static bool HasLanguageSettings(AlgoliaLanguageSettingsOptions settings)
        => settings.QueryLanguages.Count > 0
            || settings.IndexLanguages.Count > 0
            || settings.RemoveStopWords.HasValue
            || settings.IgnorePlurals.HasValue;

    private static IgnorePlurals? BuildIgnorePlurals(
        AlgoliaLanguageSettingsOptions settings,
        string storeAlias)
    {
        if (!settings.IgnorePlurals.HasValue)
            return null;

        if (!settings.IgnorePlurals.Value)
            return new IgnorePlurals(false);

        var languages = ParseLanguages(
            settings.IgnorePluralsLanguages,
            nameof(settings.IgnorePluralsLanguages),
            storeAlias);

        return languages is { Count: > 0 }
            ? new IgnorePlurals(languages)
            : new IgnorePlurals(true);
    }

    internal static bool ShouldIndex(
        IProduct product,
        AlgoliaResolvedStore store,
        IEnumerable<IAlgoliaProductIndexFilter> filters)
        => filters.All(filter => filter.ShouldIndex(product, store));

    private bool ShouldIndex(IProduct product, AlgoliaResolvedStore store)
        => ShouldIndex(product, store, _filters);

    private static List<SupportedLanguage>? ParseLanguages(
        IReadOnlyCollection<string> languages,
        string settingName,
        string storeAlias)
    {
        if (languages.Count == 0)
            return null;

        var parsedLanguages = new List<SupportedLanguage>(languages.Count);
        foreach (var language in languages)
        {
            var value = language?.Trim();
            if (string.IsNullOrWhiteSpace(value)
                || !Enum.TryParse(value, ignoreCase: true, out SupportedLanguage parsedLanguage)
                || !Enum.IsDefined(parsedLanguage))
            {
                throw new InvalidOperationException(
                    $"Unsupported Algolia language '{language}' configured for store '{storeAlias}' in '{settingName}'. Use a supported ISO 639-1 language code.");
            }

            if (!parsedLanguages.Contains(parsedLanguage))
                parsedLanguages.Add(parsedLanguage);
        }

        return parsedLanguages;
    }

    internal static List<string> BuildAttributesForFaceting(AlgoliaIndexingOptions options)
    {
        var generatedAttributes = options.FacetAttributes
            .Select(ProductIndexMapper.ConfiguredField.Parse)
            .Select(field => field.Alias)
            .Concat(options.VariantFacetAttributes
                .Select(x => ProductIndexMapper.ConfiguredVariantFacet.Parse(x.Key, x.Value))
                .Where(attribute => !string.IsNullOrWhiteSpace(attribute.Field.Alias))
                .Select(attribute => attribute.OutputAlias))
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Select(alias => alias.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var attributesForFaceting = options.AttributesForFaceting
            .Where(attribute => !string.IsNullOrWhiteSpace(attribute))
            .Select(attribute => attribute.Trim())
            .ToList();

        if (options.Variants)
        {
            attributesForFaceting.Add("filterOnly(ProductId)");
            attributesForFaceting.Add("filterOnly(categoryPageId)");
        }

        attributesForFaceting.AddRange(generatedAttributes.Select(attribute =>
            options.Variants
                ? $"afterDistinct(attributes.{attribute})"
                : $"attributes.{attribute}"));

        return attributesForFaceting
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static void EnsureCollectionsFacet(List<string> attributesForFaceting)
    {
        if (!attributesForFaceting.Any(IsCollectionsFacet))
            attributesForFaceting.Add("_collections");
    }

    private static bool IsCollectionsFacet(string expression)
    {
        var value = expression.Trim();
        while (value.EndsWith(')'))
        {
            var openParenthesis = value.IndexOf("(", StringComparison.Ordinal);
            if (openParenthesis < 0)
                break;

            value = value[(openParenthesis + 1)..^1].Trim();
        }

        return value.Equals("_collections", StringComparison.Ordinal);
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
        return
        [
            BuildReplicaSort(replica),
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

    private static string BuildReplicaSort(AlgoliaSortedReplicaOptions replica)
    {
        var direction = replica.Direction == AlgoliaSortDirection.Desc ? "desc" : "asc";
        return $"{direction}({replica.Attribute})";
    }
}
