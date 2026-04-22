using Ekom.Algolia.Models.Indexing;
using Ekom.Models;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace Ekom.Algolia.Mappers;

internal sealed class ProductIndexMapper : IAlgoliaProductIndexMapper
{
    private readonly AlgoliaOptions _options;
    private readonly IReadOnlyList<IAlgoliaProductEnricher> _enrichers;
    private readonly IReadOnlyList<IAlgoliaProductFieldConverter> _converters;

    public ProductIndexMapper(
        IOptions<AlgoliaOptions> options,
        IEnumerable<IAlgoliaProductEnricher>? enrichers = null,
        IEnumerable<IAlgoliaProductFieldConverter>? converters = null)
    {
        _options = options.Value;
        _enrichers = (enrichers ?? Array.Empty<IAlgoliaProductEnricher>())
            .OrderBy(e => e.Order)
            .ToList();
        _converters = (converters ?? Array.Empty<IAlgoliaProductFieldConverter>())
            .OrderBy(c => c.Order)
            .ToList();
    }

    public AlgoliaProductRecord? Map(IProduct product, AlgoliaResolvedStore store, string baseIndexName)
    {
        if (product == null)
            return null;

        var allowedProps = BuildAllowedProperties(product, store);
        var allowedTransforms = allowedProps.ToDictionary(x => x.Key, x => x.Value.Transform, StringComparer.OrdinalIgnoreCase);
        var price = ResolvePrice(product, store);
        var locale = store.Locale;
        var categoryLevels = BuildCategoryLevels(product, locale);
        var title = GetLocalizedValue(product, "title", product.Title, locale);

        var images = product.Images
            .Select(i => i?.Url)
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(u => ApplyDomain(u!, store.Domain))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var urls = product.UrlsWithContext
            .Where(u => string.IsNullOrWhiteSpace(locale) || u.Culture.Equals(locale, StringComparison.OrdinalIgnoreCase))
            .Select(u => u.Url)
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(u => ApplyDomain(u!, store.Domain))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            ?? [];

        if (urls.Count == 0)
        {
            urls = product.Urls
                ?.Where(u => !string.IsNullOrWhiteSpace(u))
                .Select(u => ApplyDomain(u!, store.Domain))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
                ?? [];
        }

        var record = new AlgoliaProductRecord
        {
            ObjectId = product.Key.ToString(),
            Sku = NullIfWhiteSpace(product.SKU),
            NodeName = NullIfWhiteSpace(product.Title),
            Title = title,
            Summary = NullIfWhiteSpace(GetLocalizedValue(product, "summary", product.Summary, locale)),
            Description = NullIfWhiteSpace(GetLocalizedValue(product, "description", product.Description, locale)),
            Url = NullIfWhiteSpace(urls.FirstOrDefault() ?? ApplyDomain(product.Url, store.Domain)),
            ImageUrl = NullIfWhiteSpace(images.FirstOrDefault()),
            ImageUrls = images.Count > 0 ? images : null,
            Price = price?.Value,
            PriceWithVat = price?.WithVat.Value,
            PriceWithoutVat = price?.WithoutVat.Value,
            Currency = NullIfWhiteSpace(price?.Currency.ISOCurrencySymbol ?? store.Currency),
            Available = product.Available,
            Stock = store.IncludeStock ? product.Stock : null,
            StoreAlias = NullIfWhiteSpace(store.Alias),
            Locale = NullIfWhiteSpace(store.Locale),
            CreatedAt = ToUnixTimeSeconds(product.CreateDate),
            UpdatedAt = ToUnixTimeSeconds(product.UpdateDate),
            Data = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        };

        foreach (var categoryLevel in categoryLevels)
            record.Data[categoryLevel.Key] = categoryLevel.Value;

        var categoryPaths = BuildCategoryPaths(product, locale);

        if (categoryPaths.Count > 0)
            record.Data["category_paths"] = categoryPaths;

        foreach (var kvp in allowedProps)
        {
            var alias = kvp.Key;
            var configuredField = kvp.Value;

            var raw = GetLocalizedValue(product, alias, product.GetValue(alias, store.Alias), locale);
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            object? converted = NormalizePropertyValue(raw, configuredField.ValueType);
            var ctx = new AlgoliaProductFieldContext(product, store, alias, baseIndexName);
            converted = ConvertProperty(ctx, converted);

            if (converted is null)
                continue;

            record.Data[alias] = converted;

            if (configuredField.Transform != AlgoliaFieldTransform.None)
            {
                var unixValue = configuredField.Transform switch
                {
                    AlgoliaFieldTransform.UnixSeconds => TryToUnix(converted, unixMilliseconds: false),
                    AlgoliaFieldTransform.UnixMilliseconds => TryToUnix(converted, unixMilliseconds: true),
                    _ => null
                };

                if (unixValue is not null)
                    record.Data[$"{alias}Unix"] = unixValue;
            }
        }

        if (_enrichers.Count > 0)
        {
            var ctx = new AlgoliaProductEnrichmentContext(product, store, baseIndexName, allowedTransforms);
            foreach (var enricher in _enrichers)
                enricher.Enrich(record, ctx);
        }

        RemoveEmptyValues(record.Data);

        return record;
    }

    private Dictionary<string, ConfiguredField> BuildAllowedProperties(IProduct product, AlgoliaResolvedStore store)
    {
        if (_options.Indexing.ProductProperties.Count == 0)
            return new Dictionary<string, ConfiguredField>(StringComparer.OrdinalIgnoreCase);

        return _options.Indexing.ProductProperties
            .Select(ConfiguredField.Parse)
            .Where(f => !string.IsNullOrWhiteSpace(f.Alias))
            .GroupBy(f => f.Alias, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);
    }

    private object? ConvertProperty(AlgoliaProductFieldContext ctx, object? value)
    {
        foreach (var converter in _converters)
        {
            if (converter.CanHandle(ctx))
                value = converter.Convert(ctx, value);
        }

        return value;
    }

    private static string GetLocalizedValue(INodeEntity node, string propertyAlias, string fallbackValue, string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
            return fallbackValue;

        var localized = node.GetValue(propertyAlias, locale, fallback: true);
        return string.IsNullOrWhiteSpace(localized) ? fallbackValue : localized;
    }

    private static string ApplyDomain(string? url, string? domain)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        if (string.IsNullOrWhiteSpace(domain))
            return url;

        if (Uri.TryCreate(url, UriKind.Absolute, out _))
            return url;

        if (!Uri.TryCreate(domain, UriKind.Absolute, out var baseUri))
            return url;

        if (!Uri.TryCreate(baseUri, url, out var absoluteUri))
            return url;

        return absoluteUri.ToString();
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildCategoryLevels(IProduct product, string? locale)
    {
        var categoryLevels = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var category in product.Categories)
        {
            var segments = category.Ancestors
                .Select(ancestor => GetLocalizedValue(ancestor, "title", ancestor.Title, locale))
                .Where(title => !string.IsNullOrWhiteSpace(title))
                .ToList();

            var categoryTitle = GetLocalizedValue(category, "title", category.Title, locale);
            if (!string.IsNullOrWhiteSpace(categoryTitle))
                segments.Add(categoryTitle);

            for (var i = 0; i < segments.Count; i++)
            {
                var key = $"hierarchical_categories.lvl{i}";
                if (!categoryLevels.TryGetValue(key, out var values))
                {
                    values = [];
                    categoryLevels[key] = values;
                }

                values.Add(string.Join(" > ", segments.Take(i + 1)));
            }
        }

        return categoryLevels.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<string>)kvp.Value
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> BuildCategoryPaths(IProduct product, string? locale)
    {
        var categoryPaths = new List<string>();

        foreach (var category in product.Categories)
        {
            var segments = category.Ancestors
                .Select(ancestor => GetLocalizedValue(ancestor, "title", ancestor.Title, locale))
                .Where(title => !string.IsNullOrWhiteSpace(title))
                .ToList();

            var categoryTitle = GetLocalizedValue(category, "title", category.Title, locale);
            if (!string.IsNullOrWhiteSpace(categoryTitle))
                segments.Add(categoryTitle);

            for (var i = 0; i < segments.Count; i++)
            {
                var path = string.Join(" > ", segments.Take(i + 1));
                if (!string.IsNullOrWhiteSpace(path) && !categoryPaths.Contains(path, StringComparer.OrdinalIgnoreCase))
                    categoryPaths.Add(path);
            }
        }

        return categoryPaths;
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

    private static object? NormalizePropertyValue(string value, AlgoliaFieldValueType valueType)
    {
        if (valueType == AlgoliaFieldValueType.Int)
            return TryParseInt(value);

        if (valueType == AlgoliaFieldValueType.Decimal)
            return TryParseDecimal(value);

        if (valueType == AlgoliaFieldValueType.Array)
            return TryParseStringArray(value);

        if (bool.TryParse(value, out var booleanValue))
            return booleanValue;

        return value switch
        {
            "0" => false,
            "1" => true,
            _ => value
        };
    }

    private static int? TryParseInt(string value)
    {
        var trimmedValue = value.Trim();
        if (int.TryParse(trimmedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
            return intValue;

        return int.TryParse(trimmedValue, NumberStyles.Integer, CultureInfo.CurrentCulture, out intValue)
            ? intValue
            : null;
    }

    private static decimal? TryParseDecimal(string value)
    {
        var trimmedValue = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmedValue))
            return null;

        var normalizedValue = trimmedValue.Replace(',', '.');
        return decimal.TryParse(normalizedValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue)
            ? decimalValue
            : null;
    }

    private static IReadOnlyList<string>? TryParseStringArray(string value)
    {
        var trimmedValue = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmedValue))
            return null;

        try
        {
            using var document = JsonDocument.Parse(trimmedValue);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return null;

            var results = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                    continue;

                var stringValue = item.GetString()?.Trim();
                if (string.IsNullOrWhiteSpace(stringValue) || !seen.Add(stringValue))
                    continue;

                results.Add(stringValue);
            }

            return results.Count > 0 ? results : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IPrice? ResolvePrice(IProduct product, AlgoliaResolvedStore store)
    {
        if (string.IsNullOrWhiteSpace(store.Currency))
            return product.Price;

        return product.Prices.FirstOrDefault(x => x.Currency.CurrencyValue.Equals(store.Currency, StringComparison.OrdinalIgnoreCase))
            ?? product.Price;
    }

    private static object? TryToUnix(object? value, bool unixMilliseconds)
    {
        if (value is null)
            return null;

        DateTimeOffset dto;

        switch (value)
        {
            case DateTimeOffset d:
                dto = d;
                break;
            case DateTime dt:
                dto = dt.Kind switch
                {
                    DateTimeKind.Utc => new DateTimeOffset(dt, TimeSpan.Zero),
                    DateTimeKind.Local => new DateTimeOffset(dt),
                    _ => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Local))
                };
                break;
            case string s when !string.IsNullOrWhiteSpace(s):
                if (!DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out dto) &&
                    !DateTimeOffset.TryParse(s, out dto))
                {
                    return null;
                }
                break;
            default:
                return null;
        }

        return unixMilliseconds ? dto.ToUnixTimeMilliseconds() : dto.ToUnixTimeSeconds();
    }

    private static long? ToUnixTimeSeconds(DateTime? value)
    {
        if (!value.HasValue)
            return null;

        var dto = value.Value.Kind switch
        {
            DateTimeKind.Utc => new DateTimeOffset(value.Value, TimeSpan.Zero),
            DateTimeKind.Local => new DateTimeOffset(value.Value),
            _ => new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc), TimeSpan.Zero)
        };

        return dto.ToUnixTimeSeconds();
    }

    internal readonly record struct ConfiguredField(string Alias, AlgoliaFieldValueType ValueType, AlgoliaFieldTransform Transform)
    {
        public static ConfiguredField Parse(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return new ConfiguredField(string.Empty, AlgoliaFieldValueType.None, AlgoliaFieldTransform.None);

            var parts = raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var alias = parts[0];

            if (parts.Length < 2)
                return new ConfiguredField(alias, AlgoliaFieldValueType.None, AlgoliaFieldTransform.None);

            return parts[1].ToLowerInvariant() switch
            {
                "int" => new ConfiguredField(alias, AlgoliaFieldValueType.Int, AlgoliaFieldTransform.None),
                "decimal" => new ConfiguredField(alias, AlgoliaFieldValueType.Decimal, AlgoliaFieldTransform.None),
                "array" => new ConfiguredField(alias, AlgoliaFieldValueType.Array, AlgoliaFieldTransform.None),
                "unix" => new ConfiguredField(alias, AlgoliaFieldValueType.None, AlgoliaFieldTransform.UnixSeconds),
                "unixms" => new ConfiguredField(alias, AlgoliaFieldValueType.None, AlgoliaFieldTransform.UnixMilliseconds),
                _ => new ConfiguredField(alias, AlgoliaFieldValueType.None, AlgoliaFieldTransform.None)
            };
        }
    }
}

internal enum AlgoliaFieldValueType
{
    None,
    Int,
    Decimal,
    Array
}
