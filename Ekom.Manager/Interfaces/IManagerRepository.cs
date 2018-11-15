using Ekom.Helpers;
using Ekom.Manager.Models;
using Ekom.Models.Data;
using System;
using System.Collections.Generic;

namespace Ekom.Interfaces
{
    public interface IManagerRepository
    {
        OrderListData GetAllOrders(DateTime start, DateTime end);
        OrderListData SearchOrders(DateTime start, DateTime end, string query, string store, string payment, string shipping, string discount);
        OrderListData GetOrdersByStatus(DateTime start, DateTime end, OrderStatus orderStatus);
        OrderData GetOrder(Guid uniqueId);
        OrderListData GetOrders();
        void InsertOrder(OrderData orderData);
        void UpdateOrder(OrderData orderData);
        void UpdateStatus(Guid orderId, OrderStatus status);
        IEnumerable<IDiscount> GetDiscounts();
        IEnumerable<IShippingProvider> GetShippingProviders();
        IEnumerable<IPaymentProvider> GetPaymentProviders();
        IEnumerable<IStore> GetStores();
    }
}
