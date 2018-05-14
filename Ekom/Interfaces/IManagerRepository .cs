using Ekom.Models.Data;
using System;
using System.Collections.Generic;

namespace Ekom.Interfaces
{
    public interface IManagerRepository
    {
        IEnumerable<OrderData> GetCompleteOrderByCustomerId(int customerId);
        OrderData GetOrder(Guid uniqueId);
        IEnumerable<OrderData> GetOrders();
        void InsertOrder(OrderData orderData);
        void UpdateOrder(OrderData orderData);
    }
}
