using Ekom.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ekom.API.Settings
{
    /// <summary>
    /// Ekom Order Api optional configuration
    /// </summary>
    public class OrderSettings
    {
        /// <summary>
        /// Disable Ekom OnOrderUpdated event, 
        /// useful for event listeners not interested in creating a cascade of events.
        /// </summary>
        public bool FireOnOrderUpdatedEvent { get; set; } = true;
    }

    /// <summary>
    /// Ekom Order Api AddOrderLine optional configuration
    /// </summary>
    public class AddOrderSettings : OrderSettings
    {
        /// <summary>
        /// Default is AddOrUpdate, we also allow to set quantity to fixed amount.
        /// </summary>
        public OrderAction OrderAction { get; set; } = OrderAction.AddOrUpdate;

        /// <summary>
        /// Target a specific Variant under the given product
        /// </summary>
        public Guid? VariantKey { get; set; }
    }
}
