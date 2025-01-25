using Ekom.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace Ekom.Utilities;

static class CookieHelper
{
    public static CurrencyModel? GetCurrencyCookieValue(List<CurrencyModel> currencies, string storeAlias)
    {
        var httpContext = Configuration.Resolver.GetService<IHttpContextAccessor>()?.HttpContext;
        var cookie = httpContext?.Request?.Cookies["EkomCurrency-" + storeAlias];

        var culture = httpContext?.Request.HttpContext.Features.Get<IRequestCultureFeature>()?.RequestCulture.Culture;

        if (!string.IsNullOrEmpty(cookie))
        {
            var c = currencies.FirstOrDefault(x => x.CurrencyValue == cookie);

            if (c != null)
            {
                return c;
            }
        }

        //if (culture != null)
        //{
        //    var price = currencies.FirstOrDefault(x => x.CurrencyValue == culture.Name);

        //    if (price != null)
        //    {
        //        return price;
        //    }
        //}

        return currencies.FirstOrDefault();
    }
    public static IPrice? GetCurrencyPriceCookieValue(IEnumerable<IPrice> prices, string storeAlias)
    {
        var httpContext = Configuration.Resolver.GetService<IHttpContextAccessor>()?.HttpContext;
        var cookie = httpContext?.Request?.Cookies["EkomCurrency-" + storeAlias];

        if (!string.IsNullOrEmpty(cookie))
        {
            return prices.FirstOrDefault(x => x.Currency.CurrencyValue == cookie)
                ?? prices.FirstOrDefault();
        }

        var culture = httpContext?.Request.HttpContext.Features.Get<IRequestCultureFeature>()?.RequestCulture.Culture;

        if (culture != null)
        {
            var price = prices.FirstOrDefault(x => x.Currency.CurrencyValue == culture.Name);

            if (price != null)
            {
                return price;
            }
        }

        return prices.FirstOrDefault();
    }

    public static void SetUmbracoDomain(IResponseCookies cookieCollection, Uri uri)
        => cookieCollection.Append(Configuration.Cookie_UmbracoDomain, uri.ToString());

    public static Uri? GetUmbracoDomain(IRequestCookieCollection cookieCollection)
    {
        var umbracoDomain = cookieCollection[Configuration.Cookie_UmbracoDomain];
        Uri.TryCreate(umbracoDomain, UriKind.Absolute, out var uri);

        return uri;
    }
}
