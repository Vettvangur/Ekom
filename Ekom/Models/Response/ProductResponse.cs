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

    public ProductResponse(IEnumerable<IProduct> products, ProductQuery? query = null, IProductFilterService? filterService = null, ICategory category = null)
    {
        if (query != null)
        {
            // Filter out zero-price products
            if (query.FilterOutZeroPriceProducts)
            {
                products = products.Where(x => x.OriginalPrice?.Value > 0);
            }

            // Store the total number of products before any filtering
            TotalProductCount = products.Count();

            // Apply Property Selectors
            if (query.PropertySelectors?.Any() == true)
            {
                foreach (var selector in query.PropertySelectors.Where(s => !string.IsNullOrEmpty(s.Key)))
                {
                    var separator = query.PropertySelectorsSeparator;

                    var propertyValues = products
                        .SelectMany(x => x.GetValue(selector.Key, selector.Value)?
                                                    .Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries)
                                                    .Select(value => value.Trim())
                                                    ?? Array.Empty<string>())
                        .Where(x => !string.IsNullOrEmpty(x))
                        .GroupBy(value => value)
                        .Select(group => (group.Key, group.Count()))
                        .ToList();

                    PropertySelectors.Add(selector.Key, propertyValues);
                }
            }

            if (query.AllFiltersVisible)
            {
                Filters = products.Filters();
            }

            // Apply Filters
            if (query.MetaFilters?.Any() == true || query.PropertyFilters?.Any() == true)
            {
                products = products.Filter(query);
            }

            // Apply Search Filtering
            if (!string.IsNullOrEmpty(query.SearchQuery))
            {
                long total = 0;

                using var scope = Configuration.Resolver.CreateScope();
                var searchService = scope.ServiceProvider.GetService<ICatalogSearchService>();
                var searchResults = searchService?.ProductQuery(new SearchRequest
                {
                    SearchQuery = query.SearchQuery,
                    NodeTypeAlias = new[] { "ekmProduct", "ekmCategory", "ekmVariant" },
                    SearchFields = query.SearchFields
                }, out total) ?? Enumerable.Empty<int>();

                products = total > 0 ? products.Where(x => searchResults.Contains(x.Id)) : Enumerable.Empty<IProduct>();
            }

            if (!query.AllFiltersVisible)
            {
                Filters = products.Filters();
            }

            // Apply Sorting
            if (query.OrderBy != Utilities.OrderBy.NoOrder)
            {
                products = OrderBy(products, query?.OrderBy ?? Utilities.OrderBy.TitleAsc);
            }

            // Apply Additional Filtering via filterService
            if (filterService != null)
            {
                products = filterService.ApplyFilters(products, query);
            }

            // Apply Query Filter
            if (query.Filter != null)
            {
                products = products.Where(query.Filter);
            }

            // Store the count after filtering
            ProductCount = products.Count();

            // Apply Pagination
            if (query.PageSize.HasValue && query.Page.HasValue)
            {
                PageSize = query.PageSize.Value;
                PageCount = (ProductCount + PageSize - 1) / PageSize;
                Page = query.Page.Value;

                Products = products.Skip((Page.Value - 1) * PageSize.Value).Take(PageSize.Value);
            }
            else
            {
                Products = products;
            }
        }
        else
        {
            // Apply Additional Filtering via filterService
            if (filterService != null)
            {
                products = filterService.ApplyFilters(products);
            }

            Products = products;
            ProductCount = products.Count();
            TotalProductCount = ProductCount;
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
            return products.OrderBy(x =>
                x.AllVariants != null && x.AllVariants.Any() ? x.AllVariants.Min(v => v.OriginalPrice?.Value) : x.OriginalPrice?.Value);
        }
        else if (orderBy == Utilities.OrderBy.PriceDesc)
        {
            return products.OrderByDescending(x =>
                x.AllVariants != null && x.AllVariants.Any() ? x.AllVariants.Min(v => v.OriginalPrice?.Value) : x.OriginalPrice?.Value);
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
                var scoreValue = x.GetValue("score");
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
