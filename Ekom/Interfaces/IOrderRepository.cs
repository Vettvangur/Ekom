using Ekom.Models.Data;
using System;
using System.Collections.Generic;

namespace Ekom.Interfaces
{
    public interface IOrderRepository
    {
        IEnumerable<OrderData> GetCompleteOrderByCustomerId(int customerId);
        int GetHighestOrderNumber(string storeAlias = null);
        OrderData GetOrder(Guid uniqueId);
        void InsertOrder(OrderData orderData);
        void UpdateOrder(OrderData orderData);
    }
}
