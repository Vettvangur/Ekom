using Ekom.Events;
using Ekom.Services;
using Ekom.Utilities;
using Microsoft.Extensions.DependencyInjection;

namespace Ekom.Models;

public class ProductResponse
{
    public ProductResponse()
    {
        Products = Enumerable.Empty<IProduct>();
        ProductCount = 0;
        TotalProductCount = 0;
        Filters = Enumerable.Empty<MetafieldGrouped>();
    }

    /// <summary>
    /// Backwards compatible ctor (sync pipeline; no cancellation).
    /// </summary>
    public ProductResponse(
        IEnumerable<IProduct> products,
        ProductQuery? query = null,
        IProductFilterService? filterService = null,
        ICategory? category = null)
        : this()
    {
        // sync event raiser (no blocking)
        BuildCoreAsync(
            products,
            query,
            filterService,
            category,
            raiseBeforeReturnProducts: static (p, _) => new ValueTask<IEnumerable<IProduct>>(CatalogEvents.RaiseOnBeforeReturnProducts(p)),
            ct: CancellationToken.None
        ).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Preferred async factory (async-first events + cancellation).
    /// </summary>
    public static async Task<ProductResponse> CreateAsync(
        IEnumerable<IProduct> products,
        ProductQuery? query = null,
        IProductFilterService? filterService = null,
        ICategory? category = null,
        CancellationToken ct = default)
    {
        var pr = new ProductResponse();

        await pr.BuildCoreAsync(
            products,
            query,
            filterService,
            category,
            raiseBeforeReturnProducts: static (p, token) => CatalogEvents.RaiseOnBeforeReturnProductsAsync(p, token),
            ct: ct
        );

        return pr;
    }

    // =========================
    // Public result properties
    // =========================
    public IEnumerable<IProduct> Products { get; set; }
    public int? PageCount { get; set; }
    public int? PageSize { get; set; }
    public int? Page { get; set; }
    public int ProductCount { get; set; }
    public int TotalProductCount { get; set; }
    public IEnumerable<MetafieldGrouped> Filters { get; set; } = new List<MetafieldGrouped>();

    public Dictionary<string, List<(string, int)>> PropertySelectors { get; } = new();

    // =========================
    // Single shared pipeline
    // =========================
    private async Task BuildCoreAsync(
        IEnumerable<IProduct> products,
        ProductQuery? query,
        IProductFilterService? filterService,
        ICategory? category,
        Func<IEnumerable<IProduct>, CancellationToken, ValueTask<IEnumerable<IProduct>>> raiseBeforeReturnProducts,
        CancellationToken ct)
    {
        IEnumerable<IProduct> working;

        // Preserve your "query == null" behavior
        if (query == null)
        {
            var baseList = products as List<IProduct> ?? products.ToList();
            working = baseList;

            if (filterService != null)
                working = await ApplyFilterServiceAsync(filterService, baseList, query: null, category, ct);

            working = await raiseBeforeReturnProducts(working, ct);

            Products = working;
            ProductCount = baseList.Count;
            TotalProductCount = ProductCount;
            return;
        }

        // Materialize once
        working = products as List<IProduct> ?? products.ToList();

        // Functional-ish pipeline composition
        working = StepPropertySelectors(working, query);
        working = StepFiltersVisibleAll(working, query);

        working = StepApplyMetaAndPropertyFilters(working, query);

        working = await StepApplySearchAsync(working, query, ct);

        working = StepFiltersVisibleNotAll(working, query);

        working = await StepApplyFilterServiceAndEventsAsync(working, query, filterService, category, raiseBeforeReturnProducts, ct);

        working = StepApplyQueryPredicate(working, query);
        working = StepFilterOutZeroPrice(working, query);

        // Materialize once for counts/sort/paging
        var finalList = working as List<IProduct> ?? working.ToList();

        TotalProductCount = finalList.Count;

        if (query.OrderBy != Utilities.OrderBy.NoOrder)
            finalList = OrderBy(finalList, query.OrderBy ?? Configuration.Instance.DefaultProductOrderBy).ToList();

        ProductCount = finalList.Count;

        ApplyPaging(finalList, query);
    }

    // =========================
    // Steps
    // =========================

    private IEnumerable<IProduct> StepPropertySelectors(IEnumerable<IProduct> working, ProductQuery query)
    {
        if (query.PropertySelectors?.Any() != true)
            return working;

        foreach (var selector in query.PropertySelectors.Where(s => !string.IsNullOrEmpty(s.Key)))
        {
            var sep = query.PropertySelectorsSeparator ?? string.Empty;

            var values = working
                .SelectMany(p => p.GetValue(selector.Key, selector.Value)?
                    .Split(new[] { sep }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(v => v.Trim())
                    ?? Array.Empty<string>())
                .Where(v => !string.IsNullOrEmpty(v))
                .GroupBy(v => v)
                .Select(g => (g.Key, g.Count()))
                .ToList();

            PropertySelectors[selector.Key] = values;
        }

        return working;
    }

    private IEnumerable<IProduct> StepFiltersVisibleAll(IEnumerable<IProduct> working, ProductQuery query)
    {
        if (query.AllFiltersVisible)
            Filters = working.Filters();

        return working;
    }

    private IEnumerable<IProduct> StepApplyMetaAndPropertyFilters(IEnumerable<IProduct> working, ProductQuery query)
    {
        if (query.MetaFilters?.Any() == true || query.PropertyFilters?.Any() == true)
            return working.Filter(query);

        return working;
    }

    private async Task<IEnumerable<IProduct>> StepApplySearchAsync(IEnumerable<IProduct> working, ProductQuery query, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(query.SearchQuery))
            return working;

        ct.ThrowIfCancellationRequested();

        using var scope = Configuration.Resolver.CreateScope();
        var searchService = scope.ServiceProvider.GetService<ICatalogSearchService>();

        if (searchService == null)
            return Enumerable.Empty<IProduct>();

        var (ids, total) = await SearchProductsAsync(searchService, query, ct);

        if (total <= 0)
            return Enumerable.Empty<IProduct>();

        var idSet = ids is HashSet<int> hs ? hs : new HashSet<int>(ids);
        return working.Where(p => idSet.Contains(p.Id));
    }

    private IEnumerable<IProduct> StepFiltersVisibleNotAll(IEnumerable<IProduct> working, ProductQuery query)
    {
        if (!query.AllFiltersVisible)
            Filters = working.Filters();

        return working;
    }

    private async Task<IEnumerable<IProduct>> StepApplyFilterServiceAndEventsAsync(
        IEnumerable<IProduct> working,
        ProductQuery query,
        IProductFilterService? filterService,
        ICategory? category,
        Func<IEnumerable<IProduct>, CancellationToken, ValueTask<IEnumerable<IProduct>>> raiseBeforeReturnProducts,
        CancellationToken ct)
    {
        if (filterService != null && query.RaiseEvents)
            working = await ApplyFilterServiceAsync(filterService, working, query, category, ct);

        if (query.RaiseEvents)
            working = await raiseBeforeReturnProducts(working, ct);

        return working;
    }

    private static IEnumerable<IProduct> StepApplyQueryPredicate(IEnumerable<IProduct> working, ProductQuery query)
        => query.Filter != null ? working.Where(query.Filter) : working;

    private static IEnumerable<IProduct> StepFilterOutZeroPrice(IEnumerable<IProduct> working, ProductQuery query)
    {
        if (!query.FilterOutZeroPriceProducts)
            return working;

        return working.Where(p =>
        {
            var pv = p.PrimaryVariant;
            var price = pv?.Price ?? p.Price;
            return price?.Value > 0;
        });
    }

    private void ApplyPaging(List<IProduct> finalList, ProductQuery query)
    {
        if (query.PageSize.HasValue && query.Page.HasValue)
        {
            var pageSize = query.PageSize.Value;
            var page = query.Page.Value;

            PageSize = pageSize;
            Page = page;

            PageCount = (finalList.Count + pageSize - 1) / pageSize;

            Products = finalList
                .Skip((page - 1) * pageSize)
                .Take(pageSize);
        }
        else
        {
            Products = finalList;
        }
    }

    // =========================
    // Cancellation-propagated adapters
    // =========================

    private static async ValueTask<IEnumerable<IProduct>> ApplyFilterServiceAsync(
        IProductFilterService filterService,
        IEnumerable<IProduct> products,
        ProductQuery? query,
        ICategory? category,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var filtered = await filterService.ApplyFiltersAsync(products, query, category, ct);

        filtered = filterService.ApplyFilters(products, query, category);

        ct.ThrowIfCancellationRequested();
        return filtered;
    }

    private static async ValueTask<(IEnumerable<int> Ids, long Total)> SearchProductsAsync(
        ICatalogSearchService searchService,
        ProductQuery query,
        CancellationToken ct)
    {
        var request = new SearchRequest
        {
            SearchQuery = query.SearchQuery,
            NodeTypeAlias = new[] { "ekmProduct", "ekmCategory", "ekmVariant" },
            SearchFields = query.SearchFields
        };

        ct.ThrowIfCancellationRequested();

        return await searchService.ProductQueryAsync(request, ct);
    }

    // =========================
    // OrderBy
    // =========================
    private IEnumerable<IProduct> OrderBy(IEnumerable<IProduct> products, OrderBy orderBy)
    {
        if (orderBy == Utilities.OrderBy.TitleAsc)
            return products.OrderBy(x => x.Title);

        if (orderBy == Utilities.OrderBy.TitleDesc)
            return products.OrderByDescending(x => x.Title);

        if (orderBy == Utilities.OrderBy.PriceAsc)
        {
            return products.OrderBy(p =>
            {
                var pv = p.PrimaryVariant;
                return (pv?.Price?.Value ?? p.OriginalPrice?.Value) ?? 0m;
            });
        }

        if (orderBy == Utilities.OrderBy.PriceDesc)
        {
            return products.OrderByDescending(p =>
            {
                var pv = p.PrimaryVariant;
                return (pv?.Price?.Value ?? p.OriginalPrice?.Value) ?? 0m;
            });
        }

        if (orderBy == Utilities.OrderBy.DateAsc)
            return products.OrderBy(x => x.CreateDate);

        if (orderBy == Utilities.OrderBy.DateDesc)
            return products.OrderByDescending(x => x.CreateDate);

        if (orderBy == Utilities.OrderBy.UmbracoSortOrderAsc)
            return products.OrderBy(x => x.SortOrder);

        if (orderBy == Utilities.OrderBy.UmbracoSortOrderDesc)
            return products.OrderByDescending(x => x.SortOrder);

        if (orderBy == Utilities.OrderBy.SkuAsc)
            return products.OrderBy(x => x.SKU);

        if (orderBy == Utilities.OrderBy.SkuDesc)
            return products.OrderByDescending(x => x.SKU);

        if (orderBy == Utilities.OrderBy.Score)
        {
            return products.OrderByDescending(x =>
            {
                var scoreValue = x.GetValue("score");
                if (string.IsNullOrEmpty(scoreValue))
                    return double.MinValue;

                return double.TryParse(scoreValue, out var score) ? score : double.MinValue;
            });
        }

        return products.OrderBy(x => x.SortOrder);
    }
}
