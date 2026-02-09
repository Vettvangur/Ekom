using Ekom.Models;

namespace Ekom.Services;

public interface IProductFilterService
{
    IEnumerable<IProduct> ApplyFilters(
        IEnumerable<IProduct> products, 
        ProductQuery? query = null, 
        ICategory? category = null);
    Task<IEnumerable<IProduct>> ApplyFiltersAsync(
        IEnumerable<IProduct> products,
        ProductQuery? query = null,
        ICategory? category = null,
        CancellationToken ct = default);
}
