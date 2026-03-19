using Ekom.Models;
using Ekom.Services;

public class ProductFilterService : IProductFilterService
{
    public virtual IEnumerable<IProduct> ApplyFilters(
        IEnumerable<IProduct> products,
        ProductQuery? query = null,
        ICategory? category = null)
    {
        // Sync API bridges to async implementation (single source of truth)
        return ApplyFiltersAsync(products, query, category, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }

    public virtual Task<IEnumerable<IProduct>> ApplyFiltersAsync(
        IEnumerable<IProduct> products,
        ProductQuery? query = null,
        ICategory? category = null,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        // Default async implementation just uses sync override if that's what exists
        return Task.FromResult(products);
    }
}
