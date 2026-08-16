using Ekom.Umb.SkuProductPicker.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Ekom.Umb.SkuProductPicker.Services;

internal sealed class SkuProductPickerService : ISkuProductPickerService
{
    private const string ProductContentTypeAlias = "ekmProduct";
    private const string SkuPropertyAlias = "sku";

    private readonly IContentService _contentService;
    private readonly IScopeProvider _scopeProvider;

    public SkuProductPickerService(IContentService contentService, IScopeProvider scopeProvider)
    {
        _contentService = contentService;
        _scopeProvider = scopeProvider;
    }

    public IReadOnlyList<SkuProductPickerItem> ResolveKeys(IReadOnlyList<Guid> keys)
    {
        var productSkus = new List<SkuProductPickerItem>();

        foreach (var key in keys.Distinct())
        {
            var content = _contentService.GetById(key);
            if (content == null
                || content.Trashed
                || !content.ContentType.Alias.Equals(ProductContentTypeAlias, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var sku = content.GetValue<string>(SkuPropertyAlias)?.Trim();
            if (string.IsNullOrWhiteSpace(sku))
            {
                continue;
            }

            productSkus.Add(new SkuProductPickerItem { Key = key, Sku = sku });
        }

        var productsBySku = GetProductsBySku(productSkus.Select(x => x.Sku));
        return productSkus
            .Where(item => productsBySku.TryGetValue(item.Sku, out var matches)
                && matches.Count == 1
                && matches[0].Key == item.Key)
            .ToList();
    }

    public IReadOnlyList<SkuProductPickerItem> ResolveSkus(IReadOnlyList<string> skus)
    {
        var productsBySku = GetProductsBySku(skus);
        var results = new List<SkuProductPickerItem>();

        foreach (var sku in skus)
        {
            var normalizedSku = sku?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedSku)
                || !productsBySku.TryGetValue(normalizedSku, out var matches)
                || matches.Count != 1)
            {
                continue;
            }

            results.Add(matches[0]);
        }

        return results;
    }

    private IReadOnlyDictionary<string, List<SkuProductPickerItem>> GetProductsBySku(IEnumerable<string> skus)
    {
        var requestedSkus = skus
            .Select(sku => sku?.Trim())
            .Where(sku => !string.IsNullOrWhiteSpace(sku))
            .Distinct(StringComparer.Ordinal)
            .Cast<string>()
            .ToList();

        if (requestedSkus.Count == 0)
        {
            return new Dictionary<string, List<SkuProductPickerItem>>(StringComparer.Ordinal);
        }

        using var scope = _scopeProvider.CreateScope(autoComplete: true);
        var rows = scope.Database.Fetch<SkuProductPickerRow>(
            @"SELECT n.uniqueId AS [Key], pd.varcharValue AS Sku
FROM umbracoNode n
INNER JOIN umbracoContent c ON c.nodeId = n.id
INNER JOIN cmsContentType ct ON ct.nodeId = c.contentTypeId
INNER JOIN umbracoContentVersion cv ON cv.nodeId = n.id AND cv.[current] = 1
INNER JOIN umbracoPropertyData pd ON pd.versionId = cv.id
INNER JOIN cmsPropertyType pt ON pt.id = pd.propertyTypeId
WHERE n.trashed = 0
  AND ct.alias = @0
  AND pt.alias = @1
  AND pd.varcharValue IN (@2)",
            ProductContentTypeAlias,
            SkuPropertyAlias,
            requestedSkus);

        return rows
            .Where(row => !string.IsNullOrWhiteSpace(row.Sku))
            .GroupBy(row => row.Sku, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .GroupBy(row => row.Key)
                    .Select(keyGroup => keyGroup.First())
                    .Select(row => new SkuProductPickerItem
                    {
                        Key = row.Key,
                        Sku = row.Sku,
                    })
                    .ToList(),
                StringComparer.Ordinal);
    }

    private sealed class SkuProductPickerRow
    {
        public Guid Key { get; init; }
        public string Sku { get; init; } = string.Empty;
    }
}
