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
            _log = logFac.GetLogger(typeof(OrderRepository));
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
            }
        }

        public IEnumerable<OrderData> GetCompleteOrderByCustomerId(int customerId)
        {
            using (var db = _appCtx.DatabaseContext.Database)
            {
                return db.Query<OrderData>("WHERE CustomerId = @0 AND (OrderStatusCol = @1 or OrderStatusCol = @2 or OrderStatusCol = @3)", customerId, Helpers.OrderStatus.ReadyForDispatch, Helpers.OrderStatus.OfflinePayment, Helpers.OrderStatus.Confirmed);
            }
        }

        public int GetHighestOrderNumber(string storeAlias = null)
        {
            int orderNumber = 1;

            using (var db = _appCtx.DatabaseContext.Database)
            {
                var _orderNumber = "1";

                if (_config.ShareBasketBetweenStores || string.IsNullOrEmpty(storeAlias))
                {
                    _orderNumber = db.FirstOrDefault<string>("SELECT TOP 1 ReferenceId from EkomOrders ORDER BY CreateDate DESC");
                }
                else
                {
                    _orderNumber = db.FirstOrDefault<string>("SELECT TOP 1 ReferenceId from EkomOrders WHERE StoreAlias = @0 ORDER BY CreateDate DESC", storeAlias);
                }

                int.TryParse(_orderNumber, out orderNumber);

                return orderNumber;
            }
        }
    }
}
