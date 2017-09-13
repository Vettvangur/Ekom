using System;
using System.Collections.Generic;
using uWebshop.Helpers;
using uWebshop.Models;

namespace uWebshop.Interfaces
{
    public interface IOrderService
    {
        OrderInfo AddOrderLine(Guid productId, IEnumerable<Guid> variantIds, int quantity, string storeAlias, OrderAction? action);
        OrderInfo GetOrder(string storeAlias);
        OrderInfo GetOrderInfo(Guid uniqueId);
        OrderInfo RemoveOrderLine(Guid lineId, string storeAlias);
    }
}
