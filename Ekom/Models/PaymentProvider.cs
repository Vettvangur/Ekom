using Ekom.Models.Base;
using Examine;
using log4net;
using System.Reflection;
using Umbraco.Core.Models;

namespace Ekom.Models
{
    /// <summary>
    /// F.x. Borgun/Valitor
    /// </summary>
    public class PaymentProvider : ProviderBase
    {
        /// <summary>
        /// Used by Ekom extensions
        /// </summary>
        /// <param name="store"></param>
        public PaymentProvider(Store store) : base(store) { }

        /// <summary>
        /// Construct PaymentProvider from Examine item
        /// </summary>
        /// <param name="item"></param>
        /// <param name="store"></param>
        public PaymentProvider(SearchResult item, Store store) : base(item, store) { }

        /// <summary>
        /// Construct PaymentProvider from umbraco publish event
        /// </summary>
        /// <param name="node"></param>
        /// <param name="store"></param>
        public PaymentProvider(IContent node, Store store) : base(node, store) { }

        private static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}
