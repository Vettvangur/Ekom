using Ekom.Models;

namespace Ekom.Services;

public class ProductFilterService : IProductFilterService
{
    public virtual IEnumerable<IProduct> ApplyFilters(IEnumerable<IProduct> products, ProductQuery? query = null, ICategory? category = null)
    {
        return products;
    }

    public virtual Task<IEnumerable<IProduct>> ApplyFiltersAsync(
        IEnumerable<IProduct> products,
        ProductQuery? query = null,
        ICategory? category = null,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var result = ApplyFilters(products, query, category);
        return Task.FromResult(result);
    }
}
