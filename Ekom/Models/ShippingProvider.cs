using Ekom.Interfaces;
using Ekom.Models.Behaviors;
using Ekom.Utilities;
using Examine;
using Umbraco.Core.Models;

namespace Ekom.Models
{
    /// <summary>
    /// F.x. home delivery or pickup.
    /// </summary>
    public class ShippingProvider : PerStoreNodeEntity, IShippingProvider
    {
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
        public ShippingProvider(IStore store) : base(store) { }

        /// <summary>
        /// Construct ShippingProvider from Examine item
        /// </summary>
        /// <param name="item"></param>
        /// <param name="store"></param>
        public ShippingProvider(ISearchResult item, IStore store) : base(item, store)
        {
            Constraints = new Constraints(this);
            Price = new Price(Properties.GetPropertyValue("price", Store.Alias), Store);
        }

        /// <summary>
        /// Construct ShippingProvider from umbraco publish event
        /// </summary>
        /// <param name="node"></param>
        /// <param name="store"></param>
        public ShippingProvider(IContent node, IStore store) : base(node, store)
        {
            Constraints = new Constraints(this);
            Price = new Price(Properties.GetPropertyValue("price", Store.Alias), Store);
        }
    }
}
