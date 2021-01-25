using Ekom.Helpers;
using Ekom.Models;
using Ekom.Models.OrderedObjects;
using Ekom.Utilities;
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
        OrderedDiscount Discount { get; }
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
        int TotalQuantity { get; }

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
        /// Includes Vat and discounts but without shipping providers and payment providers.
        /// </summary>
        ICalculatedPrice GrandTotal { get; }
        /// <summary>
        /// The end amount charged for all orderlines, 
        /// including shipping providers, 
        /// payment providers and discounts.
        /// </summary>
        ICalculatedPrice ChargedAmount { get; }
        /// <summary>
        /// OrderLines with OrderDiscount included
        /// </summary>
        ICalculatedPrice SubTotal { get; }
        /// <summary>
        /// OrderLines with OrderDiscount excluded
        /// </summary>
        ICalculatedPrice OrderLineTotal { get; }
        /// <summary>
        /// Total amount of value added tax in order lines.
        /// This counts up all vat whether it's included in item prices or not.
        /// </summary>
        ICalculatedPrice Vat { get; }
        /// <summary>
        /// Total amount of value added tax in order.
        /// This counts up all vat whether it's included in item prices or not.
        /// Includes Vat from shipping providers and payment providers.
        /// </summary>
        ICalculatedPrice ChargedVat { get; }
        /// <summary>
        /// Total monetary value of discount in order
        /// </summary>
        ICalculatedPrice DiscountAmount { get; }

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
        DateTime? PaidDate { get; }

        /// <summary>
        /// 
        /// </summary>
        DateTime UpdateDate { get; }
    }
}
