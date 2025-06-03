using Ekom.Models;
using System.Globalization;

namespace Ekom.Events;

public static class CatalogEvents
{
    public static event EventHandler<CurrencyStringEventArgs> CurrencyStringFormat;
    internal static void OnCurrencyStringFormat(object sender, CurrencyStringEventArgs args)
        => CurrencyStringFormat?.Invoke(sender, args);

    public static event EventHandler<CategoryEventArgs>? BeforeReturnCategory;

    /// <summary>
    /// Triggers the BeforeReturnCategory event and allows the category to be modified or replaced.
    /// </summary>
    /// <param name="category">The original category.</param>
    /// <returns>The modified or original category, or null if the event handler set it to null.</returns>
    public static ICategory? RaiseOnBeforeReturnCategory(ICategory? category)
    {
        if (category == null) {
            return null;
        }

        if (BeforeReturnCategory == null)
        {
            return category;
        }

        var args = new CategoryEventArgs(category);
        BeforeReturnCategory.Invoke(null, args);
        return args.Category;
    }

    public static event EventHandler<CategoriesEventArgs>? BeforeReturnCategories;

    public static IEnumerable<ICategory> RaiseOnBeforeReturnCategories(IEnumerable<ICategory> categories)
    {
        if (BeforeReturnCategories == null)
        {
            return categories;
        }

        var args = new CategoriesEventArgs(categories);
        BeforeReturnCategories.Invoke(null, args);
        return args.Categories;
    }

    public static event EventHandler<ProductEventArgs>? BeforeReturnProduct;

    /// <summary>
    /// Triggers the BeforeReturnProduct event and allows the product to be modified or replaced.
    /// </summary>
    /// <param name="product">The original product.</param>
    /// <returns>The modified or original product, or null if the event handler set it to null.</returns>
    public static IProduct? RaiseOnBeforeReturnProduct(IProduct? product)
    {
        if (product == null)
        {
            return null;
        }

        if (BeforeReturnProduct == null)
        {
            return product;
        }

        var args = new ProductEventArgs(product);
        BeforeReturnProduct.Invoke(null, args);
        return args.Product;
    }

    public static event EventHandler<ProductsEventArgs>? BeforeReturnProducts;

    /// <summary>
    /// Triggers the BeforeReturnProducts event and allows the products to be modified or replaced.
    /// </summary>
    /// <param name="products">The original products.</param>
    /// <returns>The modified or original products, or null if the event handler set it to null.</returns>
    public static IEnumerable<IProduct> RaiseOnBeforeReturnProducts(IEnumerable<IProduct> products)
    {
        if (BeforeReturnProducts == null)
        {
            return products;
        }

        var args = new ProductsEventArgs(products);
        BeforeReturnProducts.Invoke(null, args);
        return args.Products;
    }

}
public class CurrencyStringEventArgs : EventArgs
{
    public CultureInfo CultureInfo { get; set; }
    public decimal Value { get; set; }
    public string ValueString { get; set; }
}

public class CategoryEventArgs : EventArgs
{
    public CategoryEventArgs(ICategory category)
    {
        Category = category;
    }

    /// <summary>
    /// The category to return. Can be replaced or set to null by event subscribers.
    /// </summary>
    public ICategory? Category { get; set; }
}
public class CategoriesEventArgs : EventArgs
{
    public CategoriesEventArgs(IEnumerable<ICategory> categories)
    {
        Categories = categories;
    }

    /// <summary>
    /// Can be replaced or filtered by event handlers.
    /// </summary>
    public IEnumerable<ICategory> Categories { get; set; }
}

public class ProductEventArgs : EventArgs
{
    public ProductEventArgs(IProduct product)
    {
        Product = product;
    }

    /// <summary>
    /// The product to return. Can be replaced or set to null by event subscribers.
    /// </summary>
    public IProduct? Product { get; set; }
}

public class ProductsEventArgs : EventArgs
{
    public ProductsEventArgs(IEnumerable<IProduct> products)
    {
        Products = products;
    }

    /// <summary>
    /// Can be replaced or filtered by event handlers.
    /// </summary>
    public IEnumerable<IProduct> Products { get; set; }
}

