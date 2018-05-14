using Ekom.Helpers;
using Ekom.Models.Data;
using System;
using System.Collections.Generic;

namespace Ekom.Interfaces
{
    public interface IOrderRepository
    {
        IEnumerable<OrderData> GetCompletedOrdersByCustomerId(int customerId);
        IEnumerable<OrderData> GetCompletedOrders();
        IEnumerable<OrderData> GetOrdersByStatus(OrderStatus orderStatus);
        OrderData GetOrder(Guid uniqueId);
        void InsertOrder(OrderData orderData);
        void UpdateOrder(OrderData orderData);
    }
}
