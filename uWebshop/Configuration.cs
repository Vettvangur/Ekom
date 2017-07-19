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
        /// <summary>
        /// ctor
        /// </summary>
        public Configuration()
        {
            var container = UnityConfig.GetConfiguredContainer();
            CacheList = new List<CacheEntry>
            {
                new CacheEntry
                {
                    Cache = container.Resolve<IBaseCache<IDomain>>(),
                },
                new CacheEntry
                {
                    DocumentTypeAlias = "uwbsStore",
                    Cache = container.Resolve<IBaseCache<Store>>(),
                },
                new CacheEntry
                {
                    DocumentTypeAlias = "uwbsProductVariant",
                    Cache = container.Resolve<IPerStoreCache<Variant>>(),
                },
                new CacheEntry
                {
                    DocumentTypeAlias = "uwbsProductVariantGroup",
                    Cache = container.Resolve<IPerStoreCache<VariantGroup>>(),
                },
                new CacheEntry
                {
                    DocumentTypeAlias = "uwbsCategory",
                    Cache = container.Resolve<IPerStoreCache<Category>>(),
                },
                new CacheEntry
                {
                    DocumentTypeAlias = "uwbsProduct",
                    Cache = container.Resolve<IPerStoreCache<Product>>(),
                },
                new CacheEntry
                {
                    DocumentTypeAlias = "uwbsZone",
                    Cache = container.Resolve<IBaseCache<Zone>>(),
                },
                new CacheEntry
                {
                    DocumentTypeAlias = "uwbsPaymentProvider",
                    Cache = container.Resolve<IBaseCache<PaymentProvider>>(),
                },
            };
        }

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
        /// Lists in initialization order all caches and the document type alias of
        /// the object they cache.
        /// </summary>
        internal virtual List<CacheEntry> CacheList { get; private set; }
    }

    /// <summary>
    /// A single cache entry, with the umbraco document type alias of the object they cache,
    /// and the current instance of said cache.
    /// </summary>
    public class CacheEntry
    {
        public string DocumentTypeAlias { get; set; }

        public ICache Cache { get; set; }
    }
}
