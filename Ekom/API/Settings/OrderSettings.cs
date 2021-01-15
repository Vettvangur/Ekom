using Ekom.Interfaces;
using Ekom.Models;
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
        /// Master switch for all order events
        /// </summary>
        public bool FireEvents { get; set; } = true;

        private bool _fireOnOrderUpdatedEvent = true;
        /// <summary>
        /// Disable Ekom OnOrderUpdated event, 
        /// useful for event listeners not interested in creating a cascade of events.
        /// </summary>
        public bool FireOnOrderUpdatedEvent
        {
            get => _fireOnOrderUpdatedEvent && FireEvents;
            set => _fireOnOrderUpdatedEvent = value;
        }

        // Event handlers are called from inside the lock, 
        // at the end of a call to the OrderService, immediately after writing to db.
        // There is one common method which offered that spot and since it's inside the lock,
        // we have a special flag for event handlers to allow them to enter.
        //
        // We could make OrderInfo public, OrderLines public and provide OrderInfo to event handlers
        // But then all their modifications would bypass any checks in place in Ekom 
        // and all hell would break loose?
        /// <summary>
        /// This flag currently enables special provisions for event handler entry into
        /// locked sections of Order manipulation code.
        /// </summary>
        public bool IsEventHandler { get; set; }
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

    /// <summary>
    /// Ekom Order Api RemoveOrderLine optional configuration
    /// </summary>
    public class RemoveOrderSettings : OrderSettings
    {
        /// <summary>
        /// Target a specific Variant under the given product
        /// </summary>
        public Guid? VariantKey { get; set; }
    }

    /// <summary>
    /// Ekom Order Api ChangeOrderStatus optional configuration
    /// </summary>
    public class ChangeOrderSettings : OrderSettings
    {
        private bool _fireOnOrderStatusChangingEvent = true;
        /// <summary>
        /// Disable Ekom OnOrderStatusChanging event
        /// </summary>
        public bool FireOnOrderStatusChangingEvent
        {
            get => _fireOnOrderStatusChangingEvent && FireEvents;
            set => _fireOnOrderStatusChangingEvent = value;
        }
    }

    /// <summary>
    /// Ekom Order Api RemoveOrderLine optional configuration
    /// </summary>
    public class DiscountOrderSettings : OrderSettings
    {
        /// <summary>
        /// Target a specific Variant under the given product
        /// </summary>
        public string Coupon { get; set; }
    }
}
