using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using uWebshop.Cache;
using uWebshop.Domain.Repositories;
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
        ICountriesRepository _countryRepo;

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="cache"></param>
        /// <param name="storeSvc"></param>
        /// <param name="countryRepo"></param>
        public Providers(IPerStoreCache<ShippingProvider> cache, IStoreService storeSvc, ICountriesRepository countryRepo)
        {
            _cache = cache;
            _storeSvc = storeSvc;
            _countryRepo = countryRepo;
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
            Models.Store store = null,
            string countryCode = null,
            decimal orderAmount = 0
        )
        {
            if (store == null)
            {
                store = _storeSvc.GetStoreFromCache();
            }

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

        /// <summary>
        /// List of all <see cref="Country"/> objects from xml document or .NET as fallback.
        /// </summary>
        public List<Country> GetAllCountries()
        {
            return _countryRepo.GetAllCountries();
        }
    }
}
