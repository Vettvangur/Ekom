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
    class ManagerRepository : IManagerRepository
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
        public ManagerRepository(Configuration config, ApplicationContext appCtx, ILogFactory logFac)
        {
            _config = config;
            _appCtx = appCtx;
            _log = logFac.GetLogger(typeof(OrderRepository));
        }

        public OrderData GetOrder(Guid uniqueId)
        {
            using (var db = _appCtx.DatabaseContext.Database)
            {
                return db.FirstOrDefault<OrderData>("WHERE UniqueId = @0", uniqueId);
            }
        }

        public IEnumerable<OrderData> GetOrders()
        {
            using (var db = _appCtx.DatabaseContext.Database)
            {
                return db.Query<OrderData>("ORDER BY ReferenceId");
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
            }
        }

        public IEnumerable<OrderData> GetCompletedOrders()
        {
            using (var db = _appCtx.DatabaseContext.Database)
            {
                return db.Query<OrderData>("WHERE (OrderStatusCol = @0 or OrderStatusCol = @1 or OrderStatusCol = @2)", OrderStatus.ReadyForDispatch, OrderStatus.OfflinePayment, OrderStatus.Dispatched);
            }
        }
        public IEnumerable<OrderData> GetCompletedOrders(DateTime start, DateTime end)
        {
            var startDate = start.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var endDate = end.ToString("yyyy-MM-dd HH:mm:ss.fff");

            using (var db = _appCtx.DatabaseContext.Database)
            {
                return db.Query<OrderData>("WHERE (OrderStatusCol = @0 or OrderStatusCol = @1 or OrderStatusCol = @2) AND CreateDate >= @3 AND CreateDate < @4",
                    OrderStatus.ReadyForDispatch,
                    OrderStatus.OfflinePayment,
                    OrderStatus.Dispatched,
                    startDate,
                    endDate);
            }
        }
        public IEnumerable<OrderData> GetOrdersByStatus(OrderStatus orderStatus)
        {
            using (var db = _appCtx.DatabaseContext.Database)
            {
                return db.Query<OrderData>("WHERE OrderStatusCol = @0", orderStatus);
            }
        }
    }
}
