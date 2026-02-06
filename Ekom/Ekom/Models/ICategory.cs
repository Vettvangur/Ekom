namespace Ekom.Models;

/// <summary>
/// Categories are groupings of products, categories can also be nested, f.x.
/// Women->Winter->Shirts
/// </summary>
public interface ICategory : INodeEntityWithUrl, IPerStoreNodeEntity
{
    /// <summary>
    /// All direct child products of category. (No descendants)
    /// </summary>
    ProductResponse Products(ProductQuery? query = null);

    /// <summary>
    /// All direct child products of category (async version).
    /// Use this when your IProductFilterService requires async operations.
    /// </summary>
    Task<ProductResponse> ProductsAsync(ProductQuery? query = null, CancellationToken cancellationToken = default)
        => Task.FromResult(Products(query));

    /// <summary>
    /// All descendant products of category, this includes child products of sub-categories
    /// </summary>
    ProductResponse ProductsRecursive(ProductQuery? query = null);

    /// <summary>
    /// All descendant products of category (async version).
    /// Use this when your IProductFilterService requires async operations.
    /// </summary>
    Task<ProductResponse> ProductsRecursiveAsync(ProductQuery? query = null, CancellationToken cancellationToken = default)
        => Task.FromResult(ProductsRecursive(query));

    /// <summary>
    /// Our eldest ancestor category
    /// </summary>
    ICategory RootCategory { get; }
    /// <summary>
    /// All direct child categories
    /// </summary>
    IEnumerable<ICategory> SubCategories { get; }
    /// <summary>
    /// All descendant categories, includes grandchild categories
    /// </summary>
    IEnumerable<ICategory> SubCategoriesRecursive { get; }

    /// <summary>
    /// All parent categories, grandparent categories and so on.
    /// </summary>
    /// <returns></returns>
    IEnumerable<ICategory> Ancestors { get; }

    IEnumerable<MetafieldGrouped> Filters(bool filterable = true);

    bool VirtualUrl { get; }

    bool HasProducts();

    /// <summary>
    /// Sets the stock buffer for the category, this value will be applied to all products within the category.
    /// If the product has its own stock buffer set, that value will take precedence.
    /// If the Product lives in multiple categories, the primary category's stock buffer will be used.
    /// </summary>
    /// <returns></returns>
    decimal? StockBuffer { get; }
}
