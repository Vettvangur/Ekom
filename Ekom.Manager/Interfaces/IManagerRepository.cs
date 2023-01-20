using Ekom.API;
using Ekom.Manager.Models;
using Ekom.Models;
using Ekom.Utilities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ekom.Interfaces
{
    public interface IManagerRepository
    {
        Task AddActivityLogAsync(string log);
        Task<OrderListData> GetAllOrdersAsync(DateTime start, DateTime end);
        IEnumerable<IDiscount> GetDiscounts();
        Task<IEnumerable<OrderActivityLog>> GetLatestActivityLogsAsync();
        Task<IEnumerable<OrderActivityLog>> GetLatestActivityLogsByUserAsync(string UserName);
        Task<IEnumerable<OrderActivityLog>> GetLogsAsync(Guid uniqueId);
        Task<IEnumerable<OrderActivityLog>> GetOrderActivityLogAsync(Guid orderId);
        Task<IOrderInfo> GetOrderAsync(Guid uniqueId);
        Task<OrderListData> GetOrdersAsync();
        Task<OrderListData> GetOrdersByStatusAsync(DateTime start, DateTime end, OrderStatus orderStatus);
        IEnumerable<IPaymentProvider> GetPaymentProviders();
        IEnumerable<IShippingProvider> GetShippingProviders();
        object GetStatusList();
        object GetStoreList();
        IEnumerable<IStore> GetStores();
        Task InsertOrderAsync(OrderData orderData);
        Task<OrderListData> SearchOrdersAsync(DateTime start, DateTime end, string query, string store, string orderStatus, string payment, string shipping, string discount);
        Task UpdateOrderAsync(OrderData orderData);
        Task UpdateStatusAsync(Guid orderId, OrderStatus orderStatus, ChangeOrderSettings settings);
        Task<PaymentData> GetPaymentData(string orderId);
    }
}
