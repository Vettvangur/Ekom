using Ekom.Algolia.Models.Indexing;
using Ekom.Models;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Linq;

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
        var price = ResolvePrice(product, store);
        var locale = store.Locale;
        var categoryPageIdentifiers = BuildCategoryPageIdentifiers(product, locale);

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
            Sku = product.SKU,
            Name = GetLocalizedValue(product, "title", product.Title, locale),
            Summary = GetLocalizedValue(product, "summary", product.Summary, locale),
            Description = GetLocalizedValue(product, "description", product.Description, locale),
            Url = urls.FirstOrDefault() ?? ApplyDomain(product.Url, store.Domain),
            ImageUrls = images,
            Price = price?.Value,
            PriceWithVat = price?.WithVat.Value,
            PriceWithoutVat = price?.WithoutVat.Value,
            Currency = price?.Currency.CurrencyValue ?? store.Currency,
            Available = product.Available,
            Stock = store.IncludeStock ? product.Stock : null,
            StoreAlias = store.Alias,
            Locale = store.Locale,
            CategoryPageIdentifier = categoryPageIdentifiers,
            CreatedAt = ToUnixTimeSeconds(product.CreateDate),
            UpdatedAt = ToUnixTimeSeconds(product.UpdateDate),
            Data = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        };

        foreach (var kvp in allowedProps)
        {
            var alias = kvp.Key;
            var transform = kvp.Value;

            var raw = GetLocalizedValue(product, alias, product.GetValue(alias, store.Alias), locale);
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

    private Dictionary<string, AlgoliaFieldTransform> BuildAllowedProperties(IProduct product, AlgoliaResolvedStore store)
    {
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

    private static IReadOnlyList<string> BuildCategoryPageIdentifiers(IProduct product, string? locale)
    {
        var identifiers = new List<string>();

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
                identifiers.Add(string.Join(" > ", segments.Take(i + 1)));
        }

        return identifiers
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
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
