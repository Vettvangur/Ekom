using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using uWebshop.Cache;
using uWebshop.Interfaces;
using uWebshop.Models;

namespace uWebshop.API
{
    /// <summary>
    /// Providers API, returns shipping providers and payment providers. 
    /// </summary>
    public class Providers
    {
        private static Providers _current;
        /// <summary>
        /// Providers Singleton
        /// </summary>
        public static Providers Current
        {
            get
            {
                return _current ?? (_current = Configuration.container.GetService<Providers>());
            }
        }

        IPerStoreCache<ShippingProvider> _cache;
        IStoreService _storeSvc;

        public Providers(IPerStoreCache<ShippingProvider> cache, IStoreService storeSvc)
        {
            _cache = cache;
            _storeSvc = storeSvc;
        }

        /// <summary>
        /// Get all shipping providers, using the store from request cache.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<ShippingProvider> GetShippingProviders()
        {
            var store = _storeSvc.GetStoreFromCache();

            return GetShippingProviders(store);
        }

        /// <summary>
        /// Get shipping providers, optionally filter on country code and amount.
        /// </summary>
        /// <param name="store"></param>
        /// <param name="countryCode">
        /// Only show <see cref="ShippingProvider"/> in a zone that contains the given country code.
        /// </param>
        /// <param name="orderAmount">
        /// Only show <see cref="ShippingProvider"/> where the given amount falls within their range.
        /// </param>
        /// <returns></returns>
        public IEnumerable<ShippingProvider> GetShippingProviders(
            Models.Store store,
            string countryCode = null,
            decimal orderAmount = 0
        )
        {
            var shippingProviders = _cache.Cache[store.Alias].Select(x => x.Value);

            if (countryCode != null)
            {
                shippingProviders = shippingProviders
                    .Where(x => x.CountriesInZone.Contains(countryCode.ToUpper()));
            }
            if (orderAmount != 0)
            {
                shippingProviders = shippingProviders
                    .Where(x =>
                        x.StartRange <= orderAmount &&
                        (x.EndRange == 0 || x.EndRange >= orderAmount)
                    );
            }

            return shippingProviders;
        }

        /// <summary>
        /// Determine if the given shipping provider is valid given the provided properties.
        /// </summary>
        /// <param name="shippingProvider"></param>
        /// <param name="countryCode"></param>
        /// <param name="orderAmount"></param>
        /// <returns></returns>
        public bool IsValid(
            ShippingProvider shippingProvider,
            string countryCode,
            decimal orderAmount
        )
        {
            return
                shippingProvider.CountriesInZone.Contains(countryCode.ToUpper()) &&
                shippingProvider.StartRange <= orderAmount &&
                shippingProvider.EndRange >= orderAmount
            ;
        }
    }
}
