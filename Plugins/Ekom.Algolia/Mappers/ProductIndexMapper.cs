using Ekom.Algolia.Models.Indexing;
using Ekom.Models;
using Microsoft.Extensions.Options;
using System.Globalization;

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

    public AlgoliaProductRecord? Map(IProduct product, AlgoliaStoreOptions store, string baseIndexName)
    {
        if (product == null)
            return null;

        var allowedProps = BuildAllowedProperties(product, store);

        var images = product.Images
            .Select(i => i?.Url)
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var urls = product.Urls
            ?.Where(u => !string.IsNullOrWhiteSpace(u))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            ?? [];

        var record = new AlgoliaProductRecord
        {
            ObjectId = product.Key.ToString(),
            Sku = product.SKU,
            Name = product.Title,
            Summary = product.Summary,
            Description = product.Description,
            Url = product.Url,
            Urls = urls,
            ImageUrls = images,
            Price = product.Price.Value,
            OriginalPrice = product.OriginalPrice.OriginalValue,
            Currency = product.Price.Currency.ISOCurrencySymbol,
            Available = product.Available,
            Stock = product.Stock,
            Backorder = product.Backorder,
            StoreAlias = store.Alias,
            Locale = store.Locale,
            CategoryNames = product.Categories.Select(c => c.Title).ToList(),
            CategoryKeys = product.Categories.Select(c => c.Key.ToString()).ToList(),
            CategoryAncestors = product.CategoryAncestors.Select(c => c.Title).ToList(),
            CreatedAt = product.CreateDate,
            UpdatedAt = product.UpdateDate,
            Data = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        };

        foreach (var kvp in allowedProps)
        {
            var alias = kvp.Key;
            var transform = kvp.Value;

            var raw = product.GetValue(alias, store.Alias);
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            object? converted = raw;
            var ctx = new AlgoliaProductFieldContext(product, store, alias, baseIndexName);
            converted = ConvertProperty(ctx, converted);

            if (converted is null)
                continue;

            record.Data[alias] = converted;

            if (transform != AlgoliaFieldTransform.None)
            {
                var unixValue = transform switch
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
            var ctx = new AlgoliaProductEnrichmentContext(product, store, baseIndexName, allowedProps);
            foreach (var enricher in _enrichers)
                enricher.Enrich(record, ctx);
        }

        return record;
    }

    private Dictionary<string, AlgoliaFieldTransform> BuildAllowedProperties(IProduct product, AlgoliaStoreOptions store)
    {
        if (_options.Indexing.IncludeAllProperties)
        {
            return product.Properties
                .Keys
                .Where(k => !string.IsNullOrWhiteSpace(k) && !k.StartsWith("__", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToDictionary(k => k, _ => AlgoliaFieldTransform.None, StringComparer.OrdinalIgnoreCase);
        }

        if (_options.Indexing.ProductProperties.Count == 0)
            return new Dictionary<string, AlgoliaFieldTransform>(StringComparer.OrdinalIgnoreCase);

        return _options.Indexing.ProductProperties
            .Select(ConfiguredField.Parse)
            .Where(f => !string.IsNullOrWhiteSpace(f.Alias))
            .GroupBy(f => f.Alias, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last().Transform, StringComparer.OrdinalIgnoreCase);
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

    internal readonly record struct ConfiguredField(string Alias, AlgoliaFieldTransform Transform)
    {
        public static ConfiguredField Parse(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return new ConfiguredField(string.Empty, AlgoliaFieldTransform.None);

            var parts = raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var alias = parts[0];

            if (parts.Length < 2)
                return new ConfiguredField(alias, AlgoliaFieldTransform.None);

            return parts[1].ToLowerInvariant() switch
            {
                "unix" => new ConfiguredField(alias, AlgoliaFieldTransform.UnixSeconds),
                "unixms" => new ConfiguredField(alias, AlgoliaFieldTransform.UnixMilliseconds),
                _ => new ConfiguredField(alias, AlgoliaFieldTransform.None)
            };
        }
    }
}
