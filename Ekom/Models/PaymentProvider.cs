using Ekom.Interfaces;
using Ekom.Models.Behaviors;
using Ekom.Utilities;
using Examine;
using Umbraco.Core.Models;

namespace Ekom.Models
{
    /// <summary>
    /// F.x. Borgun/Valitor
    /// </summary>
    public class PaymentProvider : PerStoreNodeEntity, IPerStoreNodeEntity, IPaymentProvider
    {
        /// <summary>
        /// 
        /// </summary>
        public virtual string Name => Properties["nodeName"];

        /// <summary>
        /// Ranges and zones
        /// </summary>
        public virtual IConstraints Constraints { get; }

        /// <summary>
        /// 
        /// </summary>
        public virtual IPrice Price { get; }

        /// <summary>
        /// Used by Ekom extensions
        /// </summary>
        /// <param name="store"></param>
        public PaymentProvider(IStore store) : base(store) { }

        /// <summary>
        /// Construct PaymentProvider from Examine item
        /// </summary>
        /// <param name="item"></param>
        /// <param name="store"></param>
        public PaymentProvider(SearchResult item, IStore store) : base(item, store)
        {
            Constraints = new Constraints(this);
            Price = new Price(Properties.GetPropertyValue("price", Store.Alias), Store);
        }

        /// <summary>
        /// Construct PaymentProvider from umbraco publish event
        /// </summary>
        /// <param name="node"></param>
        /// <param name="store"></param>
        public PaymentProvider(IContent node, IStore store) : base(node, store)
        {
            Constraints = new Constraints(this);
            Price = new Price(Properties.GetPropertyValue("price", Store.Alias), Store);
        }
    }
}
