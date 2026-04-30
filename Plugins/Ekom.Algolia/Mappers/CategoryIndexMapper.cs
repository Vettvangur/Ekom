using Ekom.Algolia.Models.Indexing;
using Ekom.Models;

namespace Ekom.Algolia.Mappers;

internal sealed class CategoryIndexMapper : IAlgoliaCategoryIndexMapper
{
    private readonly IReadOnlyList<IAlgoliaCategoryEnricher> _enrichers;

    public CategoryIndexMapper(IEnumerable<IAlgoliaCategoryEnricher>? enrichers = null)
    {
        _enrichers = (enrichers ?? Array.Empty<IAlgoliaCategoryEnricher>())
            .OrderBy(e => e.Order)
            .ToList();
    }

    public AlgoliaCategoryRecord? Map(ICategory category, AlgoliaResolvedStore store, string baseIndexName)
    {
        if (category == null)
            return null;

        var locale = store.Locale;
        var title = GetLocalizedValue(category, "title", category.Title, locale);
        if (string.IsNullOrWhiteSpace(title))
            return null;

        var urls = category.UrlsWithContext
            .Where(u => string.IsNullOrWhiteSpace(locale) || u.Culture.Equals(locale, StringComparison.OrdinalIgnoreCase))
            .Select(u => u.Url)
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(u => u!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (urls.Count == 0)
        {
            urls = category.Urls
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Select(u => u!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var pathSegments = category.Ancestors
            .Select(ancestor => GetLocalizedValue(ancestor, "title", ancestor.Title, locale))
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToList();

        pathSegments.Add(title);

        var record = new AlgoliaCategoryRecord
        {
            ObjectId = category.Key.ToString(),
            NodeName = NullIfWhiteSpace(GetNodeName(category)),
            Title = title,
            Slug = NullIfWhiteSpace(category.Slug),
            Url = NullIfWhiteSpace(urls.FirstOrDefault() ?? category.Url),
            StoreAlias = NullIfWhiteSpace(store.Alias),
            Locale = NullIfWhiteSpace(locale),
            ParentId = category.ParentId > 0 ? category.ParentId.ToString() : null,
            CreatedAt = ToUnixTimeSeconds(category.CreateDate),
            UpdatedAt = ToUnixTimeSeconds(category.UpdateDate),
            SortOrder = category.SortOrder,
            Data = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        };

        for (var i = 0; i < pathSegments.Count; i++)
            record.Data[$"hierarchical_categories.lvl{i}"] = string.Join(" > ", pathSegments.Take(i + 1));

        record.Data["category_path"] = string.Join(" > ", pathSegments);

        if (_enrichers.Count > 0)
        {
            var ctx = new AlgoliaCategoryEnrichmentContext(category, store, baseIndexName);
            foreach (var enricher in _enrichers)
                enricher.Enrich(record, ctx);
        }

        RemoveEmptyValues(record.Data);

        return record;
    }

    private static string GetLocalizedValue(INodeEntity node, string propertyAlias, string fallbackValue, string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
            return fallbackValue;

        var localized = node.GetValue(propertyAlias, locale, fallback: true);
        return string.IsNullOrWhiteSpace(localized) ? fallbackValue : localized;
    }

    private static string GetNodeName(INodeEntity node)
    {
        var nodeName = node.GetValue("nodeName");
        if (!string.IsNullOrWhiteSpace(nodeName))
            return nodeName;

        var properties = node.Properties;
        if (properties != null && properties.TryGetValue("nodeName", out nodeName) && !string.IsNullOrWhiteSpace(nodeName))
            return nodeName;

        return node.Title;
    }

    private static void RemoveEmptyValues(IDictionary<string, object?> values)
    {
        var keysToRemove = values
            .Where(kvp => IsEmptyValue(kvp.Value))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
            values.Remove(key);
    }

    private static bool IsEmptyValue(object? value)
    {
        return value switch
        {
            null => true,
            string s => string.IsNullOrWhiteSpace(s),
            System.Collections.IDictionary dictionary => dictionary.Count == 0,
            System.Collections.IEnumerable enumerable when value is not string => !enumerable.Cast<object?>().Any(),
            _ => false
        };
    }

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    private static long? ToUnixTimeSeconds(DateTime value)
        => value == default ? null : new DateTimeOffset(value).ToUnixTimeSeconds();
}
