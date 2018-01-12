using Ekom.Helpers;
using Ekom.Interfaces;
using Ekom.Models.Behaviors;
using Examine;
using Umbraco.Core.Models;

namespace Ekom.Models
{
    /// <summary>
    /// F.x. Borgun/Valitor
    /// </summary>
    public class PaymentProvider : PerStoreNodeEntity, IConstrained
    {
        /// <summary>
        /// 
        /// </summary>
        public string Name => Properties["nodeName"];

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
        public PaymentProvider(Store store) : base(store)
        {
            Constraints = new Constraints(this);
        }


        /// <summary>
        /// Construct PaymentProvider from Examine item
        /// </summary>
        /// <param name="item"></param>
        /// <param name="store"></param>
        public PaymentProvider(SearchResult item, Store store) : base(item, store)
        {
            Constraints = new Constraints(this);
        }


        /// <summary>
        /// Construct PaymentProvider from umbraco publish event
        /// </summary>
        /// <param name="node"></param>
        /// <param name="store"></param>
        public PaymentProvider(IContent node, Store store) : base(node, store)
        {
            Constraints = new Constraints(this);
        }
    }
}
