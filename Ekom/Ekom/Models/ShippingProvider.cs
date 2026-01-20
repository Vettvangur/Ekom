using Ekom.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Ekom.Models;

/// <summary>
/// F.x. home delivery or pickup.
/// </summary>
public class ShippingProvider : PerStoreNodeEntity, IShippingProvider
{
    //readonly HttpContext _httpCtx;

    //public ShippingProvider(IHttpContextAccessor httpContextAccessor)
    //{
    //    _httpCtx = httpContextAccessor.HttpContext;
    //}
    /// <summary>
    /// Ranges and zones
    /// </summary>
    public virtual IConstraints Constraints { get; }

    /// <summary>
    /// 
    /// </summary>
    public virtual IPrice Price
    {
        get
        {
            HttpContext? httpContext = Configuration.Resolver.GetService<IHttpContextAccessor>().HttpContext;

            if (httpContext?.Request != null)
            {
                string? cookie = httpContext.Request.Cookies["EkomCurrency-" + Store.Alias];

                if (cookie != null && !string.IsNullOrEmpty(cookie))
                {
                    IPrice? price = Prices.FirstOrDefault(x => x.Currency.CurrencyValue == cookie);

                    if (price != null)
                    {
                        return price;
                    }
                }
            }

            return Prices.FirstOrDefault();

        }
    }

    public virtual string Description => GetValue("description", Store.Alias);

    /// <summary>
    /// 
    /// </summary>
    public virtual List<IPrice> Prices
    {
        get
        {
            var priceJson = Properties.GetPropertyValue("price", Store.Alias) ?? string.Empty;

            List<IPrice> prices = PriceBuilder.BuildPricesSync(
                priceJson,
                Store.Currencies,
                Store.Vat,
                Store.VatIncludedInPrice,
                Store.Currency,
                Store.Alias,
                path: null,
                categories: null
            );

            return prices;
        }
    }

    /// <summary>
    /// Used by Ekom extensions
    /// </summary>
    /// <param name="store"></param>
    internal protected ShippingProvider(IStore store) : base(store) { }

    /// <summary>
    /// Construct ShippingProvider
    /// </summary>
    /// <param name="item"></param>
    /// <param name="store"></param>
    internal protected ShippingProvider(UmbracoContent item, IStore store) : base(item, store)
    {
        Constraints = new Constraints(this);
    }
}
