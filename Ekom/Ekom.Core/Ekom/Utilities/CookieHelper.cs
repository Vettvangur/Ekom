using Ekom.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace Ekom.Utilities;

static class CookieHelper
{
    public static CurrencyModel? GetCurrencyCookieValue(List<CurrencyModel> currencies, string storeAlias)
    {
        HttpContext? httpContext = Configuration.Resolver.GetService<IHttpContextAccessor>()?.HttpContext;
        string? cookie = httpContext?.Request?.Cookies["EkomCurrency-" + storeAlias];

        System.Globalization.CultureInfo? culture = httpContext?.Request.HttpContext.Features.Get<IRequestCultureFeature>()?.RequestCulture.Culture;

        if (!string.IsNullOrEmpty(cookie))
        {
            CurrencyModel? c = currencies.FirstOrDefault(x => x.CurrencyValue == cookie);

            if (c != null)
            {
                return c;
            }
        }

        if (culture != null)
        {
            var currency = currencies.FirstOrDefault(x => x.CurrencyValue == culture.Name);

            if (currency != null)
            {
                return currency;
            }
        }

        return currencies.FirstOrDefault();
    }
    public static IPrice? GetCurrencyPriceCookieValue(IEnumerable<IPrice> prices, string storeAlias)
    {
        HttpContext? httpContext = Configuration.Resolver.GetService<IHttpContextAccessor>()?.HttpContext;
        string? cookie = httpContext?.Request?.Cookies["EkomCurrency-" + storeAlias];

        if (!string.IsNullOrEmpty(cookie))
        {
            return prices.FirstOrDefault(x => x.Currency.CurrencyValue == cookie)
                ?? prices.FirstOrDefault();
        }

        System.Globalization.CultureInfo? culture = httpContext?.Request.HttpContext.Features.Get<IRequestCultureFeature>()?.RequestCulture.Culture;

        if (culture != null)
        {
            IPrice? price = prices.FirstOrDefault(x => x.Currency.CurrencyValue == culture.Name);

            if (price != null)
            {
                return price;
            }
        }

        return prices.FirstOrDefault();
    }

    public static void SetUmbracoDomain(IResponseCookies cookieCollection, Uri uri, bool isHttps)
        => cookieCollection.Append(
            Configuration.Cookie_UmbracoDomain,
            uri.ToString(),
            new CookieOptions
            {
                Path = "/",
                SameSite = SameSiteMode.Lax,
                Secure = isHttps,
                HttpOnly = false
            });

    public static Uri? GetUmbracoDomain(IRequestCookieCollection? cookieCollection)
    {
        if (cookieCollection == null)
        {
            return null;
        }

        if (!cookieCollection.TryGetValue(Configuration.Cookie_UmbracoDomain, out var umbracoDomain) || string.IsNullOrWhiteSpace(umbracoDomain))
        {
            return null;
        }

        return Uri.TryCreate(umbracoDomain, UriKind.Absolute, out var uri) ? uri : null;
    }
}
