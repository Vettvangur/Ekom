using Ekom.Models;
#if NETCOREAPP
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;

#else
using System.Web;
#endif
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Ekom.Utilities
{
    static class CookieHelper
    {
#if NETFRAMEWORK
        public static CurrencyModel GetCurrencyCookieValue(List<CurrencyModel> currencies, string storeAlias)
        {
            var httpContext = Configuration.Resolver.GetService<HttpContextBase>();
            var cookie = httpContext?.Request?.Cookies["EkomCurrency-" + storeAlias];

            if (!string.IsNullOrEmpty(cookie?.Value))
            {
                var c = currencies.FirstOrDefault(x => x.CurrencyValue == cookie.Value);

                if (c != null)
                {
                    return c;
                }
            }

            return currencies.FirstOrDefault();
        }
        public static IPrice GetCurrencyPriceCookieValue(IEnumerable<IPrice> prices, string storeAlias)
        {
            var httpContext = Configuration.Resolver.GetService<HttpContextBase>();
            var cookie = httpContext?.Request?.Cookies["EkomCurrency-" + storeAlias];

            if (!string.IsNullOrEmpty(cookie?.Value))
            {
                return prices.FirstOrDefault(x => x.Currency.CurrencyValue == cookie.Value)
                    ?? prices.FirstOrDefault();
            }

            return prices.FirstOrDefault();
        }

        public static void SetUmbracoDomain(System.Web.HttpCookieCollection cookieCollection, Uri uri)
            => cookieCollection[Configuration.Cookie_UmbracoDomain].Value = uri.ToString();

        public static Uri GetUmbracoDomain(System.Web.HttpCookieCollection cookieCollection)
        {
            var umbracoDomain = cookieCollection[Configuration.Cookie_UmbracoDomain];
            Uri.TryCreate(umbracoDomain?.Value, UriKind.Absolute, out var uri);

            return uri;
        }
#else
        public static CurrencyModel GetCurrencyCookieValue(List<CurrencyModel> currencies, string storeAlias)
        {
            var httpContext = Configuration.Resolver.GetService<IHttpContextAccessor>().HttpContext;
            var cookie = httpContext?.Request?.Cookies["EkomCurrency-" + storeAlias];
            
            if (!string.IsNullOrEmpty(cookie))
            {
                var c = currencies.FirstOrDefault(x => x.CurrencyValue == cookie);

                if (c != null)
                {
                    return c;
                }
            }

            return currencies.FirstOrDefault();
        }
        public static IPrice GetCurrencyPriceCookieValue(IEnumerable<IPrice> prices, string storeAlias)
        {
            var httpContext = Configuration.Resolver.GetService<IHttpContextAccessor>().HttpContext;
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

        public static Uri GetUmbracoDomain(IRequestCookieCollection cookieCollection)
        {
            var umbracoDomain = cookieCollection[Configuration.Cookie_UmbracoDomain];
            Uri.TryCreate(umbracoDomain, UriKind.Absolute, out var uri);

            return uri;
        }
#endif
    }
}
