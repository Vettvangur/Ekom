using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using uWebshop.Cache;

namespace uWebshop.Data
{
    /// <summary>
    /// Contains dictionary mappings, constants and other static configuration data for the library
    /// </summary>
    public static class Mappings
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
    }
}
