using Ekom.Interfaces;
using Ekom.Models.Data;
using Ekom.Utilities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Umbraco.Core.Cache;
using Umbraco.Core.Logging;
using Umbraco.Core.Scoping;

namespace Ekom.Repository
{
    class OrderRepository : IOrderRepository
    {
        readonly ILogger _logger;
        readonly Configuration _config;
        readonly AppCaches _appCaches;
        readonly IScopeProvider _scopeProvider;
        /// <summary>
        /// ctor
        /// </summary>
        public OrderRepository(
            ILogger logger,
            Configuration config,
            AppCaches appCaches,
            IScopeProvider scopeProvider)
        {
            _logger = logger;
            _config = config;
            _appCaches = appCaches;
            _scopeProvider = scopeProvider;
        }

        public async Task<OrderData> GetOrderAsync(Guid uniqueId)
        {
            using (var db = _scopeProvider.CreateScope().Database)
            {
                return await db.FirstOrDefaultAsync<OrderData>("WHERE UniqueId = @0", uniqueId)
                    .ConfigureAwait(false);
            }
        }

        public async Task InsertOrderAsync(OrderData orderData)
        {
            using (var db = _scopeProvider.CreateScope().Database)
            {
                await db.InsertAsync(orderData).ConfigureAwait(false);
            }
        }

        public async Task UpdateOrderAsync(OrderData orderData)
        {
            using (var db = _scopeProvider.CreateScope().Database)
            {
                await db.UpdateAsync(orderData).ConfigureAwait(false);
                //Clear cache after update.
                _appCaches.RuntimeCache.ClearByKey($"EkomOrder-{orderData.UniqueId}");

            }
        }

        public async Task<List<OrderData>> GetCompletedOrdersByCustomerIdAsync(int customerId)
        {
            using (var db = _scopeProvider.CreateScope().Database)
            {
                return await db.FetchAsync<OrderData>(
                    "WHERE CustomerId = @0 AND (OrderStatusCol = @1 or OrderStatusCol = @2 or OrderStatusCol = @3)",
                    customerId,
                    OrderStatus.ReadyForDispatch,
                    OrderStatus.OfflinePayment,
                    OrderStatus.Dispatched
                ).ConfigureAwait(false);
            }
        }
    }
}
