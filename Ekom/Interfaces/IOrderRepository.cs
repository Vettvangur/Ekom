using Ekom.Models.Data;
using System;
using System.Collections.Generic;

namespace Ekom.Interfaces
{
    public interface IOrderRepository
    {
        IEnumerable<OrderData> GetCompleteOrderByCustomerId(int customerId);
        OrderData GetOrder(Guid uniqueId);
        void InsertOrder(OrderData orderData);
        void UpdateOrder(OrderData orderData);
    }
}
