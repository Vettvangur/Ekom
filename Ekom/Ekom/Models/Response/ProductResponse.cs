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
        Filters = Enumerable.Empty<MetafieldGrouped>();
    }

    public ProductResponse(IEnumerable<IProduct> products, ProductQuery? query = null, IProductFilterService? filterService = null, ICategory? category = null)
    {
        if (query == null)
        {
            var baseList = products as List<IProduct> ?? products.ToList();

            if (filterService != null)
                products = filterService.ApplyFilters(baseList, query, category);

            products = CatalogEvents.RaiseOnBeforeReturnProducts(products);

            Products = products;
            ProductCount = baseList.Count;
            TotalProductCount = ProductCount;
            return;
        }

        IEnumerable<IProduct> working = products as List<IProduct> ?? products.ToList();

        if (query.PropertySelectors?.Any() == true)
        {
            foreach (var selector in query.PropertySelectors.Where(s => !string.IsNullOrEmpty(s.Key)))
            {
                var sep = query.PropertySelectorsSeparator;

                var propertyValues = working
                    .SelectMany(x => x.GetValue(selector.Key, selector.Value)?
                        .Split(new[] { sep }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(v => v.Trim())
                        ?? Array.Empty<string>())
                    .Where(v => !string.IsNullOrEmpty(v))
                    .GroupBy(v => v)
                    .Select(g => (g.Key, g.Count()))
                    .ToList();

                PropertySelectors.Add(selector.Key, propertyValues);
            }
        }

        if (query.AllFiltersVisible)
            Filters = working.Filters();

        if (query.MetaFilters?.Any() == true || query.PropertyFilters?.Any() == true)
            working = working.Filter(query);

        if (!string.IsNullOrEmpty(query.SearchQuery))
        {
            using var scope = Configuration.Resolver.CreateScope();
            var searchService = scope.ServiceProvider.GetService<ICatalogSearchService>();

            long total = 0;
            var ids = searchService?.ProductQuery(new SearchRequest
            {
                SearchQuery = query.SearchQuery,
                NodeTypeAlias = new[] { "ekmProduct", "ekmCategory", "ekmVariant" },
                SearchFields = query.SearchFields
            }, out total) ?? Enumerable.Empty<int>();

            if (total > 0)
            {
                var idSet = ids is HashSet<int> hs ? hs : new HashSet<int>(ids);
                working = working.Where(p => idSet.Contains(p.Id));
            }
            else
            {
                working = Enumerable.Empty<IProduct>();
            }
        }

        if (!query.AllFiltersVisible)
            Filters = working.Filters();


        if (filterService != null && query.RaiseEvents)
            working = filterService.ApplyFilters(working, query, category);

        if (query.RaiseEvents)
            working = CatalogEvents.RaiseOnBeforeReturnProducts(working);

        // Query predicate
        if (query.Filter != null)
            working = working.Where(query.Filter);

        if (query.FilterOutZeroPriceProducts)
        {
            working = working.Where(p =>
            {
                var pv = p.PrimaryVariant;
                var price = pv?.Price ?? p.Price;
                return price?.Value > 0;
            });
        }

        // Materialize once for counts + paging
        var finalList = working as List<IProduct> ?? working.ToList();

        // Total AFTER price filtering (your requirement)
        TotalProductCount = finalList.Count;

        // Sorting
        if (query.OrderBy != Utilities.OrderBy.NoOrder)
            finalList = OrderBy(finalList, query?.OrderBy ?? Configuration.Instance.DefaultProductOrderBy).ToList();

        ProductCount = finalList.Count;

        // Paging
        if (query.PageSize.HasValue && query.Page.HasValue)
        {
            var pageSize = query.PageSize.Value;
            var page = query.Page.Value;

            PageSize = pageSize;
            Page = page;

            PageCount = (ProductCount + pageSize - 1) / pageSize;

            Products = finalList
                .Skip((page - 1) * pageSize)
                .Take(pageSize);
        }
        else
        {
            Products = finalList;
        }

    }

    public IEnumerable<IProduct> Products { get; set; }
    public int? PageCount { get; set; }
    public int? PageSize { get; set; }
    public int? Page { get; set; }
    public int ProductCount { get; set; }
    public int TotalProductCount { get; set; }
    public IEnumerable<MetafieldGrouped> Filters { get; set; } = new List<MetafieldGrouped>();
    public Dictionary<string, List<(string, int)>> PropertySelectors = new Dictionary<string, List<(string, int)>>();
    private IEnumerable<IProduct> OrderBy(IEnumerable<IProduct> products, OrderBy orderBy)
    {
        if (orderBy == Utilities.OrderBy.TitleAsc)
        {
            return products.OrderBy(x => x.Title);
        }
        else if (orderBy == Utilities.OrderBy.TitleDesc)
        {
            return products.OrderByDescending(x => x.Title);
        }
        else if (orderBy == Utilities.OrderBy.PriceAsc)
        {
            return products.OrderBy(p =>
            {
                var pv = p.PrimaryVariant;
                return (pv?.Price?.Value ?? p.OriginalPrice?.Value) ?? 0m;
            });
        }
        else if (orderBy == Utilities.OrderBy.PriceDesc)
        {
            return products.OrderByDescending(p =>
            {
                var pv = p.PrimaryVariant;
                return (pv?.Price?.Value ?? p.OriginalPrice?.Value) ?? 0m;
            });
        }
        else if (orderBy == Utilities.OrderBy.DateAsc)
        {
            return products.OrderBy(x => x.CreateDate);
        }
        else if (orderBy == Utilities.OrderBy.DateDesc)
        {
            return products.OrderByDescending(x => x.CreateDate);
        }
        else if (orderBy == Utilities.OrderBy.UmbracoSortOrderAsc)
        {
            return products.OrderBy(x => x.SortOrder);
        }
        else if (orderBy == Utilities.OrderBy.UmbracoSortOrderDesc)
        {
            return products.OrderByDescending(x => x.SortOrder);
        }
        else if (orderBy == Utilities.OrderBy.SkuAsc)
        {
            return products.OrderBy(x => x.SKU);
        }
        else if (orderBy == Utilities.OrderBy.SkuDesc)
        {
            return products.OrderByDescending(x => x.SKU);
        }
        else if (orderBy == Utilities.OrderBy.Score)
        {
            return products.OrderByDescending(x =>
            {
                string scoreValue = x.GetValue("score");
                if (string.IsNullOrEmpty(scoreValue))
                {
                    return double.MinValue;
                }

                // Try to parse the score to a double
                if (double.TryParse(scoreValue.ToString(), out double score))
                {
                    return score;
                }
                else
                {
                    return double.MinValue; // or any default value in case of parsing failure
                }
            });
        }

        return products.OrderBy(x => x.SortOrder);
    }
}
