using Ekom.Models;

namespace Ekom.Services;

public class ProductFilterService : IProductFilterService
{
    /// <inheritdoc/>
    public virtual IEnumerable<IProduct> ApplyFilters(IEnumerable<IProduct> products, ProductQuery? query = null, ICategory? category = null)
    {
        return products;
    }

    /// <inheritdoc/>
    public virtual Task<IEnumerable<IProduct>> ApplyFiltersAsync(IEnumerable<IProduct> products, ProductQuery? query = null, ICategory? category = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ApplyFilters(products, query, category));
    }
}
