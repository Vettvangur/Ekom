using Ekom.Helpers;
using Ekom.Models.Data;
using System;
using System.Collections.Generic;

namespace Ekom.Interfaces
{
    public interface IManagerRepository
    {
        IEnumerable<OrderData> GetAllOrders(DateTime start, DateTime end);
        IEnumerable<OrderData> GetOrdersByStatus(DateTime start, DateTime end, OrderStatus orderStatus);
        OrderData GetOrder(Guid uniqueId);
        IEnumerable<OrderData> GetOrders();
        void InsertOrder(OrderData orderData);
        void UpdateOrder(OrderData orderData);
        void UpdateStatus(Guid orderId, OrderStatus status);
        IEnumerable<IDiscount> GetDiscounts();
        IEnumerable<IShippingProvider> GetShippingProviders();
        IEnumerable<IPaymentProvider> GetPaymentProviders();
        IEnumerable<IStore> GetStores();
    }
}
