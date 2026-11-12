using Ekom.Models;
using Ekom.Models.Umbraco;
using Ekom.Umb.CatalogCollection.Models;
using Ekom.Umb.Models;
using Ekom.Utilities;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<CatalogCollectionService> _logger;

    public CatalogCollectionService(
        IContentService contentService,
        IContentTypeService contentTypeService,
        IScopeProvider scopeProvider,
        ILogger<CatalogCollectionService> logger)
    {
        _contentService = contentService;
        _contentTypeService = contentTypeService;
        _scopeProvider = scopeProvider;
        _logger = logger;
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
        var status = CatalogCollectionIndex.NormalizeStatus(request.Status);

        // Narrow rows for the whole subtree: enough for counts, filters, sorting and paging
        // without loading content. Full content is only loaded for the page being shown.
        var rows = new CatalogCollectionRowReader(_scopeProvider);
        var productRows = rows.GetProductRows(current.Path);
        var variantRows = rows.GetVariantRows(current.Path);
        var links = rows.GetLinkRows(current.Id, current.Path);
        rows.AddLinkedProducts(productRows, variantRows, links);

        var index = CatalogCollectionIndex.Build(
            current.Id,
            current.Path,
            productRows,
            variantRows,
            links,
            CreateStockReader());

        var subcategoryContent = GetCatalogChildren(current.Id, "ekmCategory")
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToList();
        var subcategoryCounts = GetChildCounts(subcategoryContent.Select(x => x.Id).ToList());
        var subcategories = subcategoryContent
            .Select(category => MapNode(category, subcategoryCounts.GetValueOrDefault(category.Id), index.GetBranch(category.Id)))
            .ToList();

        IReadOnlyList<CatalogProductEntry> candidates = index.DirectProducts;
        Dictionary<int, IContent>? loadedContent = null;

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            // Title and SKU live in property data, so a text search has to read the content.
            loadedContent = GetCatalogChildren(current.Id, "ekmProduct").ToDictionary(x => x.Id);
            var linkedIds = index.DirectProducts.Where(x => x.Linked && !loadedContent.ContainsKey(x.Id)).Select(x => x.Id).ToList();
            foreach (var linked in _contentService.GetByIds(linkedIds))
            {
                loadedContent[linked.Id] = linked;
            }

            var matches = loadedContent.Values
                .Where(x => MatchesQuery(x, request.Query))
                .Select(x => x.Id)
                .ToHashSet();
            candidates = index.DirectProducts.Where(x => matches.Contains(x.Id)).ToList();
        }

        var statusCounts = CatalogCollectionIndex.CountStatuses(candidates);
        var filtered = CatalogCollectionIndex.Sort(CatalogCollectionIndex.FilterByStatus(candidates, status), request.Sort);
        var totalPages = Math.Max(1, (int)Math.Ceiling(filtered.Count / (double)pageSize));
        page = Math.Min(page, totalPages);

        var pageEntries = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var pageContent = loadedContent
            ?? _contentService.GetByIds(pageEntries.Select(x => x.Id)).ToDictionary(x => x.Id);
        var storeAlias = GetDefaultStore()?.Alias;
        var products = new List<CatalogCollectionProduct>(pageEntries.Count);
        foreach (var entry in pageEntries)
        {
            if (pageContent.TryGetValue(entry.Id, out var content))
            {
                products.Add(MapProduct(content, entry, storeAlias));
            }
        }

        var totals = GetCategoryTotals(current);
        var currentNode = MapNode(current);
        currentNode.TotalProductCount = index.TotalProductCount;
        currentNode.ProductCount = index.DirectProducts.Count;
        currentNode.SubcategoryCount = subcategories.Count;

        return new CatalogCollectionResponse
        {
            Current = currentNode,
            Parent = GetParent(current),
            Breadcrumbs = GetBreadcrumbs(current, totals),
            Subcategories = subcategories,
            Products = products,
            ProductCount = index.DirectProducts.Count,
            SubcategoryCount = subcategories.Count,
            FilteredProductCount = filtered.Count,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            Status = status,
            StatusCounts = statusCounts,
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

        return _contentService.GetPagedChildNodes(
                parentId,
                0,
                int.MaxValue,
                out _,
                filter,
                Ordering.ByDefault())
            .ToList();
    }

    /// <summary>
    /// Recursive product totals for every category in the catalog, from two aggregate queries.
    /// Used for the sibling lists in the breadcrumb.
    /// </summary>
    private IReadOnlyDictionary<int, int> GetCategoryTotals(IContent current)
    {
        var top = current;
        while (top.ParentId > 0)
        {
            var parent = _contentService.GetById(top.ParentId);
            if (parent == null || !CatalogAliases.Contains(parent.ContentType.Alias))
            {
                break;
            }

            top = parent;
        }

        // Siblings of the top catalog node share its parent, so count from one level above it.
        var lastSeparator = top.Path.LastIndexOf(',');
        var scopePath = lastSeparator > 0 ? top.Path[..lastSeparator] : top.Path;

        return new CatalogCollectionRowReader(_scopeProvider).GetCategoryTotals(scopePath);
    }

    private static IStore? GetDefaultStore()
        => API.Store.Instance.GetStore() ?? API.Store.Instance.GetAllStores().FirstOrDefault();

    private static Func<Guid, decimal> CreateStockReader()
    {
        var storeAlias = Configuration.Instance.PerStoreStock ? GetDefaultStore()?.Alias : null;

        return key =>
        {
            try
            {
                return string.IsNullOrWhiteSpace(storeAlias)
                    ? API.Stock.Instance.GetStock(key)
                    : API.Stock.Instance.GetStock(key, storeAlias);
            }
            catch (InvalidOperationException)
            {
                return 0;
            }
        };
    }

    private static bool MatchesQuery(IContent content, string query)
    {
        return GetTitle(content).Contains(query, StringComparison.OrdinalIgnoreCase)
            || (content.Name ?? string.Empty).Contains(query, StringComparison.OrdinalIgnoreCase)
            || GetStringValue(content, "sku").Contains(query, StringComparison.OrdinalIgnoreCase);
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

    private IReadOnlyList<CatalogCollectionBreadcrumb> GetBreadcrumbs(IContent content, IReadOnlyDictionary<int, int> totals)
    {
        var breadcrumbs = new List<IContent>();
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
                breadcrumbs.Add(parent);
            }

            current = parent;
        }

        breadcrumbs.Reverse();
        breadcrumbs.Add(content);
        return breadcrumbs.Select(x => MapBreadcrumb(x, totals)).ToList();
    }

    private CatalogCollectionBreadcrumb MapBreadcrumb(IContent content, IReadOnlyDictionary<int, int> totals)
    {
        IReadOnlyList<CatalogCollectionNode> siblings = content.ContentType.Alias.Equals("ekmCategory", StringComparison.OrdinalIgnoreCase)
            && content.ParentId > 0
                ? GetCatalogChildren(content.ParentId, "ekmCategory")
                    .OrderBy(x => x.SortOrder)
                    .ThenBy(x => x.Name)
                    .Select(category =>
                    {
                        var sibling = MapNode(category);
                        sibling.TotalProductCount = totals.GetValueOrDefault(category.Id);
                        return sibling;
                    })
                    .ToList()
                : Array.Empty<CatalogCollectionNode>();

        return new CatalogCollectionBreadcrumb
        {
            Id = content.Id,
            Key = content.Key,
            Name = content.Name ?? string.Empty,
            Title = GetTitle(content),
            ContentTypeAlias = content.ContentType.Alias,
            SortOrder = content.SortOrder,
            Siblings = siblings,
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

    private static CatalogCollectionNode MapNode(IContent content, CatalogCollectionCounts? counts = null, CatalogBranchSummary? branch = null)
    {
        var pendingChanges = content.Published && content.Edited;

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
            Published = content.Published,
            PendingChanges = pendingChanges,
            Status = content.Published ? pendingChanges ? "Pending changes" : "Published" : "Unpublished",
            TotalProductCount = branch?.Total ?? 0,
            LiveCount = branch?.Live ?? 0,
            OutOfStockCount = branch?.OutOfStock ?? 0,
        };
    }

    private CatalogCollectionProduct MapProduct(IContent content, CatalogProductEntry entry, string? storeAlias)
    {
        var price = GetPrice(content);
        var pendingChanges = content.Published && content.Edited;
        var product = new CatalogCollectionProduct
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
            Available = entry.Stock > 0,
            Linked = entry.Linked,
            Stock = entry.Stock,
            VariantCount = entry.VariantCount,
            Image = GetFirstImage(GetStringValue(content, "images")),
            Published = content.Published,
            PendingChanges = pendingChanges,
            Status = content.Published ? pendingChanges ? "Pending changes" : "Published" : "Unpublished",
        };

        if (content.Published)
        {
            ApplyDiscountedPrice(product, price.Currency, storeAlias);
        }

        return product;
    }

    /// <summary>
    /// Shows the discounted price with the original struck through when the storefront
    /// product currently has a product discount.
    /// </summary>
    private void ApplyDiscountedPrice(CatalogCollectionProduct product, CurrencyModel? currency, string? storeAlias)
    {
        try
        {
            var price = API.Catalog.Instance.GetProduct(product.Key, storeAlias, raiseEvent: false)?.Price;
            if (price == null)
            {
                return;
            }

            var before = price.BeforeDiscount.Value;
            var after = price.AfterDiscount.Value;
            if (after <= 0 || after >= before)
            {
                return;
            }

            product.PriceValue = after;
            product.Price = FormatPrice(new CatalogProductPrice(after, currency));
            product.OriginalPrice = FormatPrice(new CatalogProductPrice(before, currency));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or NullReferenceException or KeyNotFoundException)
        {
            // The storefront price pipeline can be unavailable for a node; keep the stored price.
            _logger.LogDebug(ex, "Could not resolve discounted price for product {ProductKey}", product.Key);
        }
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

        if (Configuration.Instance.UseDatabasePrices)
        {
            var store = API.Store.Instance.GetStore() ?? API.Store.Instance.GetAllStores().FirstOrDefault();
            if (store != null)
            {
                var item = new Umbraco17Content(content, Guid.Empty);
                return new CatalogProductPrice(item.GetPrice(store.Alias, store.Currency.CurrencyValue), store.Currency);
            }
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
