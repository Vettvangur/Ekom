using Ekom.Services;
using Ekom.Utilities;
using Microsoft.Extensions.DependencyInjection;

namespace Ekom.Models
{
    public class ProductResponse
    {
        public ProductResponse()
        {
            Products = Enumerable.Empty<IProduct>();
            ProductCount = 0;
            Filters = Enumerable.Empty<MetafieldGrouped>();
        }

        public ProductResponse(IEnumerable<IProduct> products, ProductQuery query)
        {

            Products = products;

            if (query?.AllFiltersVisible == true)
            {
                Filters = products.Filters();
            }

            if (query?.MetaFilters?.Any() == true || query?.PropertyFilters?.Any() == true)
            {
                products = products.Filter(query);
            }

            if (!string.IsNullOrEmpty(query?.SearchQuery))
            {
                var scope = Configuration.Resolver.CreateScope();
                var _searhService = scope.ServiceProvider.GetService<ICatalogSearchService>();
                var searchResults = _searhService.ProductQuery(new SearchRequest() {
                    SearchQuery = query.SearchQuery,
                    NodeTypeAlias = new string[] { "ekmProduct", "ekmCategory", "ekmVariant" },
                    SearchFields = query.SearchFields
                }, out long total);
                
                scope.Dispose();

                if (searchResults == null || total <= 0)
                {
                    products = Enumerable.Empty<IProduct>();
                } else
                {
                    products = products.Where(x => searchResults.Any(y => y == x.Id));
                }
            }

            if (query?.AllFiltersVisible == false)
            {
                Filters = products.Filters();
            }

            ProductCount = products.Count();

            if (query?.OrderBy != Utilities.OrderBy.NoOrder)
            {
                products = OrderBy(products, query?.OrderBy ?? Utilities.OrderBy.TitleAsc);
            }

            if (query?.PageSize.HasValue == true && query?.Page.HasValue == true)
            {
                Page = query.Page;
                PageSize = query.PageSize;
                PageCount = (ProductCount + PageSize - 1) / PageSize;

                if (Page > PageCount)
                    Page = PageCount;

                if (Page < 1)
                    Page = 1;

                Products = products.Skip((Page.Value - 1) * PageSize.Value).Take(PageSize.Value);

            } else
            {
                Products = products;
            }
        }
        
        public IEnumerable<IProduct> Products { get; set; }
        public int? PageCount { get; set; }
        public int? PageSize { get; set; }
        public int? Page { get; set; }
        public int ProductCount { get; set; }
        public IEnumerable<MetafieldGrouped> Filters { get; set; } = new List<MetafieldGrouped>();
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
                return products.OrderBy(x => x.OriginalPrice.Value);
            }
            else if (orderBy == Utilities.OrderBy.PriceDesc)
            {
                return products.OrderByDescending(x => x.OriginalPrice.Value);
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

            return products.OrderBy(x => x.SortOrder);
        } 
    }
}
