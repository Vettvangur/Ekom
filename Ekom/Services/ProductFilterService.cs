using Ekom.Models;

namespace Ekom.Services;

public class ProductFilterService : IProductFilterService
{
    public virtual IEnumerable<IProduct> ApplyFilters(IEnumerable<IProduct> products, ProductQuery? query = null, ICategory? category = null)
    {
        return products;
    }
}
