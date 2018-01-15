using Ekom.Helpers;
using Ekom.Models;
using Ekom.Models.Discounts;
using System;
using System.Collections.Generic;

namespace Ekom.Interfaces
{
    /// <summary>
    /// Order/Cart
    /// </summary>
    public interface IOrderInfo
    {
        /// <summary>
        /// Force changes to come through order api, 
        /// api can then make checks to ensure that a discount is only ever applied to either cart or items, never both.
        /// </summary>
        IDiscount Discount { get; }
        /// <summary>
        /// 
        /// </summary>
        string Coupon { get; }

        /// <summary>
        /// Gets the uniqueId.
        /// </summary>
        /// <value>
        /// The uniqueId.
        /// </value>
        Guid UniqueId { get; }

        /// <summary>
        /// Gets the quantity.
        /// </summary>
        /// <value>
        /// The quantity.
        /// </value>
        int Quantity { get; }

        /// <summary>
        /// Gets the store info.
        /// </summary>
        /// <value>
        /// The store info.
        /// </value>
        StoreInfo StoreInfo { get; }

        /// <summary>
        /// 
        /// </summary>
        IReadOnlyCollection<IOrderLine> OrderLines { get; }

        /// <summary>
        /// <see cref="Price"/> object for total value of all orderlines.
        /// </summary>
        IDiscountedPrice ChargedAmount { get; }
        IDiscountedPrice SubTotal { get; }
        IDiscountedPrice OrderLineTotal { get; }

        /// <summary>
        /// A collection of hangfire job ids linked to this order,
        /// each job id represents a stock reservation for a store item or discount.
        /// </summary>
        IReadOnlyCollection<string> HangfireJobs { get; }
        string OrderNumber { get; }

        /// <summary>
        /// 
        /// </summary>
        OrderStatus OrderStatus { get; }

        int ReferenceId { get; }

        CustomerInfo CustomerInformation { get; }

        /// <summary>
        /// 
        /// </summary>
        OrderedPaymentProvider PaymentProvider { get; set; }

        /// <summary>
        /// 
        /// </summary>
        OrderedShippingProvider ShippingProvider { get; set; }

        /// <summary>
        /// 
        /// </summary>
        DateTime CreateDate { get; }

        /// <summary>
        /// Date order was paid
        /// </summary>
        DateTime PaidDate { get; }

        /// <summary>
        /// 
        /// </summary>
        DateTime UpdateDate { get; }
    }
}
