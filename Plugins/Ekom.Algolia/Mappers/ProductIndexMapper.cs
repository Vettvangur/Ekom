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
        var nodeName = GetNodeName(product);
        var title = ApplyBuiltInTextTransform(
            "title",
            GetLocalizedValue(product, "title", product.Title, locale),
            allowedProps,
            product,
            store,
            baseIndexName);
        var summary = ApplyBuiltInTextTransform(
            "summary",
            GetLocalizedValue(product, "summary", product.Summary, locale),
            allowedProps,
            product,
            store,
            baseIndexName);
        var description = ApplyBuiltInTextTransform(
            "description",
            GetLocalizedValue(product, "description", product.Description, locale),
            allowedProps,
            product,
            store,
            baseIndexName);

        var images = product.Images
            .Select(i => i?.Url)
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(u => u!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var urls = product.UrlsWithContext
            .Where(u => string.IsNullOrWhiteSpace(locale) || u.Culture.Equals(locale, StringComparison.OrdinalIgnoreCase))
            .Select(u => u.Url)
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(u => u!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            ?? [];

        if (urls.Count == 0)
        {
            urls = product.Urls
                ?.Where(u => !string.IsNullOrWhiteSpace(u))
                .Select(u => u!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
                ?? [];
        }

        var record = new AlgoliaProductRecord
        {
            ObjectId = product.Key.ToString(),
            Sku = NullIfWhiteSpace(product.SKU),
            ProductId = product.Key.ToString(),
            NodeName = NullIfWhiteSpace(nodeName),
            Title = title,
            Summary = NullIfWhiteSpace(summary),
            Description = NullIfWhiteSpace(description),
            Url = NullIfWhiteSpace(urls.FirstOrDefault() ?? product.Url),
            ImageUrl = NullIfWhiteSpace(images.FirstOrDefault()),
            ImageUrls = images.Count > 0 ? images : null,
            Price = price?.Value,
            PriceWithVat = price?.WithVat.Value,
            PriceWithoutVat = price?.WithoutVat.Value,
            Currency = NullIfWhiteSpace(price?.Currency.ISOCurrencySymbol ?? store.Currency),
            Available = product.Available ? 1 : 0,
            ProductRanking = ResolveRank(product, store.Alias),
            CategoryRanking = ResolveCategoryRank(product, store.Alias),
            CategoryPageId = BuildCategoryPageIds(product),
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

        foreach (var kvp in allowedProps.Where(x => !IsBuiltInTextStripHtmlField(x.Value)))
        {
            var alias = kvp.Key;
            var configuredField = kvp.Value;

            object? converted = ResolveConfiguredValue(product, store, configuredField);
            var ctx = new AlgoliaProductFieldContext(product, store, alias, baseIndexName);
            converted = ConvertProperty(ctx, converted);
            converted = ApplyTransform(converted, configuredField.Transform);

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

    public IReadOnlyList<AlgoliaProductRecord> MapRecords(IProduct product, AlgoliaResolvedStore store, string baseIndexName)
    {
        var productRecord = Map(product, store, baseIndexName);
        if (productRecord is null)
            return [];

        if (!_options.Indexing.Variants)
            return [productRecord];

        var records = new List<AlgoliaProductRecord> { productRecord };
        var configuredFields = BuildAllowedProperties(product, store);

        foreach (var variant in product.AllVariants)
        {
            var record = MapVariant(product, variant, productRecord, store, baseIndexName, configuredFields);
            if (record is not null)
                records.Add(record);
        }

        return records;
    }

    private AlgoliaProductRecord? MapVariant(
        IProduct product,
        IVariant variant,
        AlgoliaProductRecord productRecord,
        AlgoliaResolvedStore store,
        string baseIndexName,
        IReadOnlyDictionary<string, ConfiguredField> configuredFields)
    {
        if (string.IsNullOrWhiteSpace(variant.SKU))
            return null;

        var images = variant.Images
            .Select(i => i?.Url)
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(u => u!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var price = ResolvePrice(variant, store);
        var variantDescription = ResolveVariantDescription(product, variant, store, baseIndexName, configuredFields);
        var data = new Dictionary<string, object?>(productRecord.Data, StringComparer.OrdinalIgnoreCase)
        {
            ["variantSku"] = variant.SKU,
            ["variantTitle"] = NullIfWhiteSpace(variant.Title),
            ["variantDescription"] = variantDescription,
            ["variantAvailable"] = variant.Available ? 1 : 0,
            ["variantStock"] = store.IncludeStock ? variant.Stock : null
        };

        RemoveEmptyValues(data);

        return new AlgoliaProductRecord
        {
            ObjectId = $"{product.Key}_{variant.Key}",
            Sku = NullIfWhiteSpace(variant.SKU),
            ProductId = product.Key.ToString(),
            VariantId = variant.Key.ToString(),
            ParentSku = NullIfWhiteSpace(product.SKU),
            IsVariant = true,
            VariantGroupId = variant.VariantGroupId,
            NodeName = productRecord.NodeName,
            Title = productRecord.Title,
            Summary = productRecord.Summary,
            Description = variantDescription ?? productRecord.Description,
            Url = productRecord.Url,
            ImageUrl = NullIfWhiteSpace(images.FirstOrDefault()) ?? productRecord.ImageUrl,
            ImageUrls = images.Count > 0 ? images : productRecord.ImageUrls,
            Price = price?.Value ?? productRecord.Price,
            PriceWithVat = price?.WithVat.Value ?? productRecord.PriceWithVat,
            PriceWithoutVat = price?.WithoutVat.Value ?? productRecord.PriceWithoutVat,
            Currency = NullIfWhiteSpace(price?.Currency.ISOCurrencySymbol ?? store.Currency) ?? productRecord.Currency,
            Available = variant.Available ? 1 : 0,
            ProductRanking = productRecord.ProductRanking,
            CategoryRanking = productRecord.CategoryRanking,
            CategoryPageId = productRecord.CategoryPageId,
            Stock = store.IncludeStock ? variant.Stock : productRecord.Stock,
            StoreAlias = productRecord.StoreAlias,
            Locale = productRecord.Locale,
            CreatedAt = ToUnixTimeSeconds(variant.CreateDate),
            UpdatedAt = ToUnixTimeSeconds(variant.UpdateDate),
            Data = data
        };
    }

    private string? ResolveVariantDescription(
        IProduct product,
        IVariant variant,
        AlgoliaResolvedStore store,
        string baseIndexName,
        IReadOnlyDictionary<string, ConfiguredField> configuredFields)
    {
        if (!configuredFields.TryGetValue("description", out var field) || !IsBuiltInTextStripHtmlField(field))
            return NullIfWhiteSpace(variant.Description);

        var context = new AlgoliaProductFieldContext(product, store, "description", baseIndexName);
        var converted = ConvertProperty(context, variant.Description);
        converted = ApplyTransform(converted, field.Transform);

        return NullIfWhiteSpace(converted?.ToString());
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

    private static object? ApplyTransform(object? value, AlgoliaFieldTransform transform)
        => transform == AlgoliaFieldTransform.StripHtml
            ? AlgoliaHtmlTextConverter.Convert(value)
            : value;

    private string ApplyBuiltInTextTransform(
        string alias,
        string value,
        IReadOnlyDictionary<string, ConfiguredField> configuredFields,
        IProduct product,
        AlgoliaResolvedStore store,
        string baseIndexName)
    {
        if (!configuredFields.TryGetValue(alias, out var field) || !IsBuiltInTextStripHtmlField(field))
            return value;

        var context = new AlgoliaProductFieldContext(product, store, alias, baseIndexName);
        var converted = ConvertProperty(context, value);
        converted = ApplyTransform(converted, field.Transform);

        return converted?.ToString() ?? string.Empty;
    }

    private static bool IsBuiltInTextStripHtmlField(ConfiguredField field)
        => field.Source == AlgoliaFieldSource.Property
            && field.Transform == AlgoliaFieldTransform.StripHtml
            && (field.Alias.Equals("title", StringComparison.OrdinalIgnoreCase)
                || field.Alias.Equals("summary", StringComparison.OrdinalIgnoreCase)
                || field.Alias.Equals("description", StringComparison.OrdinalIgnoreCase));

    private static object? ResolveConfiguredValue(IProduct product, AlgoliaResolvedStore store, ConfiguredField configuredField)
    {
        return configuredField.Source switch
        {
            AlgoliaFieldSource.Metafield => ResolveMetafieldValue(product, configuredField.Alias, store.Locale, configuredField.ValueType),
            _ => ResolveProductPropertyValue(product, store, configuredField)
        };
    }

    private static object? ResolveProductPropertyValue(IProduct product, AlgoliaResolvedStore store, ConfiguredField configuredField)
    {
        var raw = GetLocalizedValue(product, configuredField.Alias, product.GetValue(configuredField.Alias, store.Alias), store.Locale);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        return NormalizePropertyValue(raw, configuredField.ValueType);
    }

    private static int ResolveCategoryRank(IProduct product, string? storeAlias)
    {
        var rank = product.Categories
            .Select(category => TryResolveRank(category, storeAlias))
            .Where(value => value.HasValue)
            .Max();

        return rank ?? 0;
    }

    private static int ResolveRank(INodeEntity node, string? storeAlias)
        => TryResolveRank(node, storeAlias) ?? 0;

    private static int? TryResolveRank(INodeEntity node, string? storeAlias)
    {
        var raw = node.GetValue("ekmAlgoliaRank", storeAlias);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        return int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var rank)
            ? rank
            : null;
    }

    private static object? ResolveMetafieldValue(IProduct product, string alias, string? locale, AlgoliaFieldValueType valueType)
    {
        var metafield = product.Metafields
            .FirstOrDefault(x => x.Field.Alias.Equals(alias, StringComparison.OrdinalIgnoreCase));

        if (metafield is null || metafield.Values.Count == 0)
            return null;

        var values = metafield.Values
            .Select(value => ResolveMetafieldText(value, locale))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (values.Count == 0)
            return null;

        if (valueType == AlgoliaFieldValueType.Array || metafield.Field.EnableMultipleChoice)
            return values;

        if (values.Count != 1)
            return null;

        return NormalizePropertyValue(values[0], valueType);
    }

    private static string? ResolveMetafieldText(IReadOnlyDictionary<string, string> values, string? locale)
    {
        if (!string.IsNullOrWhiteSpace(locale) &&
            values.TryGetValue(locale, out var localizedValue) &&
            !string.IsNullOrWhiteSpace(localizedValue))
        {
            return localizedValue;
        }

        if (values.TryGetValue(string.Empty, out var rawValue) && !string.IsNullOrWhiteSpace(rawValue))
            return rawValue;

        return values.Values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
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

    private static IReadOnlyList<string>? BuildCategoryPageIds(IProduct product)
    {
        var categoryIds = new List<string>();
        var seen = new HashSet<Guid>();

        foreach (var category in product.Categories)
        {
            foreach (var categoryKey in category.Ancestors
                .Select(ancestor => ancestor.Key)
                .Append(category.Key))
            {
                if (categoryKey != Guid.Empty && seen.Add(categoryKey))
                    categoryIds.Add(categoryKey.ToString("D"));
            }
        }

        return categoryIds.Count > 0 ? categoryIds : null;
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

    private static IPrice? ResolvePrice(IVariant variant, AlgoliaResolvedStore store)
    {
        if (string.IsNullOrWhiteSpace(store.Currency))
            return variant.Price;

        return variant.Prices.FirstOrDefault(x => x.Currency.CurrencyValue.Equals(store.Currency, StringComparison.OrdinalIgnoreCase))
            ?? variant.Price;
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

    internal readonly record struct ConfiguredField(string Alias, AlgoliaFieldSource Source, AlgoliaFieldValueType ValueType, AlgoliaFieldTransform Transform)
    {
        public static ConfiguredField Parse(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return new ConfiguredField(string.Empty, AlgoliaFieldSource.Property, AlgoliaFieldValueType.None, AlgoliaFieldTransform.None);

            var parts = raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var (alias, source) = ParseSource(parts[0]);

            if (parts.Length < 2)
                return new ConfiguredField(alias, source, AlgoliaFieldValueType.None, AlgoliaFieldTransform.None);

            return parts[1].ToLowerInvariant() switch
            {
                "int" => new ConfiguredField(alias, source, AlgoliaFieldValueType.Int, AlgoliaFieldTransform.None),
                "decimal" => new ConfiguredField(alias, source, AlgoliaFieldValueType.Decimal, AlgoliaFieldTransform.None),
                "array" => new ConfiguredField(alias, source, AlgoliaFieldValueType.Array, AlgoliaFieldTransform.None),
                "unix" => new ConfiguredField(alias, source, AlgoliaFieldValueType.None, AlgoliaFieldTransform.UnixSeconds),
                "unixms" => new ConfiguredField(alias, source, AlgoliaFieldValueType.None, AlgoliaFieldTransform.UnixMilliseconds),
                "striphtml" => new ConfiguredField(alias, source, AlgoliaFieldValueType.None, AlgoliaFieldTransform.StripHtml),
                _ => new ConfiguredField(alias, source, AlgoliaFieldValueType.None, AlgoliaFieldTransform.None)
            };
        }

        private static (string Alias, AlgoliaFieldSource Source) ParseSource(string rawAlias)
        {
            const string metafieldPrefix = "metafield:";

            if (rawAlias.StartsWith(metafieldPrefix, StringComparison.OrdinalIgnoreCase))
                return (rawAlias[metafieldPrefix.Length..], AlgoliaFieldSource.Metafield);

            return (rawAlias, AlgoliaFieldSource.Property);
        }
    }
}

internal enum AlgoliaFieldSource
{
    Property,
    Metafield
}

internal enum AlgoliaFieldValueType
{
    None,
    Int,
    Decimal,
    Array
}
