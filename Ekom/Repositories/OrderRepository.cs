using Ekom.Models;
using Ekom.Services;
using Ekom.Utilities;
using LinqToDB;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Ekom.Repositories
{
    class OrderRepository
    {
        readonly ILogger _logger;
        readonly Configuration _config;
        readonly IMemoryCache _memoryCache;
        readonly DatabaseFactory _databaseFactory;
        /// <summary>
        /// ctor
        /// </summary>
        public OrderRepository(
            ILogger<OrderRepository> logger,
            Configuration config,
            DatabaseFactory databaseFactory,
            IMemoryCache memoryCache)
        {
            _logger = logger;
            _config = config;
            _databaseFactory = databaseFactory;
            _memoryCache = memoryCache;
        }

        public async Task<OrderData> GetOrderAsync(Guid uniqueId)
        {
            using (var db = _databaseFactory.GetDatabase())
            {
                var data = await db.OrderData
                    .Where(x => x.UniqueId == uniqueId)
                    .SingleOrDefaultAsync()
                    .ConfigureAwait(false);

                return data;
            }
        }

        public async Task InsertOrderAsync(OrderData orderData)
        {
            using (var db = _databaseFactory.GetDatabase())
            {
                await db.InsertAsync(orderData).ConfigureAwait(false);
            }
        }

        public async Task UpdateOrderAsync(OrderData orderData)
        {
            using (var db = _databaseFactory.GetDatabase())
            {
                await db.UpdateAsync(orderData).ConfigureAwait(false);
                //Clear cache after update.
                _memoryCache.Remove(orderData.UniqueId);
            }
        }

        /// <summary>
        /// Get all Orders with the given OrderStatuses. Optionally filter further by any column.
        /// </summary>
        /// <param name="filter"></param>
        /// <param name="orderStatuses"></param>
        /// <returns></returns>
        public async Task<List<OrderData>> GetStatusOrdersAsync(
            Expression<Func<OrderData, bool>> filter = null,
            params OrderStatus[] orderStatuses
        )
        {
            using (var db = _databaseFactory.GetDatabase())
            {
                var query = db.OrderData
                    .Where(x => orderStatuses.Select(y => y.ToString()).Contains(x.OrderStatusCol));

                if (filter != null)
                {
                    query = query.Where(filter);
                }

                return await query
                    .ToListAsync()
                    .ConfigureAwait(false);
            }
        }
    }
}
