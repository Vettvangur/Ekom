using Ekom.Cache;
using Ekom.Interfaces;
using Ekom.Utilities;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Umbraco.Core;
using UmbracoCurrent = Umbraco.Core.Composing.Current;

namespace Ekom
{
    /// <summary>
    /// Controls configuration of Ekom
    /// </summary>
    public class Configuration
    {
        /// <summary>
        /// Configuration Singleton
        /// </summary>
        public static Configuration Current => UmbracoCurrent.Factory.GetInstance<Configuration>();

        /// <summary>
        /// Ekom.PerStoreStock
        /// Controls which stock cache to will be used. Per store or per Product/Variant.
        /// </summary>
        public virtual bool PerStoreStock
        {
            get
            {
                var value = ConfigurationManager.AppSettings["Ekom.PerStoreStock"];

                return value.ConvertToBool();
            }
        }

        /// <summary>
        /// Ekom.ExamineIndex
        /// Overrides the default of ExternalSearcher
        /// </summary>
        public virtual string ExamineIndex
        {
            get
            {
                var value = ConfigurationManager.AppSettings["Ekom.ExamineIndex"];

                return value ?? "ExternalIndex";
            }
        }

        /// <summary>
        /// Ekom.ShareBasket
        /// </summary>
        public virtual bool ShareBasketBetweenStores
        {
            get
            {
                var value = ConfigurationManager.AppSettings["Ekom.ShareBasket"];

                return value.ConvertToBool();
            }
        }

        /// <summary>
        /// Ekom.BasketCookieLifetime
        /// Set how many days the order cookie should live, Default 1 day
        /// </summary>
        public virtual double BasketCookieLifetime
        {
            get
            {
                var value = ConfigurationManager.AppSettings["Ekom.BasketCookieLifetime"];

                double _value = 1;

                if (double.TryParse(value, out double __value))
                {
                    _value = __value;
                }

                return _value;
            }
        }

        /// <summary>
        /// Ekom.CustomImage
        /// Overrides the default of images
        /// </summary>
        public virtual string CustomImage
        {
            get
            {
                var value = ConfigurationManager.AppSettings["Ekom.CustomImage"];

                return value ?? "images";
            }
        }

        /// <summary>
        /// Ekom.VirtualContent
        /// Allows for configuration of content nodes to use for matching all requests
        /// Use case: Data populated from Navision, Ekom used as in memory cache with no backing umbraco nodes.
        /// </summary>
        public virtual bool VirtualContent
        {
            get
            {
                var value = ConfigurationManager.AppSettings["Ekom.VirtualContent"];

                return value.ConvertToBool();
            }
        }

        /// <summary>
        /// Ekom.CategoryRootLevel
        /// Umbraco level value. Minimum level value for categories in umbraco hierarchy.
        /// </summary>
        public virtual int CategoryRootLevel
        {
            get
            {
                return int.Parse(ConfigurationManager.AppSettings["Ekom.CategoryRootLevel"] ?? "3");
            }
        }

        /// <summary>
        /// Time we give the user to complete checkout.
        /// Give value in minutes when overriding default
        /// </summary>
        public virtual TimeSpan ReservationTimeout
            => TimeSpan.FromMinutes(double.Parse(ConfigurationManager.AppSettings["Ekom.ReservationTimeout"] ?? "30"));

        /// <summary>
        /// Should Ekom create a ekmCustomerData table and use it to store customer + order data 
        /// submitted to the checkout controller?
        /// </summary>
        public virtual bool StoreCustomerData
            => ConfigurationManager.AppSettings["Ekom.CustomerData"].ConvertToBool();

        /// <summary>
        /// Should Ekom create a ekmCustomerData table and use it to store customer + order data 
        /// submitted to the checkout controller?
        /// </summary>
        public virtual Rounding VatCalculationRounding
        {
            get
            {
                var configVal = ConfigurationManager.AppSettings["Ekom.VatCalcRounding"];

                if (!Enum.TryParse(configVal, out Rounding preferredRounding))
                {
                    // Default
                    preferredRounding = Rounding.RoundUp;
                }

                return preferredRounding;
            }
        }

        /// <summary>
        /// Ekom.UserBasket
        /// Single basket per user, not saved in session or cookie, only on the member under "orderId".
        /// </summary>
        public virtual bool UserBasket
        {
            get
            {
                var value = ConfigurationManager.AppSettings["Ekom.UserBasket"];

                return value.ConvertToBool();
            }
        }

        /// <summary>
        /// Lists in initialization order all caches and the document type alias of
        /// the object they cache.
        /// This object is lazy initialized to make sure that all types have been registered with IoC container
        /// before we attempt to resolve.
        /// 
        /// The order in this list is important as addition and removal from caches triggers updates on succeeding caches.
        /// </summary>
        internal virtual Lazy<List<ICache>> CacheList { get; } = new Lazy<List<ICache>>(()
            => new List<ICache>
            {
                { UmbracoCurrent.Factory.GetInstance<IStoreDomainCache>() },
                { UmbracoCurrent.Factory.GetInstance<IBaseCache<IStore>>() },
                { UmbracoCurrent.Factory.GetInstance<IPerStoreCache<ICategory>>() },
                { UmbracoCurrent.Factory.GetInstance<IPerStoreCache<IProductDiscount>>() },
                { UmbracoCurrent.Factory.GetInstance<IPerStoreCache<IProduct>>() },
                { UmbracoCurrent.Factory.GetInstance<IPerStoreCache<IVariant>>() },
                { UmbracoCurrent.Factory.GetInstance<IPerStoreCache<IVariantGroup>>() },
                { UmbracoCurrent.Factory.GetInstance<IBaseCache<IZone>>() },
                { UmbracoCurrent.Factory.GetInstance<IPerStoreCache<IPaymentProvider>>() },
                { UmbracoCurrent.Factory.GetInstance<IPerStoreCache<IShippingProvider>>() },
                { UmbracoCurrent.Factory.GetInstance<IPerStoreCache<IDiscount>>() },
            }
        );

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
