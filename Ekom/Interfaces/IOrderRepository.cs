using Ekom.Models.Data;
using Ekom.Utilities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ekom.Interfaces
{
    public interface IOrderRepository
    {
        Task<OrderData> GetOrderAsync(Guid uniqueId);
        Task InsertOrderAsync(OrderData orderData);
        Task UpdateOrderAsync(OrderData orderData);
        Task<List<OrderData>> GetCompletedOrdersByCustomerIdAsync(int customerId);

        Task<List<OrderData>> GetStatusOrdersByCustomerIdAsync(int customerId, OrderStatus[] orderStatuses);
    }
}
