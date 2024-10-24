using Ekom.Models;

namespace Ekom.Services;

public class ProductFilterService : IProductFilterService
{
    public virtual IEnumerable<IProduct> ApplyFilters(IEnumerable<IProduct> products)
    {
        return products;
    }
}
