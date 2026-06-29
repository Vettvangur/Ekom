using Algolia.Search.Clients;
using Ekom.Algolia.Mappers;
using Ekom.Algolia.Models.Indexing;
using Ekom.Algolia.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Infrastructure.Persistence.Querying;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Extensions;

namespace Ekom.Algolia.Indexing;

internal sealed class AlgoliaContentIndexExecutor
{
    private readonly ISearchClient _client;
    private readonly AlgoliaOptions _options;
    private readonly IReadOnlyList<IAlgoliaContentEnricher> _enrichers;
    private readonly IReadOnlyList<IAlgoliaContentPropertyValueConverter> _propertyConverters;
    private readonly IContentService _contentService;
    private readonly ILocalizationService _languageService;
    private readonly PropertyEditorCollection _propertyEditors;
    private readonly IContentTypeService _contentTypeService;
    private readonly IPublishedUrlProvider _urlProvider;
    private readonly IUmbracoContextFactory _umbracoContextFactory;
    private readonly IScopeProvider _scopeProvider;
    private readonly IMemoryCache _cache;
    private readonly ContentIndexNameResolver _indexNameResolver;
    private readonly AlgoliaSearchCacheVersionProvider _searchCacheVersions;
    private readonly ILogger<AlgoliaContentIndexExecutor> _logger;

    public AlgoliaContentIndexExecutor(
        ISearchClient client,
        IOptions<AlgoliaOptions> options,
        IContentService contentService,
        ILocalizationService languageService,
        PropertyEditorCollection propertyEditors,
        IContentTypeService contentTypeService,
        IPublishedUrlProvider urlProvider,
        IUmbracoContextFactory umbracoContextFactory,
        IScopeProvider scopeProvider,
        IMemoryCache cache,
        ContentIndexNameResolver indexNameResolver,
        AlgoliaSearchCacheVersionProvider searchCacheVersions,
        ILogger<AlgoliaContentIndexExecutor> logger,
        IEnumerable<IAlgoliaContentEnricher>? enrichers = null,
        IEnumerable<IAlgoliaContentPropertyValueConverter>? propertyConverters = null)
    {
        _client = client;
        _options = options.Value;
        _contentService = contentService;
        _languageService = languageService;
        _propertyEditors = propertyEditors;
        _contentTypeService = contentTypeService;
        _urlProvider = urlProvider;
        _umbracoContextFactory = umbracoContextFactory;
        _scopeProvider = scopeProvider;
        _cache = cache;
        _indexNameResolver = indexNameResolver;
        _searchCacheVersions = searchCacheVersions;
        _logger = logger;
        _enrichers = (enrichers ?? []).OrderBy(x => x.Order).ToList();
        _propertyConverters = (propertyConverters ?? []).OrderBy(x => x.Order).ToList();
    }

    public async Task HandleAsync(IReadOnlyCollection<AlgoliaContentIndexJob> jobs, CancellationToken ct)
    {
        if (jobs.Count == 0 || !_options.Enabled || !_options.ContentIndexing.Enabled)
            return;

        if (jobs.Any(x => x.Type == AlgoliaContentIndexJobType.Rebuild))
        {
            foreach (var indexName in jobs.Where(x => x.Type == AlgoliaContentIndexJobType.Rebuild).Select(x => x.IndexName).Distinct(StringComparer.OrdinalIgnoreCase))
                await RebuildAsync(indexName, ct).ConfigureAwait(false);

            return;
        }

        var upsertIds = jobs
            .Where(x => x.Type == AlgoliaContentIndexJobType.Upsert)
            .SelectMany(x => x.NodeIds)
            .Distinct()
            .ToArray();

        var deleteKeys = jobs
            .Where(x => x.Type == AlgoliaContentIndexJobType.Delete)
            .SelectMany(x => x.NodeKeys)
            .Distinct()
            .ToArray();

        if (upsertIds.Length > 0)
            await UpdateByIdsAsync(upsertIds, ct).ConfigureAwait(false);

        if (deleteKeys.Length > 0)
            await DeleteByKeysAsync(deleteKeys, ct).ConfigureAwait(false);
    }

    private async Task RebuildAsync(string? indexName, CancellationToken ct)
    {
        var indexes = string.IsNullOrWhiteSpace(indexName)
            ? _options.ContentIndexing.Indexes
            : _options.ContentIndexing.Indexes.Where(x => x.IndexName.Equals(indexName, StringComparison.OrdinalIgnoreCase)).ToList();

        var allCultures = await GetAllCulturesAsync().ConfigureAwait(false);
        var contentTypes = await GetContentTypesAsync().ConfigureAwait(false);
        var documentsByIndex = new Dictionary<string, List<AlgoliaContentRecord>>(StringComparer.OrdinalIgnoreCase);

        foreach (var index in indexes)
        {
            foreach (var contentTypeAlias in GetConfiguredAliases(index))
            {
                ct.ThrowIfCancellationRequested();

                using var ctx = _umbracoContextFactory.EnsureUmbracoContext();
                var publishedContentType = ctx.UmbracoContext.Content?.GetContentType(contentTypeAlias);
                if (publishedContentType is null)
                    continue;

                var entities = GetAllOfType(publishedContentType.Id);
                var (upsertsByCulture, _) = BuildPerCultureBuckets(entities, allCultures);

                foreach (var (culture, content) in upsertsByCulture)
                {
                    var resolvedIndexName = _indexNameResolver.Resolve(index.IndexName, culture);
                    if (!documentsByIndex.TryGetValue(resolvedIndexName, out var documents))
                    {
                        documents = [];
                        documentsByIndex[resolvedIndexName] = documents;
                    }

                    documents.AddRange(MapMany(content, culture, index, allCultures, contentTypes));
                }
            }
        }

        foreach (var (resolvedIndexName, documents) in documentsByIndex)
        {
            ct.ThrowIfCancellationRequested();
            var batchSize = _options.ContentIndexing.BatchSize <= 0 ? 1000 : _options.ContentIndexing.BatchSize;
            await _client.ReplaceAllObjectsAsync(resolvedIndexName, documents, batchSize, cancellationToken: ct).ConfigureAwait(false);
            _logger.LogInformation("Rebuilt Algolia content index {IndexName} with {Count} documents.", resolvedIndexName, documents.Count);
        }

        _searchCacheVersions.InvalidateStore("content");
    }

    private async Task UpdateByIdsAsync(IReadOnlyCollection<int> nodeIds, CancellationToken ct)
    {
        var entities = _contentService.GetByIds(nodeIds).Where(x => !x.Trashed).ToList();
        if (entities.Count == 0)
            return;

        var allCultures = await GetAllCulturesAsync().ConfigureAwait(false);
        var contentTypes = await GetContentTypesAsync().ConfigureAwait(false);

        foreach (var index in _options.ContentIndexing.Indexes)
        {
            var configuredAliases = GetConfiguredAliases(index);
            var indexEntities = entities.Where(x => configuredAliases.Contains(x.ContentType.Alias)).DistinctBy(x => x.Id).ToList();
            if (indexEntities.Count == 0)
                continue;

            var (upsertsByCulture, deletesByCulture) = BuildPerCultureBuckets(indexEntities, allCultures);

            foreach (var (culture, content) in upsertsByCulture)
            {
                var documents = MapMany(content, culture, index, allCultures, contentTypes).ToList();
                if (documents.Count == 0)
                    continue;

                var indexName = _indexNameResolver.Resolve(index.IndexName, culture);
                await _client.SaveObjectsAsync(indexName, documents, batchSize: 1000, waitForTasks: true, cancellationToken: ct).ConfigureAwait(false);
            }

            foreach (var (culture, keys) in deletesByCulture)
                await DeleteAsync(keys, culture, index, ct).ConfigureAwait(false);
        }

        _searchCacheVersions.InvalidateStore("content");
    }

    private async Task DeleteByKeysAsync(IReadOnlyCollection<Guid> keys, CancellationToken ct)
    {
        if (keys.Count == 0)
            return;

        var allCultures = await GetAllCulturesAsync().ConfigureAwait(false);
        var objectIds = keys.Select(x => x.ToString()).ToArray();

        foreach (var index in _options.ContentIndexing.Indexes)
        {
            foreach (var culture in allCultures)
                await DeleteAsync(objectIds, culture, index, ct).ConfigureAwait(false);
        }

        _searchCacheVersions.InvalidateStore("content");
    }

    private async Task DeleteAsync(IEnumerable<string> objectIds, string culture, AlgoliaContentIndexOptions index, CancellationToken ct)
    {
        var ids = objectIds.ToArray();
        if (ids.Length == 0 || string.IsNullOrWhiteSpace(culture))
            return;

        var indexName = _indexNameResolver.Resolve(index.IndexName, culture);
        await _client.DeleteObjectsAsync(indexName, ids, batchSize: 1000, waitForTasks: false, cancellationToken: ct).ConfigureAwait(false);
    }

    private List<IContent> GetAllOfType(int contentTypeId)
    {
        var entities = new List<IContent>();
        const int pageSize = 250;
        var page = 0;
        long total;
        var filter = new Query<IContent>(_scopeProvider.SqlContext).Where(x => !x.Trashed);

        do
        {
            var pageItems = _contentService.GetPagedOfType(contentTypeId, page, pageSize, out total, filter).Where(x => !x.Trashed);
            entities.AddRange(pageItems);
            page++;
        } while (entities.Count < total);

        return entities;
    }

    private IEnumerable<AlgoliaContentRecord> MapMany(
        IEnumerable<IContent> content,
        string culture,
        AlgoliaContentIndexOptions index,
        IEnumerable<string> availableCultures,
        IDictionary<Guid, IContentType> contentTypes)
    {
        foreach (var item in content)
        {
            var allowedProperties = GetAllowedProperties(index, item);
            var record = MapAndEnrich(item, culture, index.IndexName, allowedProperties, availableCultures, contentTypes);
            if (record is not null)
                yield return record;
        }
    }

    private AlgoliaContentRecord? MapAndEnrich(
        IContent content,
        string? culture,
        string baseIndexName,
        Dictionary<string, AlgoliaContentFieldTransform> allowedProperties,
        IEnumerable<string> availableCultures,
        IDictionary<Guid, IContentType> contentTypes)
    {
        var record = Map(content, culture, baseIndexName, allowedProperties, availableCultures, contentTypes);
        if (record is null)
            return null;

        var ctx = new AlgoliaContentEnrichmentContext(content, culture, baseIndexName, allowedProperties);
        foreach (var enricher in _enrichers)
            enricher.Enrich(record, ctx);

        record.RemoveReservedFields();
        return record;
    }

    private AlgoliaContentRecord? Map(
        IContent content,
        string? culture,
        string baseIndexName,
        Dictionary<string, AlgoliaContentFieldTransform> allowedProperties,
        IEnumerable<string> availableCultures,
        IDictionary<Guid, IContentType> contentTypes)
    {
        using var contextReference = _umbracoContextFactory.EnsureUmbracoContext();
        var variesByCulture = content.ContentType.Variations.VariesByCulture();
        var name = variesByCulture && culture != null ? content.GetCultureName(culture) ?? string.Empty : content.Name ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var url = variesByCulture && culture != null ? _urlProvider.GetUrl(content.Id, culture: culture) : _urlProvider.GetUrl(content.Id);
        var record = new AlgoliaContentRecord
        {
            ObjectID = content.Key.ToString(),
            NodeId = content.Id,
            ContentTypeAlias = content.ContentType.Alias,
            Url = url,
            Name = name,
            UpdateDate = content.UpdateDate,
            CreateDate = content.CreateDate,
            Data = new Dictionary<string, object>()
        };

        foreach (var (alias, transform) in allowedProperties)
        {
            var property = content.Properties.FirstOrDefault(x => x.Alias.Equals(alias, StringComparison.OrdinalIgnoreCase));
            if (property is null)
                continue;

            var propertyCulture = property.PropertyType.Variations.VariesByCulture() ? culture : null;
            var value = ReadPropertyValue(property, propertyCulture, availableCultures, contentTypes);
            var converted = ConvertProperty(new AlgoliaContentPropertyContext(content, property, propertyCulture, culture, baseIndexName), value);

            if (!HasIndexableValue(converted))
                continue;

            record.TryAddField(alias, converted!);

            var unixValue = transform switch
            {
                AlgoliaContentFieldTransform.UnixSeconds => TryToUnix(converted, unixMilliseconds: false),
                AlgoliaContentFieldTransform.UnixMilliseconds => TryToUnix(converted, unixMilliseconds: true),
                _ => null
            };

            if (unixValue is not null)
                record.TryAddField($"{alias}Unix", unixValue);
        }

        return record;
    }

    private object? ConvertProperty(AlgoliaContentPropertyContext ctx, object? value)
    {
        foreach (var converter in _propertyConverters)
        {
            if (converter.CanHandle(ctx))
                value = converter.Convert(ctx, value);
        }

        return value;
    }

    private object? ReadPropertyValue(
        IProperty property,
        string? culture,
        IEnumerable<string> availableCultures,
        IDictionary<Guid, IContentType> contentTypes)
    {
        var propertyEditor = _propertyEditors.FirstOrDefault(x => x.Alias == property.PropertyType.PropertyEditorAlias);
        if (propertyEditor is null)
            return null;

        var indexValues = propertyEditor.PropertyIndexValueFactory.GetIndexValues(property, culture, null, true, availableCultures, contentTypes);
        var firstValue = indexValues?
            .SelectMany(x => x.Value ?? [])
            .FirstOrDefault();

        return firstValue?.ToString() ?? string.Empty;
    }

    private Dictionary<string, AlgoliaContentFieldTransform> GetAllowedProperties(AlgoliaContentIndexOptions index, IContent content)
    {
        var configured = index.ContentTypes
            .FirstOrDefault(x => x.Alias.Equals(content.ContentType.Alias, StringComparison.OrdinalIgnoreCase))
            ?.Properties
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(ParseConfiguredField)
            .Where(x => !string.IsNullOrWhiteSpace(x.Alias))
            .GroupBy(x => x.Alias, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Last().Transform, StringComparer.OrdinalIgnoreCase);

        if (configured is not null && configured.Count > 0)
            return configured;

        return content.Properties
            .Select(x => x.Alias)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x, _ => AlgoliaContentFieldTransform.None, StringComparer.OrdinalIgnoreCase);
    }

    private static HashSet<string> GetConfiguredAliases(AlgoliaContentIndexOptions index)
        => index.ContentTypes
            .Select(x => x.Alias)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static (Dictionary<string, List<IContent>> UpsertsByCulture, Dictionary<string, HashSet<string>> DeletesByCulture) BuildPerCultureBuckets(
        IEnumerable<IContent> entities,
        IEnumerable<string> allCultures)
    {
        var upserts = new Dictionary<string, List<IContent>>(StringComparer.OrdinalIgnoreCase);
        var deletes = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var entity in entities)
        {
            if (entity.ContentType.Variations.VariesByCulture())
            {
                var cultures = entity.CultureInfos?.Values.Select(x => x.Culture).Where(x => !string.IsNullOrWhiteSpace(x)) ?? [];
                foreach (var culture in cultures)
                {
                    if (entity.IsCulturePublished(culture))
                        AddList(upserts, culture, entity);
                    else
                        AddSet(deletes, culture, entity.Key.ToString());
                }
            }
            else
            {
                foreach (var culture in allCultures.Where(x => !string.IsNullOrWhiteSpace(x)))
                {
                    if (entity.Published)
                        AddList(upserts, culture, entity);
                    else
                        AddSet(deletes, culture, entity.Key.ToString());
                }
            }
        }

        return (upserts, deletes);

        static void AddList(Dictionary<string, List<IContent>> dict, string culture, IContent content)
        {
            if (!dict.TryGetValue(culture, out var list))
            {
                list = [];
                dict[culture] = list;
            }

            list.Add(content);
        }

        static void AddSet(Dictionary<string, HashSet<string>> dict, string culture, string key)
        {
            if (!dict.TryGetValue(culture, out var set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                dict[culture] = set;
            }

            set.Add(key);
        }
    }

    private async Task<string[]> GetAllCulturesAsync()
        => await _cache.GetOrCreateAsync("algolia:content:languages", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            var cultures = _languageService.GetAllLanguages().Select(x => x.IsoCode).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            return Task.FromResult<string[]?>(cultures);
        }).ConfigureAwait(false) ?? [];

    private Task<Dictionary<Guid, IContentType>> GetContentTypesAsync()
        => _cache.GetOrCreateAsync("algolia:content:contenttypes", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            return Task.FromResult(_contentTypeService.GetAll().ToDictionary(x => x.Key));
        })!;

    private static bool HasIndexableValue(object? value)
        => value switch
        {
            null => false,
            string text => !string.IsNullOrWhiteSpace(text),
            _ => true
        };

    private static (string Alias, AlgoliaContentFieldTransform Transform) ParseConfiguredField(string raw)
    {
        var parts = raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return (string.Empty, AlgoliaContentFieldTransform.None);

        var transform = parts.Length < 2
            ? AlgoliaContentFieldTransform.None
            : parts[1].ToLowerInvariant() switch
            {
                "unix" => AlgoliaContentFieldTransform.UnixSeconds,
                "unixms" => AlgoliaContentFieldTransform.UnixMilliseconds,
                _ => AlgoliaContentFieldTransform.None
            };

        return (parts[0], transform);
    }

    private static object? TryToUnix(object? value, bool unixMilliseconds)
    {
        if (value is null)
            return null;

        DateTimeOffset dto;
        switch (value)
        {
            case DateTimeOffset dateTimeOffset:
                dto = dateTimeOffset;
                break;
            case DateTime dateTime:
                dto = dateTime.Kind switch
                {
                    DateTimeKind.Utc => new DateTimeOffset(dateTime, TimeSpan.Zero),
                    DateTimeKind.Local => new DateTimeOffset(dateTime),
                    _ => new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Local))
                };
                break;
            case string text when !string.IsNullOrWhiteSpace(text):
                if (!DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out dto) && !DateTimeOffset.TryParse(text, out dto))
                    return null;
                break;
            default:
                return null;
        }

        return unixMilliseconds ? dto.ToUnixTimeMilliseconds() : dto.ToUnixTimeSeconds();
    }
}
