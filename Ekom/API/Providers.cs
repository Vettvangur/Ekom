using Ekom.Cache;
using Ekom.Domain.Repositories;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Models.Behaviors;
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
        private static Providers _current;
        /// <summary>
        /// Providers Singleton
        /// </summary>
        public static Providers Current
        {
            get
            {
                return _current ?? (_current = Configuration.container.GetInstance<Providers>());
            }
        }

        IPerStoreCache<ShippingProvider> _shippingProviderCache;
        IPerStoreCache<PaymentProvider> _paymentProviderCache;
        IStoreService _storeSvc;
        ICountriesRepository _countryRepo;

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="paymentProviderCache"></param>
        /// <param name="shippingProviderCache"></param>
        /// <param name="storeSvc"></param>
        /// <param name="countryRepo"></param>
        public Providers(
            IPerStoreCache<ShippingProvider> shippingProviderCache,
            IPerStoreCache<PaymentProvider> paymentProviderCache,
            IStoreService storeSvc,
            ICountriesRepository countryRepo)
        {
            _shippingProviderCache = shippingProviderCache;
            _paymentProviderCache = paymentProviderCache;
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
        public IEnumerable<PaymentProvider> GetPaymentProviders(
            Models.Store store = null,
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
            Models.Store store = null,
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
        /// Determine if the given provider is valid given the provided properties.
        /// </summary>
        /// <param name="constraints"></param>
        /// <param name="countryCode"></param>
        /// <param name="orderAmount"></param>
        /// <returns></returns>
        public bool IsValid(
            Constraints constraints,
            string countryCode,
            decimal orderAmount
        )
        {
            return
                constraints.CountriesInZone.Contains(countryCode.ToUpper()) &&
                constraints.StartRange <= orderAmount &&
                constraints.EndRange >= orderAmount
            ;
        }

        /// <summary>
        /// List of all <see cref="Country"/> objects from xml document or .NET as fallback.
        /// </summary>
        public List<Country> GetAllCountries()
        {
            return _countryRepo.GetAllCountries();
        }

        /// <summary>
        /// Get shipping provider from cache
        /// </summary>
        /// <param name="key"></param>
        /// <param name="store"></param>
        /// <returns></returns>
        public ShippingProvider GetShippingProvider(Guid key, Models.Store store = null)
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
        public PaymentProvider GetPaymentProvider(Guid key, Models.Store store = null)
        {
            if (store == null)
            {
                store = _storeSvc.GetStoreFromCache();
            }

            return _paymentProviderCache[store.Alias][key];
        }
    }
}
