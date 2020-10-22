using Ekom.Models.Data;
using Ekom.Utilities;
using NPoco.Linq;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Ekom.Interfaces
{
    public interface IOrderRepository
    {
        Task<OrderData> GetOrderAsync(Guid uniqueId);
        Task InsertOrderAsync(OrderData orderData);
        Task UpdateOrderAsync(OrderData orderData);
        Task<List<OrderData>> GetStatusOrdersAsync(
            Expression<Func<OrderData, bool>> filter = null,
            params OrderStatus[] orderStatuses
        );
    }
}
