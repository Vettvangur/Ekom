using Common.Logging;
using Ekom.Models.Base;
using Examine;
using System.Reflection;
using Umbraco.Core.Models;

namespace Ekom.Models
{
    /// <summary>
    /// F.x. home delivery or pickup.
    /// </summary>
    public class ShippingProvider : ProviderBase
    {
        /// <summary>
        /// Used by Ekom extensions
        /// </summary>
        /// <param name="store"></param>
        public ShippingProvider(Store store) : base(store) { }

        /// <summary>
        /// Construct ShippingProvider from Examine item
        /// </summary>
        /// <param name="item"></param>
        /// <param name="store"></param>
        public ShippingProvider(SearchResult item, Store store) : base(item, store) { }

        /// <summary>
        /// Construct ShippingProvider from umbraco publish event
        /// </summary>
        /// <param name="node"></param>
        /// <param name="store"></param>
        public ShippingProvider(IContent node, Store store) : base(node, store) { }

        private static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}
