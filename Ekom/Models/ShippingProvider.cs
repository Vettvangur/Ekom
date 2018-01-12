using Ekom.Helpers;
using Ekom.Interfaces;
using Ekom.Models.Behaviors;
using Examine;
using Umbraco.Core.Models;

namespace Ekom.Models
{
    /// <summary>
    /// F.x. home delivery or pickup.
    /// </summary>
    public class ShippingProvider : PerStoreNodeEntity, IConstrained
    {
        /// <summary>
        /// Ranges and zones
        /// </summary>
        public Constraints Constraints { get; }

        IDiscountedPrice _price;
        /// <summary>
        /// 
        /// </summary>
        public IDiscountedPrice Price => _price
            ?? (_price = new Price(Properties.GetStoreProperty("price", store.Alias), store));

        /// <summary>
        /// Used by Ekom extensions
        /// </summary>
        /// <param name="store"></param>
        public ShippingProvider(Store store) : base(store)
        {
            Constraints = new Constraints(this);
        }

        /// <summary>
        /// Construct ShippingProvider from Examine item
        /// </summary>
        /// <param name="item"></param>
        /// <param name="store"></param>
        public ShippingProvider(SearchResult item, Store store) : base(item, store)
        {
            Constraints = new Constraints(this);
        }

        /// <summary>
        /// Construct ShippingProvider from umbraco publish event
        /// </summary>
        /// <param name="node"></param>
        /// <param name="store"></param>
        public ShippingProvider(IContent node, Store store) : base(node, store)
        {
            Constraints = new Constraints(this);
        }
    }
}
