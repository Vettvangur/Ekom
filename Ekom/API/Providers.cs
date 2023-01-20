using Ekom.Cache;
using Ekom.Models;
using Ekom.Services;
using Ekom.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ekom.API
{
    /// <summary>
    /// Providers API, returns shipping providers and payment providers. 
    /// </summary>
    public class Providers
    {
        /// <summary>
        /// Providers Instance
        /// </summary>
        public static Providers Instance => Configuration.Resolver.GetService<Providers>();

        readonly Configuration _config;
        readonly ILogger<Providers> _logger;
        readonly IPerStoreCache<IShippingProvider> _shippingProviderCache;
        readonly IPerStoreCache<IPaymentProvider> _paymentProviderCache;
        readonly IBaseCache<IZone> _zoneCache;
        readonly IStoreService _storeSvc;
        readonly CountriesRepository _countryRepo;

        /// <summary>
        /// ctor
        /// </summary>
        internal Providers(
            Configuration config,
            ILogger<Providers> logger,
            IPerStoreCache<IShippingProvider> shippingProviderCache,
            IPerStoreCache<IPaymentProvider> paymentProviderCache,
            IBaseCache<IZone> zoneCache,
            IStoreService storeService,
            CountriesRepository countryRepo
        )
        {
            _config = config;
            _shippingProviderCache = shippingProviderCache;
            _paymentProviderCache = paymentProviderCache;
            _zoneCache = zoneCache;
            _storeSvc = storeService;
            _countryRepo = countryRepo;
            _logger = logger;
        }

        /// <summary>
        /// Get all shipping providers
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IShippingProvider> GetShippingProviders()
        {
            return GetShippingProviders(null, null, 0);
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
        public IEnumerable<IShippingProvider> GetShippingProviders(
            string store = null,
            string countryCode = null,
            decimal orderAmount = 0
        )
        {
            return GetProviders(
                storeAlias => _shippingProviderCache[storeAlias].Select(x => x.Value).ToList(),
                store,
                countryCode,
                orderAmount
            ).Cast<IShippingProvider>();
        }


        /// <summary>
        /// Get payment providers, optionally filter on country code and amount.
        /// </summary>
        /// <param name="store"></param>
        /// <param name="countryCode">
        /// Only show <see cref="PaymentProvider"/> in a zone that contains the given country code.
        /// </param>
        /// <param name="orderAmount">
        /// Only show <see cref="PaymentProvider"/> where the given amount falls within their range.
        /// </param>
        /// <returns></returns>
        public IEnumerable<IPaymentProvider> GetPaymentProviders(
            string store = null,
            string countryCode = null,
            decimal orderAmount = 0
        )
        {
            return GetProviders(
                storeAlias => _paymentProviderCache[storeAlias].Select(x => x.Value).ToList(),
                store,
                countryCode,
                orderAmount
            ).Cast<IPaymentProvider>();
        }

        /// <summary>
        /// Common logic for get provider methods.
        /// </summary>
        /// <param name="cacheFunc"></param>
        /// <param name="store"></param>
        /// <param name="countryCode"></param>
        /// <param name="orderAmount"></param>
        /// <returns></returns>
        private IEnumerable<IConstrained> GetProviders(
            Func<string, IEnumerable<IConstrained>> cacheFunc,
            string store = null,
            string countryCode = null,
            decimal orderAmount = 0
        )
        {
            if (string.IsNullOrEmpty(store))
            {
                store = _storeSvc.GetStoreFromCache().Alias;
            }

            var providers = cacheFunc(store);

            if (!string.IsNullOrEmpty(countryCode) && countryCode.Length == 2)
            {
                providers = providers
                    .Where(x => x.Constraints.CountriesInZone.Any() && x.Constraints.CountriesInZone.Contains(countryCode.ToUpper()));
            }


            if (orderAmount > 0)
            {
                providers = providers
                    .Where(x =>
                        x.Constraints.StartRange <= orderAmount &&
                        (x.Constraints.EndRange == 0 || x.Constraints.EndRange >= orderAmount)
                    );
            }

            return providers;
        }

        /// <summary>
        /// List of all <see cref="Country"/> objects from xml document or .NET as fallback.
        /// </summary>
        public List<Country> GetAllCountries() => _countryRepo.GetAllCountries();

        /// <summary>
        /// Get shipping provider from cache
        /// </summary>
        /// <param name="key"></param>
        /// <param name="store"></param>
        /// <returns></returns>
        public IShippingProvider GetShippingProvider(Guid key, IStore store = null)
        {
            if (store == null)
            {
                store = _storeSvc.GetStoreFromCache();
            }

            return _shippingProviderCache[store.Alias].ContainsKey(key) ? _shippingProviderCache[store.Alias][key] : null;
        }

        /// <summary>
        /// Get payment provider from cache
        /// </summary>
        /// <param name="key"></param>
        /// <param name="store"></param>
        /// <returns></returns>
        public IPaymentProvider GetPaymentProvider(Guid key, IStore store = null)
        {
            if (store == null)
            {
                store = _storeSvc.GetStoreFromCache();
            }

            return _paymentProviderCache[store.Alias].ContainsKey(key) ? _paymentProviderCache[store.Alias][key] : null;
        }

        public IEnumerable<IZone> GetAllZones()
        {
            return _zoneCache.Cache.Select(x => x.Value);
        }
    }
}
