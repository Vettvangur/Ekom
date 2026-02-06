using Ekom.Models;

namespace Ekom.Services;

public interface IProductFilterService
{
    /// <summary>
    /// Synchronously applies filters to the product collection.
    /// </summary>
    IEnumerable<IProduct> ApplyFilters(IEnumerable<IProduct> products, ProductQuery? query = null, ICategory? category = null);

    /// <summary>
    /// Asynchronously applies filters to the product collection.
    /// Default implementation delegates to the synchronous method.
    /// Override this method to provide true async filtering.
    /// </summary>
    Task<IEnumerable<IProduct>> ApplyFiltersAsync(IEnumerable<IProduct> products, ProductQuery? query = null, ICategory? category = null, CancellationToken cancellationToken = default)
        => Task.FromResult(ApplyFilters(products, query, category));
}
