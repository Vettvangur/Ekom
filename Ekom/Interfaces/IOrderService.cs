using Ekom.Exceptions;
using Ekom.Helpers;
using System;
using System.Collections.Generic;

namespace Ekom.Interfaces
{
    /// <summary>
    /// Unused and likely unneeded
    /// </summary>
    [Obsolete]
    public interface IOrderService
    {
        IOrderInfo AddOrderLine(Guid productId, IEnumerable<Guid> variantIds, int quantity, string storeAlias, OrderAction? action);
        /// <summary>
        /// Get an order in progress, does not return completed orders.
        /// </summary>
        /// <param name="storeAlias"></param>
        /// <returns></returns>
        IOrderInfo GetOrder(string storeAlias);
        IOrderInfo GetOrderInfo(Guid uniqueId);
        IOrderInfo RemoveOrderLine(Guid lineId, string storeAlias);

        IOrderInfo UpdateCustomerInformation(Dictionary<string, string> form);
        IOrderInfo UpdateShippingInformation(Guid ShippingProvider, string storeAlias);
        IOrderInfo UpdatePaymentInformation(Guid paymentProviderId, string storeAlias);
        IEnumerable<IOrderInfo> GetCompleteCustomerOrders(int customerId);
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
