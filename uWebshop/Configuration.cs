using Microsoft.Practices.Unity;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Models;
using uWebshop.App_Start;
using uWebshop.Cache;
using uWebshop.Models;
using uWebshop.Utilities;

namespace uWebshop
{
    /// <summary>
    /// Controls configuration of uWebshop
    /// </summary>
    public class Configuration
    {
        public virtual string ExamineSearcher
        {
            get
            {
                var value = ConfigurationManager.AppSettings["uwbsExamineSearcher"];

                return value ?? "ExternalSearcher";
            }
        }

        public virtual bool ShareBasketBetweenStores
        {
            get
            {
                var value = ConfigurationManager.AppSettings["uwbsShareBasket"];

                return value.ConvertToBool();
            }
        }

        public virtual string CurrencyFormat
        {
            get
            {
                var value = ConfigurationManager.AppSettings["uwbsCurrencyFormat"];

                return value ?? "C";
            }
        }

        public virtual bool CustomOrderPrice
        {
            get
            {
                var value = ConfigurationManager.AppSettings["uwbsCustomOrderPrice"];

                return value.ConvertToBool();
            }
        }

        /// <summary>
        /// Allows for configuration of content nodes to use for matching all requests
        /// Use case: Data populated from Navision, uWebshop used as in memory cache with no backing umbraco nodes.
        /// </summary>
        public virtual bool VirtualContent
        {
            get
            {
                var value = ConfigurationManager.AppSettings["uwbsVirtualContent"];

                return value.ConvertToBool();
            }
        }

        /// <summary>
        /// Umbraco level value. Minimum level value for categories in umbraco hierarchy.
        /// </summary>
        public virtual int CategoryRootLevel
        {
            get
            {
                return int.Parse(ConfigurationManager.AppSettings["uwbsCategoryRootLevel"] ?? "3");
            }
        }

        /// <summary>
        /// Lists in initialization order all caches and the document type alias of
        /// the object they cache.
        /// This object is lazy initialized to make sure that all types have been registered with IoC container
        /// before we attempt to resolve.
        /// </summary>
        internal virtual Lazy<List<ICache>> CacheList { get; private set; } = new Lazy<List<ICache>>(() =>
        {
            var container = UnityConfig.GetConfiguredContainer();

            return new List<ICache>
            {
                { container.Resolve<IBaseCache<IDomain>>() },
                { container.Resolve<IBaseCache<Store>>() },
                { container.Resolve<IPerStoreCache<Variant>>() },
                { container.Resolve<IPerStoreCache<VariantGroup>>() },
                { container.Resolve<IPerStoreCache<Category>>() },
                { container.Resolve<IPerStoreCache<Product>>() },
                { container.Resolve<IBaseCache<Zone>>() },
                { container.Resolve<IPerStoreCache<PaymentProvider>>() },
            };
        });

        /// <summary> 
        /// Returns all <see cref="ICache"/> in the sequence succeeding the given cache 
        /// </summary> 
        public IEnumerable<ICache> Succeeding(ICache cache)
        {

            var indexOf = CacheList.Value.FindIndex(x => x == cache);

            return CacheList.Value.Skip(indexOf + 1);
        }
    }
}
