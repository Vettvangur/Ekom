
using Ekom.Utilities;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Linq;

#if NETCOREAPP
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
#else
using System.Web;
#endif

namespace Ekom.Models
{
    /// <summary>
    /// F.x. Borgun/Valitor
    /// </summary>
    public class PaymentProvider : PerStoreNodeEntity, IPerStoreNodeEntity, IPaymentProvider
    {
        /// <summary>
        /// 
        /// </summary>
        public virtual string Name => Properties["nodeName"];

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
#if NETCOREAPP
                var httpContext = Configuration.Resolver.GetService<IHttpContextAccessor>().HttpContext;

                if (httpContext?.Request != null)
                {
                    var cookie = httpContext.Request.Cookies["EkomCurrency-" + Store.Alias];

                    if (cookie != null && !string.IsNullOrEmpty(cookie))
                    {
                        var price = Prices.FirstOrDefault(x => x.Currency.CurrencyValue == cookie);
#else
                var httpContext = Configuration.Resolver.GetService<HttpContextBase>();

                if (httpContext != null)
                {
                    var cookie = httpContext.Request.Cookies["EkomCurrency-" + Store.Alias];

                    if (cookie != null && !string.IsNullOrEmpty(cookie.Value))
                    {
                        var price = Prices.FirstOrDefault(x => x.Currency.CurrencyValue == cookie.Value);
#endif
                        if (price != null)
                        {
                            return price;
                        }
                    }

                }

                return Prices.FirstOrDefault();

            }
        }

        /// <summary>
        /// 
        /// </summary>
        public virtual List<IPrice> Prices
        {
            get
            {
                var Prices = Properties.GetPropertyValue("price", Store.Alias).GetPriceValues(Store.Currencies, Store.Vat, Store.VatIncludedInPrice, Store.Currency);

                return Prices;
            }
        }

        /// <summary>
        /// Used by Ekom extensions
        /// </summary>
        /// <param name="store"></param>
        internal protected PaymentProvider(IStore store) : base(store) { }

        /// <summary>
        /// Construct PaymentProvider
        /// </summary>
        /// <param name="item"></param>
        /// <param name="store"></param>
        internal protected PaymentProvider(UmbracoContent item, IStore store) : base(item, store)
        {
            Constraints = new Constraints(this);
        }
    }
}
