using Ekom.Interfaces;
using Ekom.Models.Data;
using Ekom.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Umbraco.Core;
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
            using (var scope = _scopeProvider.CreateScope())
            {
                var data = await scope.Database.Query<OrderData>()
                    .Where(x => x.UniqueId == uniqueId)
                    .FirstOrDefaultAsync()
                    .ConfigureAwait(false);

                scope.Complete();
                return data;
            }
        }

        public async Task InsertOrderAsync(OrderData orderData)
        {
            using (var db = _scopeProvider.CreateScope())
            {
                await db.Database.InsertAsync(orderData).ConfigureAwait(false);
                db.Complete();
            }
        }

        public async Task UpdateOrderAsync(OrderData orderData)
        {
            using (var db = _scopeProvider.CreateScope())
            {
                await db.Database.UpdateAsync(orderData).ConfigureAwait(false);
                //Clear cache after update.
                _appCaches.RuntimeCache.ClearByKey(orderData.UniqueId.ToString());

                db.Complete();
            }
        }

        public async Task<List<OrderData>> GetCompletedOrdersByCustomerIdAsync(int customerId)
        {
            using (var scope = _scopeProvider.CreateScope())
            {
                return await scope.Database.Query<OrderData>()
                    .Where(x => x.CustomerId == customerId 
                        && (x.OrderStatusCol == (int)OrderStatus.ReadyForDispatch 
                        || x.OrderStatusCol == (int)OrderStatus.OfflinePayment
                        || x.OrderStatusCol == (int)OrderStatus.Dispatched))
                    .ToListAsync()
                    .ConfigureAwait(false);
            }
        }

        public async Task<List<OrderData>> GetStatusOrdersByCustomerIdAsync(
            int customerId, 
            OrderStatus[] orderStatuses)
        {
            using (var scope = _scopeProvider.CreateScope())
            {
                return await scope.Database.Query<OrderData>()
                    .Where(x => x.CustomerId == customerId)
                    .Where(x => orderStatuses.Contains((OrderStatus)x.OrderStatusCol))
                    .ToListAsync()
                    .ConfigureAwait(false);
            }
        }
    }
}
