using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using uWebshop.Cache;

namespace uWebshop
{
    /// <summary>
    /// Contains dictionary mappings, constants and other static configuration data for the library
    /// </summary>
    static class Data
    {
        /// <summary>
        /// Maps Umbraco node aliases to Caches implementing methods appropriate for
        /// cache addition and removal using Umbraco events.
        /// </summary>
        public static Dictionary<string, ICache> registeredTypes =
                  new Dictionary<string, ICache>
        {
            { "uwbsStore",               StoreCache.Instance },
            { "uwbsZone",                ZoneCache.Instance },
            { "uwbsCategory",            CategoryCache.Instance},
            { "uwbsProduct",             ProductCache.Instance},
            { "uwbsProductVariant",      VariantCache.Instance},
            { "uwbsProductVariantGroup", VariantGroupCache.Instance},
            { "uwbsPaymentProvider",     PaymentProviderCache.Instance},
        };

        /// <summary>
        /// Sequence of caches, in the order they are to be initialized
        /// </summary>
        internal static class InitializationSequence
        {
            public static ICache[] initSeq = new ICache[]
            {
                StoreDomainCache.Instance,
                StoreCache.Instance,
                VariantCache.Instance,
                VariantGroupCache.Instance,
                CategoryCache.Instance,
                ProductCache.Instance,
                ZoneCache.Instance,
                PaymentProviderCache.Instance,
            };


            /// <summary>
            /// Returns all <see cref="ICache"/> in the sequence succeeding the given cache
            /// </summary>
            public static IEnumerable<ICache> Succeeding(ICache cache)
            {
                var indexOf = Array.IndexOf(initSeq, cache);

                return initSeq.Skip(indexOf + 1);
            }
        }
    }
}
