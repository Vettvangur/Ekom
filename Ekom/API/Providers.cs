using Ekom.Cache;
using Ekom.Interfaces;
using Ekom.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Composing;
using Umbraco.Core.Logging;

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
        public static Providers Instance => Current.Factory.GetInstance<Providers>();

        readonly Configuration _config;
        readonly ILogger _logger;
        readonly IPerStoreCache<IShippingProvider> _shippingProviderCache;
        readonly IPerStoreCache<IPaymentProvider> _paymentProviderCache;

        readonly IStoreService _storeSvc;
        readonly ICountriesRepository _countryRepo;

        /// <summary>
        /// ctor
        /// </summary>
        internal Providers(
            Configuration config,
            ILogger logger,
            IPerStoreCache<IShippingProvider> shippingProviderCache,
            IPerStoreCache<IPaymentProvider> paymentProviderCache,
            IStoreService storeService,
            ICountriesRepository countryRepo
        )
        {
            _config = config;
            _shippingProviderCache = shippingProviderCache;
            _paymentProviderCache = paymentProviderCache;
            _storeSvc = storeService;
            _countryRepo = countryRepo;
            _logger = logger;
        }

        /// <summary>
        /// Get all shipping providers, using the store from request cache.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IShippingProvider> GetShippingProviders()
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
        public IEnumerable<IShippingProvider> GetShippingProviders(
            IStore store = null,
            string countryCode = null,
            decimal orderAmount = 0
        )
        {
            return GetProviders(
                storeAlias => _shippingProviderCache[storeAlias].Select(x => x.Value).ToList(),
                store,
                countryCode,
                orderAmount
            ).Cast<ShippingProvider>();
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
            IStore store = null,
            string countryCode = null,
            decimal orderAmount = 0
        )
        {
            return GetProviders(
                storeAlias => _paymentProviderCache[storeAlias].Select(x => x.Value).ToList(),
                store,
                countryCode,
                orderAmount
            ).Cast<PaymentProvider>();
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
            IStore store = null,
            string countryCode = null,
            decimal orderAmount = 0
        )
        {
            if (store == null)
            {
                store = _storeSvc.GetStoreFromCache();
            }

            var providers = cacheFunc(store.Alias);

            if (countryCode != null)
            {
                providers = providers
                    .Where(x => x.Constraints.CountriesInZone.Contains(countryCode.ToUpper()));
            }
            if (orderAmount != 0)
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

            return _shippingProviderCache[store.Alias][key];
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

            return _paymentProviderCache[store.Alias][key];
        }
    }
}
