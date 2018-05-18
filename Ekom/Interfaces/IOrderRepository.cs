using Ekom.Models.Data;
using System;
using System.Collections.Generic;

namespace Ekom.Interfaces
{
    public interface IOrderRepository
    {
        OrderData GetOrder(Guid uniqueId);
        void InsertOrder(OrderData orderData);
        void UpdateOrder(OrderData orderData);
        IEnumerable<OrderData> GetCompletedOrdersByCustomerId(int customerId);
    }
}
