using CommonServiceLocator;
using Ekom.Cache;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Models.Discounts;
using Ekom.Utilities;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Umbraco.Core.Models;

namespace Ekom
{
    /// <summary>
    /// Controls configuration of Ekom
    /// </summary>
    public class Configuration
    {
        private static Configuration _current;
        /// <summary>
        /// Configuration Singleton
        /// </summary>
        public static Configuration Current
        {
            get
            {
                return _current ?? (_current = container.GetInstance<Configuration>());
            }
        }

        /// <summary>
        /// Current dependency resolver instance
        /// </summary>
        internal static IServiceLocator container;

        /// <summary>
        /// ekmPerStoreStock
        /// Controls which stock cache to will be used. Per store or per Product/Variant.
        /// </summary>
        public bool PerStoreStock
        {
            get
            {
                var value = ConfigurationManager.AppSettings["ekmPerStoreStock"];

                return value.ConvertToBool();
            }
        }

        /// <summary>
        /// ekmExamineSearcher
        /// Overrides the default of ExternalSearcher
        /// </summary>
        public virtual string ExamineSearcher
        {
            get
            {
                var value = ConfigurationManager.AppSettings["ekmExamineSearcher"];

                return value ?? "ExternalSearcher";
            }
        }

        /// <summary>
        /// ekmShareBasket
        /// </summary>
        public virtual bool ShareBasketBetweenStores
        {
            get
            {
                var value = ConfigurationManager.AppSettings["ekmShareBasket"];

                return value.ConvertToBool();
            }
        }

        /// <summary>
        /// Numeric format string to use for currency
        /// https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-numeric-format-strings
        /// </summary>
        public virtual string CurrencyFormat
        {
            get
            {
                var value = ConfigurationManager.AppSettings["ekmCurrencyFormat"];

                return value ?? "C";
            }
        }

        public virtual bool CustomOrderPrice
        {
            get
            {
                var value = ConfigurationManager.AppSettings["ekmCustomOrderPrice"];

                return value.ConvertToBool();
            }
        }

        /// <summary>
        /// ekmVirtualContent
        /// Allows for configuration of content nodes to use for matching all requests
        /// Use case: Data populated from Navision, Ekom used as in memory cache with no backing umbraco nodes.
        /// </summary>
        public virtual bool VirtualContent
        {
            get
            {
                var value = ConfigurationManager.AppSettings["ekmVirtualContent"];

                return value.ConvertToBool();
            }
        }

        /// <summary>
        /// ekmCategoryRootLevel
        /// Umbraco level value. Minimum level value for categories in umbraco hierarchy.
        /// </summary>
        public virtual int CategoryRootLevel
        {
            get
            {
                return int.Parse(ConfigurationManager.AppSettings["ekmCategoryRootLevel"] ?? "3");
            }
        }

        /// <summary>
        /// Time we give the user to complete checkout.
        /// Give value in minutes when overriding default
        /// </summary>
        public virtual TimeSpan ReservationTimeout
            => TimeSpan.FromMinutes(double.Parse(ConfigurationManager.AppSettings["ekmReservationTimeout"] ?? "30"));

        /// <summary>
        /// Should Ekom create a ekmCustomerData table and use it to store customer + order data 
        /// submitted to the checkout controller?
        /// </summary>
        public virtual bool StoreCustomerData
            => ConfigurationManager.AppSettings["ekmCustomerData"].ConvertToBool();

        /// <summary>
        /// Lists in initialization order all caches and the document type alias of
        /// the object they cache.
        /// This object is lazy initialized to make sure that all types have been registered with IoC container
        /// before we attempt to resolve.
        /// </summary>
        internal virtual Lazy<List<ICache>> CacheList { get; } = new Lazy<List<ICache>>(() =>
        {
            return new List<ICache>
            {
                { container.GetInstance<IBaseCache<IDomain>>() },
                { container.GetInstance<IBaseCache<IStore>>() },
                { container.GetInstance<IPerStoreCache<ICategory>>() },
                { container.GetInstance<IPerStoreCache<IProduct>>() },
                { container.GetInstance<IPerStoreCache<IVariant>>() },
                { container.GetInstance<IPerStoreCache<IVariantGroup>>() },
                { container.GetInstance<IBaseCache<IZone>>() },
                { container.GetInstance<IPerStoreCache<IPaymentProvider>>() },
                { container.GetInstance<IPerStoreCache<IShippingProvider>>() },
                { container.GetInstance<IPerStoreCache<IDiscount>>() },
            };
        });

        /// <summary> 
        /// Returns all <see cref="ICache"/> in the sequence succeeding the given cache 
        /// </summary> 
        internal IEnumerable<ICache> Succeeding(ICache cache)
        {
            var indexOf = CacheList.Value.FindIndex(x => x == cache);

            return CacheList.Value.Skip(indexOf + 1);
        }

        internal const string DiscountStockTableName = "EkomDiscountStock";
    }
}
