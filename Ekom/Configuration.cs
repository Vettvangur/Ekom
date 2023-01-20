using Ekom.Cache;
using Ekom.Models;
using Ekom.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
namespace Ekom
{
    /// <summary>
    /// Controls configuration of Ekom
    /// </summary>
    public class Configuration
    {
        readonly IConfiguration _configuration;

        public Configuration(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public static IServiceProvider Resolver { get; internal set; }

        /// <summary>
        /// This IServiceProvider is a root scope provider,
        /// that means any services and their dependencies can only be singletons/transients.
        /// If any services resolved with this root scope provider require scoped lifetime services,
        /// get yourself an HttpContext (with an accessor) and RequestServices.GetService them.
        /// </summary>
        public static Configuration Instance => Resolver.GetService<Configuration>();

        internal const string Cookie_UmbracoDomain = "EkomUmbracoDomain";

        /// <summary>
        /// Ekom:PerStoreStock
        /// Controls which stock cache to will be used. Per store or per Product/Variant.
        /// </summary>
        public virtual bool PerStoreStock
        {
            get
            {
                var value = _configuration["Ekom:PerStoreStock"];

                return value.ConvertToBool();
            }
        }

        /// <summary>
        /// Ekom:ExamineIndex
        /// Overrides the default of ExternalSearcher
        /// </summary>
        public virtual string ExamineIndex
        {
            get
            {
                var value = _configuration["Ekom:ExamineIndex"];

                return value ?? "ExternalIndex";
            }
        }

        /// <summary>
        /// Umbraco:CMS:RequestHandler:CharCollection
        /// Gets the Umbraco CharCollection
        /// </summary>
        public virtual List<CharCollection> CharCollections
        {
            get
            {
#if NETCOREAPP
                var value = _configuration.GetSection("Umbraco:CMS:RequestHandler:CharCollection").Get<List<CharCollection>>();
#else
                var value =  new List<CharCollection>();
#endif
                return value;
            }
        }

        public class CharCollection
        {
            public string Char { get; set; }
            public string Replacement { get; set; }
        }

        /// <summary>
        /// Ekom:CustomIndex
        /// This will create an custom examine index that can be used for faster lookup or search named EkomIndex
        /// </summary>
        public virtual bool CustomIndex
        {
            get
            {
                var value = _configuration["Ekom:CustomIndex"];

                return value.ConvertToBool();
            }
        }

        /// <summary>
        /// Ekom:ShareBasket
        /// This will allow baskets to be shared between stores but we aware that it requires the same currencies to be available cross stores or it will break down.
        /// </summary>
        public virtual bool ShareBasketBetweenStores
        {
            get
            {
                var value = _configuration["Ekom:ShareBasket"];

                return value.ConvertToBool();
            }
        }

        /// <summary>
        /// Ekom:BasketCookieLifetime
        /// Set how many days the order cookie should live, Default 1 day
        /// </summary>
        public virtual double BasketCookieLifetime
        {
            get
            {
                var value = _configuration["Ekom:BasketCookieLifetime"];

                double _value = 1;

                if (double.TryParse(value, out double __value))
                {
                    _value = __value;
                }

                return _value;
            }
        }

        /// <summary>
        /// Ekom:CustomImage
        /// Overrides the default of images
        /// </summary>
        public virtual string CustomImage
        {
            get
            {
                var value = _configuration["Ekom:CustomImage"];

                return value ?? "images";
            }
        }

        /// <summary>
        /// Ekom:ExamineRebuild
        /// Default is false, if true on startup we will check if examine is empty and rebuild if so.
        /// </summary>
        public virtual bool ExamineRebuild
        {
            get
            {
                var value = _configuration["Ekom:ExamineRebuild"];

                return value.ConvertToBool();
            }
        }

        /// <summary>
        /// Ekom:VirtualContent
        /// Allows for configuration of content nodes to use for matching all requests
        /// Use case: Data populated from Navision, Ekom used as in memory cache with no backing umbraco nodes.
        /// </summary>
        public virtual bool VirtualContent
        {
            get
            {
                var value = _configuration["Ekom:VirtualContent"];

                return value.ConvertToBool();
            }
        }

        /// <summary>
        /// Ekom:CategoryRootLevel
        /// Umbraco level value. Minimum level value for categories in umbraco hierarchy.
        /// </summary>
        public virtual int CategoryRootLevel
        {
            get
            {
                return int.Parse(_configuration["Ekom:CategoryRootLevel"] ?? "3");
            }
        }

        /// <summary>
        /// Time we give the user to complete checkout.
        /// Give value in minutes when overriding default
        /// </summary>
        public virtual TimeSpan ReservationTimeout
            => TimeSpan.FromMinutes(double.Parse(_configuration["Ekom:ReservationTimeout"] ?? "30"));

        /// <summary>
        /// Should Ekom create a ekmCustomerData table and use it to store customer + order data 
        /// submitted to the checkout controller?
        /// </summary>
        public virtual bool StoreCustomerData
            => _configuration["Ekom:CustomerData"].ConvertToBool();

        /// <summary>
        /// </summary>
        public virtual Rounding VatCalculationRounding
        {
            get
            {
                var configVal = _configuration["Ekom:VatCalcRounding"];

                if (!Enum.TryParse(configVal, out Rounding preferredRounding))
                {
                    // Default
                    preferredRounding = Rounding.RoundUp;
                }

                return preferredRounding;
            }
        }

        /// <summary>
        /// Perform rounding on <see cref="IOrderInfo"/> totals. A common configuration with Navision.
        /// </summary>
        public virtual Rounding OrderVatCalculationRounding
        {
            get
            {
                var configVal = _configuration["Ekom:OrderVatCalcRounding"];

                if (!Enum.TryParse(configVal, out Rounding preferredRounding))
                {
                    // Default
                    preferredRounding = Rounding.None;
                }

                return preferredRounding;
            }
        }

        /// <summary>
        /// Ekom:UserBasket
        /// Single basket per user, not saved in session or cookie, only on the member under "orderId".
        /// </summary>
        public virtual bool UserBasket
        {
            get
            {
                var value = _configuration["Ekom:UserBasket"];

                return value.ConvertToBool();
            }
        }

        /// <summary>
        /// Used by MailService, defaults to umbracoSettings.config configured email
        /// </summary>
        public virtual string EmailNotifications
            => _configuration["Ekom:EmailNotifications"];

        /// <summary>
        /// </summary>
        public virtual bool DisableStock
            => _configuration["Ekom:DisableStock"].ConvertToBool();

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
                { Resolver.GetService<IStoreDomainCache>() },
                { Resolver.GetService<IBaseCache<IStore>>() },
                { Resolver.GetService<IPerStoreCache<ICategory>>() },
                { Resolver.GetService<IPerStoreCache<IProductDiscount>>() },
                { Resolver.GetService<IPerStoreCache<IProduct>>() },
                { Resolver.GetService<IPerStoreCache<IVariant>>() },
                { Resolver.GetService<IPerStoreCache<IVariantGroup>>() },
                { Resolver.GetService<IBaseCache<IZone>>() },
                { Resolver.GetService<IPerStoreCache<IPaymentProvider>>() },
                { Resolver.GetService<IPerStoreCache<IShippingProvider>>() },
                { Resolver.GetService<IPerStoreCache<IDiscount>>() },
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

        internal static readonly TimeSpan orderInfoCacheTime = TimeSpan.FromDays(1);

        /// <summary>
        /// Useful for Azure app services which sometimes do not include the correct currency symbol
        /// </summary>
        public static CultureInfo IsCultureInfo
        {
            get
            {
                var ci = new CultureInfo("is-IS");
                ci.NumberFormat.CurrencySymbol = "kr";
                return ci;
            }
        }

        public static object UmbracoCurrent { get; private set; }
    }
}
