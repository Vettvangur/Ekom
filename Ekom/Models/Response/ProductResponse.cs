using Ekom.Services;
using Ekom.Utilities;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Linq;

namespace Ekom.Models
{
    public class ProductResponse
    {
        public ProductResponse(IEnumerable<IProduct> products, ProductQuery query)
        {
            
            if (query?.MetaFilters?.Any() == true || query?.PropertyFilters?.Any() == true)
            {
                products = products.Filter(query);
            }

            if (!string.IsNullOrEmpty(query?.SearchQuery))
            {
                var scope = Configuration.Resolver.CreateScope();
                var _searhService = scope.ServiceProvider.GetService<ICatalogSearchService>();
                var searchResults = _searhService.QueryCatalog(query.SearchQuery, out long total, int.MaxValue);
                
                scope.Dispose();
                if (searchResults == null || total <= 0)
                {
                    products = Enumerable.Empty<IProduct>();
                } else
                {
                    products = products.Where(x => searchResults.Any(y => y.Id == x.Id));
                }
                
            }

            ProductCount = products.Count();

            if (query?.PageSize.HasValue == true && query?.Page.HasValue == true)
            {
                Page = query.Page;
                PageSize = query.PageSize;
                PageCount = (ProductCount + PageSize - 1) / PageSize;

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
    }
}
