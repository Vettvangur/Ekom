using Ekom.Helpers;
using Ekom.Interfaces;
using Ekom.Models.Data;
using Ekom.Services;
using log4net;
using System;
using System.Collections.Generic;
using Umbraco.Core;

namespace Ekom.Repository
{
    class OrderRepository : IOrderRepository
    {
        ILog _log;
        Configuration _config;
        ApplicationContext _appCtx;
        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="config"></param>
        /// <param name="appCtx "></param>
        /// <param name="logFac"></param>
        public OrderRepository(Configuration config, ApplicationContext appCtx, ILogFactory logFac)
        {
            _config = config;
            _appCtx = appCtx;
            _log = logFac.GetLogger<OrderRepository>();
        }

        public OrderData GetOrder(Guid uniqueId)
        {
            using (var db = _appCtx.DatabaseContext.Database)
            {
                return db.FirstOrDefault<OrderData>("WHERE UniqueId = @0", uniqueId);
            }
        }

        public void InsertOrder(OrderData orderData)
        {
            using (var db = _appCtx.DatabaseContext.Database)
            {
                db.Insert(orderData);
            }
        }

        public void UpdateOrder(OrderData orderData)
        {
            using (var db = _appCtx.DatabaseContext.Database)
            {
                db.Update(orderData);
                //Clear cache after update.
                _appCtx.ApplicationCache.RuntimeCache.ClearCacheItem($"EkomOrder-{orderData.UniqueId}");

            }
        }

        public IEnumerable<OrderData> GetCompletedOrdersByCustomerId(int customerId)
        {
            using (var db = _appCtx.DatabaseContext.Database)
            {
                return db.Query<OrderData>("WHERE CustomerId = @0 AND (OrderStatusCol = @1 or OrderStatusCol = @2 or OrderStatusCol = @3)", customerId, OrderStatus.ReadyForDispatch, OrderStatus.OfflinePayment, OrderStatus.Dispatched);
            }
        }
    }
}
