using Ekom.Helpers;
using Ekom.Models.Data;
using System;
using System.Collections.Generic;

namespace Ekom.Interfaces
{
    public interface IManagerRepository
    {
        IEnumerable<OrderData> GetCompletedOrders();
        IEnumerable<OrderData> GetCompletedOrders(DateTime start, DateTime end);
        IEnumerable<OrderData> GetOrdersByStatus(OrderStatus orderStatus);
        OrderData GetOrder(Guid uniqueId);
        IEnumerable<OrderData> GetOrders();
        void InsertOrder(OrderData orderData);
        void UpdateOrder(OrderData orderData);
    }
}
