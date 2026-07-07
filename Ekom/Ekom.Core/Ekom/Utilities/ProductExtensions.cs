using Ekom.Models;
using Ekom.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Ekom.Utilities;

public static class ProductExtensions
{
    public static IEnumerable<MetafieldGrouped> Filters(this IEnumerable<IProduct> products, bool filterable = true)
    {
        var httpContext = Configuration.Resolver.GetService<IHttpContextAccessor>()?.HttpContext;
        if (httpContext != null)
        {
            return httpContext.RequestServices.GetRequiredService<IMetafieldService>().Filters(products, filterable);
        }

        using var scope = Configuration.Resolver.GetRequiredService<IServiceScopeFactory>().CreateScope();
        return scope.ServiceProvider.GetRequiredService<IMetafieldService>().Filters(products, filterable).ToList();
    }
    public static IEnumerable<IProduct> Filter(this IEnumerable<IProduct> products, ProductQuery query)
    {
        var httpContext = Configuration.Resolver.GetService<IHttpContextAccessor>()?.HttpContext;
        if (httpContext != null)
        {
            return httpContext.RequestServices.GetRequiredService<IMetafieldService>().FilterProducts(products, query);
        }

        using var scope = Configuration.Resolver.GetRequiredService<IServiceScopeFactory>().CreateScope();
        return scope.ServiceProvider.GetRequiredService<IMetafieldService>().FilterProducts(products, query).ToList();
    }
    public static string GetMetaFieldValue(this IProduct product, string alias, string culture = "")
    {
        culture = string.IsNullOrEmpty(culture) ? System.Globalization.CultureInfo.CurrentCulture.Name : culture;

        var httpContext = Configuration.Resolver.GetService<IHttpContextAccessor>()?.HttpContext;
        if (httpContext != null)
        {
            return httpContext.RequestServices.GetRequiredService<IMetafieldService>().GetMetaFieldValue(product, alias, culture);
        }

        using var scope = Configuration.Resolver.GetRequiredService<IServiceScopeFactory>().CreateScope();
        return scope.ServiceProvider.GetRequiredService<IMetafieldService>().GetMetaFieldValue(product, alias, culture);
    }
    public static IEnumerable<IProduct> Filter(this ProductResponse response, ProductQuery query)
    {
        var httpContext = Configuration.Resolver.GetService<IHttpContextAccessor>()?.HttpContext;
        if (httpContext != null)
        {
            return httpContext.RequestServices.GetRequiredService<IMetafieldService>().FilterProducts(response.Products, query);
        }

        using var scope = Configuration.Resolver.GetRequiredService<IServiceScopeFactory>().CreateScope();
        return scope.ServiceProvider.GetRequiredService<IMetafieldService>().FilterProducts(response.Products, query).ToList();
    }
}
