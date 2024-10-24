using Ekom.Models;
using Ekom.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Ekom.Utilities;

public static class ProductExtensions
{
    public static IEnumerable<MetafieldGrouped> Filters(this IEnumerable<IProduct> products, bool filterable = true)
    {
        var ms = Configuration.Resolver.GetService<IMetafieldService>();

        return ms.Filters(products, filterable);
    }
    public static IEnumerable<IProduct> Filter(this IEnumerable<IProduct> products, ProductQuery query)
    {
        var ms = Configuration.Resolver.GetService<IMetafieldService>();

        return ms.FilterProducts(products, query);
    }
    public static string GetMetaFieldValue(this IProduct product, string alias, string culture = "")
    {
        var ms = Configuration.Resolver.GetService<IMetafieldService>();

        culture = string.IsNullOrEmpty(culture) ? System.Globalization.CultureInfo.CurrentCulture.Name : culture;

        return ms.GetMetaFieldValue(product, alias, culture);
    }
    public static IEnumerable<IProduct> Filter(this ProductResponse response, ProductQuery query)
    {
        var ms = Configuration.Resolver.GetService<IMetafieldService>();

        return ms.FilterProducts(response.Products, query);
    }
}
