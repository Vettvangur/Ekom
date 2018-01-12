using System;
using System.Collections.Generic;
using Ekom.Helpers;
using Ekom.Models;
using System.Web.Mvc;
using Ekom.Exceptions;

namespace Ekom.Interfaces
{
    /// <summary>
    /// Unused and likely unneeded
    /// </summary>
    [Obsolete]
    public interface IOrderService
    {
        OrderInfo AddOrderLine(Guid productId, IEnumerable<Guid> variantIds, int quantity, string storeAlias, OrderAction? action);
        /// <summary>
        /// Get an order in progress, does not return completed orders.
        /// </summary>
        /// <param name="storeAlias"></param>
        /// <returns></returns>
        OrderInfo GetOrder(string storeAlias);
        OrderInfo GetOrderInfo(Guid uniqueId);
        OrderInfo RemoveOrderLine(Guid lineId, string storeAlias);

        OrderInfo UpdateCustomerInformation(Dictionary<string,string> form);
        OrderInfo UpdateShippingInformation(Guid ShippingProvider, string storeAlias);
        OrderInfo UpdatePaymentInformation(Guid paymentProviderId, string storeAlias);
        IEnumerable<OrderInfo> GetCompleteCustomerOrders(int customerId);
        void ChangeOrderStatus(Guid uniqueId, OrderStatus status);

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="DiscountNotFoundException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <returns></returns>
        bool ApplyDiscountToOrder(Guid discountKey, string storeAlias);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="storeAlias"></param>
        void RemoveDiscountFromOrder(string storeAlias);
        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="OrderLineNotFoundException"></exception>
        /// <exception cref="DiscountNotFoundException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <returns></returns>
        bool ApplyDiscountToOrderLine(Guid productKey, Guid discountKey, string storeAlias);
        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="OrderLineNotFoundException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <returns></returns>
        void RemoveDiscountFromOrderLine(Guid productKey, string storeAlias);
    }
}
