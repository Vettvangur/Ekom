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

    /// <summary>
    /// Apply filters using the store alias used to retrieve the products, independently of the query.
    /// Existing implementations fall back to the original overload.
    /// </summary>
    IEnumerable<IProduct> ApplyFilters(
        IEnumerable<IProduct> products,
        ProductQuery? query,
        ICategory? category,
        string storeAlias)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeAlias);
        return ApplyFilters(products, query, category);
    }

    /// <summary>
    /// Apply filters using the store alias used to retrieve the products, independently of the query.
    /// Existing implementations fall back to the original overload.
    /// </summary>
    Task<IEnumerable<IProduct>> ApplyFiltersAsync(
        IEnumerable<IProduct> products,
        ProductQuery? query,
        ICategory? category,
        string storeAlias,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeAlias);
        return ApplyFiltersAsync(products, query, category, ct);
    }
}
