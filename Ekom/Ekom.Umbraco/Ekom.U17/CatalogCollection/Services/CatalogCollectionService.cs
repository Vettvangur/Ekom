using Ekom.Models;
using Ekom.Models.Umbraco;
using Ekom.Umb.CatalogCollection.Models;
using Newtonsoft.Json;
using System.Globalization;
using System.Net;
using System.Text.Json;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Persistence.Querying;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Extensions;

namespace Ekom.Umb.CatalogCollection.Services;

internal sealed class CatalogCollectionService : ICatalogCollectionService
{
    private const int MaxPageSize = 200;
    private static readonly HashSet<string> CatalogAliases = new(StringComparer.OrdinalIgnoreCase) { "ekmCategory", "ekmProduct" };

    private readonly IContentService _contentService;
    private readonly IContentTypeService _contentTypeService;
    private readonly IScopeProvider _scopeProvider;

    public CatalogCollectionService(
        IContentService contentService,
        IContentTypeService contentTypeService,
        IScopeProvider scopeProvider)
    {
        _contentService = contentService;
        _contentTypeService = contentTypeService;
        _scopeProvider = scopeProvider;
    }

    public CatalogCollectionResponse GetCollection(string nodeId, CatalogCollectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var current = GetContent(nodeId);
        if (current.Trashed)
        {
            throw new Exceptions.HttpResponseException(HttpStatusCode.NotFound);
        }

        if (!CatalogAliases.Contains(current.ContentType.Alias))
        {
            throw new Exceptions.HttpResponseException(HttpStatusCode.BadRequest);
        }

        var pageSize = Math.Clamp(request.PageSize <= 0 ? 80 : request.PageSize, 1, MaxPageSize);
        var page = Math.Max(request.Page, 1);
        var subcategoryContent = GetCatalogChildren(current.Id, "ekmCategory")
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToList();
        var subcategoryCounts = GetChildCounts(subcategoryContent.Select(x => x.Id).ToList());
        var subcategories = subcategoryContent
            .Select(category => MapNode(category, subcategoryCounts.GetValueOrDefault(category.Id)))
            .ToList();
        var hasQuery = !string.IsNullOrWhiteSpace(request.Query);
        var productCount = 0;
        var filteredProductCount = 0;
        var totalPages = 1;
        IReadOnlyList<CatalogCollectionProduct> pagedProducts;

        if (hasQuery)
        {
            var products = GetCatalogChildren(current.Id, "ekmProduct")
                .Select(MapProduct)
                .ToList();
            var filteredProducts = SortProducts(FilterProducts(products, request.Query), request.Sort);

            productCount = products.Count;
            filteredProductCount = filteredProducts.Count;
            totalPages = Math.Max(1, (int)Math.Ceiling(filteredProductCount / (double)pageSize));
            page = Math.Min(page, totalPages);
            pagedProducts = filteredProducts
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
        }
        else
        {
            var productContent = GetPagedProducts(current.Id, page - 1, pageSize, request.Sort, out var totalProducts);
            productCount = (int)Math.Min(totalProducts, int.MaxValue);
            filteredProductCount = productCount;
            totalPages = Math.Max(1, (int)Math.Ceiling(productCount / (double)pageSize));
            page = Math.Min(page, totalPages);

            if (page - 1 != request.Page - 1)
            {
                productContent = GetPagedProducts(current.Id, page - 1, pageSize, request.Sort, out _);
            }

            pagedProducts = productContent.Select(MapProduct).ToList();
        }

        return new CatalogCollectionResponse
        {
            Current = MapNode(current),
            Parent = GetParent(current),
            Breadcrumbs = GetBreadcrumbs(current),
            Subcategories = subcategories,
            Products = pagedProducts,
            ProductCount = productCount,
            SubcategoryCount = subcategories.Count,
            FilteredProductCount = filteredProductCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages,
        };
    }

    private IContent GetContent(string id)
    {
        IContent? content = null;

        if (int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intId))
        {
            content = _contentService.GetById(intId);
        }
        else if (Guid.TryParse(id, out var key))
        {
            content = _contentService.GetById(key);
        }

        return content ?? throw new Exceptions.HttpResponseException(HttpStatusCode.NotFound);
    }

    private IReadOnlyList<IContent> GetCatalogChildren(int parentId, string contentTypeAlias)
    {
        var contentType = _contentTypeService.Get(contentTypeAlias)
            ?? throw new InvalidOperationException($"Content type {contentTypeAlias} not found.");
        var filter = new Query<IContent>(_scopeProvider.SqlContext)
            .Where(x => !x.Trashed && x.ContentTypeId == contentType.Id);

        return _contentService.GetPagedChildren(
                parentId,
                0,
                int.MaxValue,
                out _,
                filter,
                Ordering.ByDefault())
            .ToList();
    }

    private IReadOnlyList<IContent> GetPagedProducts(
        int parentId,
        int pageIndex,
        int pageSize,
        string? sort,
        out long totalRecords)
    {
        var productType = _contentTypeService.Get("ekmProduct")
            ?? throw new InvalidOperationException("Content type ekmProduct not found.");
        var filter = new Query<IContent>(_scopeProvider.SqlContext)
            .Where(x => !x.Trashed && x.ContentTypeId == productType.Id);

        return _contentService.GetPagedChildren(
                parentId,
                pageIndex,
                pageSize,
                out totalRecords,
                filter,
                GetProductOrdering(sort))
            .ToList();
    }

    private static Ordering GetProductOrdering(string? sort)
    {
        return (sort ?? string.Empty).ToUpperInvariant() switch
        {
            "SORTORDERDESC" => Ordering.By("sortOrder", Direction.Descending),
            "NAMEASC" => Ordering.By("name", Direction.Ascending),
            "NAMEDESC" => Ordering.By("name", Direction.Descending),
            "CREATEDASC" => Ordering.By("createDate", Direction.Ascending),
            "CREATEDDESC" => Ordering.By("createDate", Direction.Descending),
            "UPDATEDASC" => Ordering.By("updateDate", Direction.Ascending),
            "UPDATEDDESC" => Ordering.By("updateDate", Direction.Descending),
            _ => Ordering.By("sortOrder", Direction.Ascending),
        };
    }

    private CatalogCollectionNode? GetParent(IContent content)
    {
        if (content.ParentId <= 0)
        {
            return null;
        }

        var parent = _contentService.GetById(content.ParentId);
        return parent == null || !CatalogAliases.Contains(parent.ContentType.Alias) ? null : MapNode(parent);
    }

    private IReadOnlyList<CatalogCollectionNode> GetBreadcrumbs(IContent content)
    {
        var breadcrumbs = new List<CatalogCollectionNode>();
        var current = content;

        while (current.ParentId > 0)
        {
            var parent = _contentService.GetById(current.ParentId);
            if (parent == null)
            {
                break;
            }

            if (CatalogAliases.Contains(parent.ContentType.Alias))
            {
                breadcrumbs.Add(MapNode(parent));
            }

            current = parent;
        }

        breadcrumbs.Reverse();
        breadcrumbs.Add(MapNode(content));
        return breadcrumbs;
    }

    private static List<CatalogCollectionProduct> FilterProducts(List<CatalogCollectionProduct> products, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return products;
        }

        return products
            .Where(x => x.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || x.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || x.Sku.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static List<CatalogCollectionProduct> SortProducts(List<CatalogCollectionProduct> products, string? sort)
    {
        return (sort ?? string.Empty).ToUpperInvariant() switch
        {
            "SORTORDERDESC" => products.OrderByDescending(x => x.SortOrder).ThenBy(x => x.Title).ToList(),
            "NAMEDESC" => products.OrderByDescending(x => x.Title).ThenBy(x => x.Sku).ToList(),
            "CREATEDASC" => products.OrderBy(x => x.CreatedDate).ThenBy(x => x.Title).ToList(),
            "CREATEDDESC" => products.OrderByDescending(x => x.CreatedDate).ThenBy(x => x.Title).ToList(),
            "UPDATEDASC" => products.OrderBy(x => x.UpdatedDate).ThenBy(x => x.Title).ToList(),
            "UPDATEDDESC" => products.OrderByDescending(x => x.UpdatedDate).ThenBy(x => x.Title).ToList(),
            "NAMEASC" => products.OrderBy(x => x.Title).ThenBy(x => x.Sku).ToList(),
            _ => products.OrderBy(x => x.SortOrder).ThenBy(x => x.Title).ToList(),
        };
    }

    private IReadOnlyDictionary<int, CatalogCollectionCounts> GetChildCounts(IReadOnlyList<int> parentIds)
    {
        if (parentIds.Count == 0)
        {
            return new Dictionary<int, CatalogCollectionCounts>();
        }

        using var scope = _scopeProvider.CreateScope(autoComplete: true);
        var rows = scope.Database.Fetch<CatalogCollectionCountRow>(
            @"SELECT n.parentId AS ParentId, ct.alias AS ContentTypeAlias, COUNT(*) AS Count
FROM umbracoNode n
INNER JOIN umbracoContent c ON c.nodeId = n.id
INNER JOIN cmsContentType ct ON ct.nodeId = c.contentTypeId
WHERE n.parentId IN (@0)
  AND n.trashed = 0
  AND ct.alias IN ('ekmCategory', 'ekmProduct')
GROUP BY n.parentId, ct.alias",
            parentIds);

        var counts = new Dictionary<int, CatalogCollectionCounts>();
        foreach (var row in rows)
        {
            if (!counts.TryGetValue(row.ParentId, out var count))
            {
                count = new CatalogCollectionCounts();
                counts[row.ParentId] = count;
            }

            if (row.ContentTypeAlias.Equals("ekmProduct", StringComparison.OrdinalIgnoreCase))
            {
                count.ProductCount = row.Count;
            }
            else if (row.ContentTypeAlias.Equals("ekmCategory", StringComparison.OrdinalIgnoreCase))
            {
                count.SubcategoryCount = row.Count;
            }
        }

        return counts;
    }

    private static CatalogCollectionNode MapNode(IContent content, CatalogCollectionCounts? counts = null)
    {
        return new CatalogCollectionNode
        {
            Id = content.Id,
            Key = content.Key,
            Name = content.Name ?? string.Empty,
            Title = GetTitle(content),
            ContentTypeAlias = content.ContentType.Alias,
            SortOrder = content.SortOrder,
            ProductCount = counts?.ProductCount ?? 0,
            SubcategoryCount = counts?.SubcategoryCount ?? 0,
        };
    }

    private static CatalogCollectionProduct MapProduct(IContent content)
    {
        var price = GetPrice(content);
        var pendingChanges = content.Published && content.Edited;

        return new CatalogCollectionProduct
        {
            Id = content.Id,
            Key = content.Key,
            Name = content.Name ?? string.Empty,
            Title = GetTitle(content),
            ContentTypeAlias = content.ContentType.Alias,
            SortOrder = content.SortOrder,
            Sku = GetStringValue(content, "sku"),
            PriceValue = price.Value,
            Price = FormatPrice(price),
            CreatedDate = content.CreateDate,
            UpdatedDate = content.UpdateDate,
            Published = content.Published,
            PendingChanges = pendingChanges,
            Status = content.Published ? pendingChanges ? "Pending changes" : "Published" : "Unpublished",
            Available = IsAvailable(content),
            Image = GetFirstImage(GetStringValue(content, "images")),
        };
    }

    private static string GetFirstImage(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmedValue = value.Trim();
        if (trimmedValue.StartsWith("[", StringComparison.Ordinal))
        {
            return GetFirstImageFromJson(trimmedValue);
        }

        return NormalizeMediaIdentifier(trimmedValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? string.Empty);
    }

    private static string GetFirstImageFromJson(string value)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return string.Empty;
            }

            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (TryGetJsonString(item, "mediaKey", out var mediaKey) || TryGetJsonString(item, "key", out mediaKey))
                {
                    return NormalizeMediaIdentifier(mediaKey);
                }
            }
        }
        catch (System.Text.Json.JsonException)
        {
            return string.Empty;
        }

        return string.Empty;
    }

    private static bool TryGetJsonString(JsonElement item, string propertyName, out string value)
    {
        value = string.Empty;
        if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static string NormalizeMediaIdentifier(string value)
    {
        const string mediaUdiPrefix = "umb://media/";
        var trimmedValue = value.Trim();
        var identifier = trimmedValue.StartsWith(mediaUdiPrefix, StringComparison.OrdinalIgnoreCase)
            ? trimmedValue[mediaUdiPrefix.Length..]
            : trimmedValue;

        return Guid.TryParse(identifier, out var key) ? key.ToString("D") : identifier;
    }

    private static string GetTitle(IContent content)
    {
        var title = GetStringValue(content, "title");
        return string.IsNullOrWhiteSpace(title) ? content.Name ?? string.Empty : title;
    }

    private static string GetStringValue(IContent content, string alias)
    {
        if (!content.HasProperty(alias))
        {
            return string.Empty;
        }

        var value = content.GetValue<string>(alias) ?? string.Empty;
        var property = ParsePropertyValue(value);
        if (property?.Values == null)
        {
            return value;
        }

        return property.Values.Values.Select(x => x?.ToString()).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
    }

    private static PropertyValue? ParsePropertyValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            var propertyValue = value.InvariantContains("values") ? value : "{\"values\":" + value + "}";
            return JsonConvert.DeserializeObject<PropertyValue>(propertyValue);
        }
        catch (Newtonsoft.Json.JsonException)
        {
            return null;
        }
    }

    private static CatalogProductPrice GetPrice(IContent content)
    {
        if (!content.HasProperty("price"))
        {
            return CatalogProductPrice.Empty;
        }

        var value = content.GetValue<string>("price");
        if (string.IsNullOrWhiteSpace(value))
        {
            return CatalogProductPrice.Empty;
        }

        try
        {
            var prices = JsonConvert.DeserializeObject<CurrencyPriceRoot>(value);
            return GetDefaultCurrencyPrice(prices);
        }
        catch (Newtonsoft.Json.JsonException)
        {
            return CatalogProductPrice.Empty;
        }
    }

    private static CatalogProductPrice GetDefaultCurrencyPrice(CurrencyPriceRoot? prices)
    {
        var store = API.Store.Instance.GetStore() ?? API.Store.Instance.GetAllStores().FirstOrDefault();
        var currency = store?.Currency;
        var storePrices = GetStorePrices(prices, store?.Alias);

        var price = GetCurrencyPrice(storePrices, currency?.CurrencyValue, currency?.ISOCurrencySymbol)
            ?? GetCurrencyPrice(prices?.SelectMany(x => x.Value), currency?.CurrencyValue, currency?.ISOCurrencySymbol)
            ?? storePrices?.FirstOrDefault(x => x.Price.HasValue)
            ?? prices?.SelectMany(x => x.Value).FirstOrDefault(x => x.Price.HasValue);

        return new CatalogProductPrice(price?.Price, currency);
    }

    private static IReadOnlyList<CurrencyPrice>? GetStorePrices(CurrencyPriceRoot? prices, string? storeAlias)
    {
        if (prices == null || string.IsNullOrWhiteSpace(storeAlias))
        {
            return null;
        }

        return prices.FirstOrDefault(x => x.Key.Equals(storeAlias, StringComparison.OrdinalIgnoreCase)).Value;
    }

    private static CurrencyPrice? GetCurrencyPrice(IEnumerable<CurrencyPrice>? prices, string? currencyValue, string? isoCurrencySymbol)
    {
        if (prices == null)
        {
            return null;
        }

        return prices.FirstOrDefault(x => x.Price.HasValue
            && (string.Equals(x.Currency, currencyValue, StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.Currency, isoCurrencySymbol, StringComparison.OrdinalIgnoreCase)));
    }

    private static string FormatPrice(CatalogProductPrice price)
    {
        if (price.Value == null)
        {
            return string.Empty;
        }

        if (price.Currency == null)
        {
            return price.Value.Value.ToString("N0", CultureInfo.InvariantCulture);
        }

        try
        {
            return price.Value.Value.ToString(price.Currency.CurrencyFormat, CultureInfo.GetCultureInfo(price.Currency.CurrencyValue));
        }
        catch (ArgumentException)
        {
            return price.Value.Value.ToString("N0", CultureInfo.InvariantCulture);
        }
    }

    private static bool IsAvailable(IContent content)
    {
        try
        {
            return API.Stock.Instance.GetStock(content.Key) > 0;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private sealed class CatalogCollectionCounts
    {
        public int ProductCount { get; set; }
        public int SubcategoryCount { get; set; }
    }

    private sealed class CatalogCollectionCountRow
    {
        public int ParentId { get; set; }
        public string ContentTypeAlias { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    private sealed class CatalogProductPrice
    {
        public static readonly CatalogProductPrice Empty = new(null, null);

        public CatalogProductPrice(decimal? value, CurrencyModel? currency)
        {
            Value = value;
            Currency = currency;
        }

        public decimal? Value { get; }
        public CurrencyModel? Currency { get; }
    }
}
