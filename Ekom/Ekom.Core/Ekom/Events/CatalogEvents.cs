using Ekom.Models;
using System.Globalization;

namespace Ekom.Events;

public static class CatalogEvents
{
    // =========================================================
    // Currency formatting (kept sync; can be async-fied later)
    // =========================================================
    public static event EventHandler<CurrencyStringEventArgs>? CurrencyStringFormat;

    internal static void OnCurrencyStringFormat(object sender, CurrencyStringEventArgs args)
        => CurrencyStringFormat?.Invoke(sender, args);

    // =========================================================
    // CATEGORY (single)
    // =========================================================

    /// <summary>
    /// Async-first: register handlers here.
    /// </summary>
    public static event Func<CategoryEventArgs, CancellationToken, ValueTask>? BeforeReturnCategoryAsync;

    /// <summary>
    /// Legacy sync event (will be removed in next major).
    /// </summary>
    [Obsolete("Use BeforeReturnCategoryAsync")]
    public static event EventHandler<CategoryEventArgs>? BeforeReturnCategory;

    /// <summary>
    /// Sync raise bridges into async raise (runs async handlers + sync handlers).
    /// </summary>
    public static ICategory? RaiseOnBeforeReturnCategory(ICategory? category)
        => RaiseOnBeforeReturnCategoryAsync(category, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>
    /// Async raise runs async handlers first, then legacy sync handlers.
    /// </summary>
    public static async ValueTask<ICategory?> RaiseOnBeforeReturnCategoryAsync(ICategory? category, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (category == null)
            return null;

        var args = new CategoryEventArgs(category);

        await InvokeAsync(BeforeReturnCategoryAsync, args, ct);

#pragma warning disable CS0618
        BeforeReturnCategory?.Invoke(null, args);
#pragma warning restore CS0618

        return args.Category;
    }

    // =========================================================
    // CATEGORIES (many)
    // =========================================================

    public static event Func<CategoriesEventArgs, CancellationToken, ValueTask>? BeforeReturnCategoriesAsync;

    [Obsolete("Use BeforeReturnCategoriesAsync")]
    public static event EventHandler<CategoriesEventArgs>? BeforeReturnCategories;

    public static IEnumerable<ICategory> RaiseOnBeforeReturnCategories(IEnumerable<ICategory> categories)
        => RaiseOnBeforeReturnCategoriesAsync(categories, CancellationToken.None).GetAwaiter().GetResult();

    public static async ValueTask<IEnumerable<ICategory>> RaiseOnBeforeReturnCategoriesAsync(IEnumerable<ICategory> categories, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var args = new CategoriesEventArgs(categories);

        await InvokeAsync(BeforeReturnCategoriesAsync, args, ct);

#pragma warning disable CS0618
        BeforeReturnCategories?.Invoke(null, args);
#pragma warning restore CS0618

        return args.Categories;
    }

    // =========================================================
    // PRODUCT (single)
    // =========================================================

    public static event Func<ProductEventArgs, CancellationToken, ValueTask>? BeforeReturnProductAsync;

    [Obsolete("Use BeforeReturnProductAsync")]
    public static event EventHandler<ProductEventArgs>? BeforeReturnProduct;

    public static IProduct? RaiseOnBeforeReturnProduct(IProduct? product)
        => RaiseOnBeforeReturnProductAsync(product, CancellationToken.None).GetAwaiter().GetResult();

    public static async ValueTask<IProduct?> RaiseOnBeforeReturnProductAsync(IProduct? product, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (product == null)
            return null;

        var args = new ProductEventArgs(product);

        await InvokeAsync(BeforeReturnProductAsync, args, ct);

#pragma warning disable CS0618
        BeforeReturnProduct?.Invoke(null, args);
#pragma warning restore CS0618

        return args.Product;
    }

    // =========================================================
    // PRODUCTS
    // =========================================================

    public static event Func<ProductsEventArgs, CancellationToken, ValueTask>? BeforeReturnProductsAsync;

    [Obsolete("Use BeforeReturnProductsAsync")]
    public static event EventHandler<ProductsEventArgs>? BeforeReturnProducts;

    public static IEnumerable<IProduct> RaiseOnBeforeReturnProducts(IEnumerable<IProduct> products)
        => RaiseOnBeforeReturnProductsAsync(products, CancellationToken.None).GetAwaiter().GetResult();

    public static async ValueTask<IEnumerable<IProduct>> RaiseOnBeforeReturnProductsAsync(IEnumerable<IProduct> products, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var args = new ProductsEventArgs(products);

        await InvokeAsync(BeforeReturnProductsAsync, args, ct);

#pragma warning disable CS0618
        BeforeReturnProducts?.Invoke(null, args);
#pragma warning restore CS0618

        return args.Products;
    }

    // =========================================================
    // Helper: invoke async multicast delegates safely
    // =========================================================
    private static async ValueTask InvokeAsync<TArgs>(
        Func<TArgs, CancellationToken, ValueTask>? multicast,
        TArgs args,
        CancellationToken ct)
        where TArgs : EventArgs
    {
        if (multicast == null)
            return;

        foreach (var del in multicast.GetInvocationList())
        {
            ct.ThrowIfCancellationRequested();
            await ((Func<TArgs, CancellationToken, ValueTask>)del)(args, ct);
        }
    }
}

// =========================================================
// EventArgs
// =========================================================

public class CurrencyStringEventArgs : EventArgs
{
    public CultureInfo CultureInfo { get; set; } = CultureInfo.InvariantCulture;
    public decimal Value { get; set; }
    public string ValueString { get; set; } = "";
}

public class CategoryEventArgs : EventArgs
{
    public CategoryEventArgs(ICategory category) => Category = category;
    public ICategory? Category { get; set; }
}

public class CategoriesEventArgs : EventArgs
{
    public CategoriesEventArgs(IEnumerable<ICategory> categories) => Categories = categories;
    public IEnumerable<ICategory> Categories { get; set; }
}

public class ProductEventArgs : EventArgs
{
    public ProductEventArgs(IProduct product) => Product = product;
    public IProduct? Product { get; set; }
}

public class ProductsEventArgs : EventArgs
{
    public ProductsEventArgs(IEnumerable<IProduct> products) => Products = products;
    public IEnumerable<IProduct> Products { get; set; }
}
