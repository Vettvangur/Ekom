using System;
using System.Collections.Generic;
using Ekom.Helpers;
using Ekom.Models;
using System.Web.Mvc;

namespace Ekom.Interfaces
{
    public interface IOrderService
    {
        OrderInfo AddOrderLine(Guid productId, IEnumerable<Guid> variantIds, int quantity, string storeAlias, OrderAction? action);
        OrderInfo GetOrder(string storeAlias);
        OrderInfo GetOrderInfo(Guid uniqueId);
        OrderInfo RemoveOrderLine(Guid lineId, string storeAlias);

        OrderInfo UpdateCustomerInformation(Dictionary<string,string> form);
        OrderInfo UpdateShippingInformation(Guid ShippingProvider, string storeAlias);
        OrderInfo UpdatePaymentInformation(Guid paymentProviderId, string storeAlias);
        IEnumerable<OrderInfo> GetCompleteCustomerOrders(int customerId);
        void ChangeOrderStatus(Guid uniqueId, OrderStatus status);
    }
}
