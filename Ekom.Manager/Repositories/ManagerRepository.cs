using Ekom.Helpers;
using Ekom.Interfaces;
using Ekom.Manager.Models;
using Ekom.Models.Data;
using Ekom.Services;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using Umbraco.Web.Mvc;
using Umbraco.Core;

namespace Ekom.Repository
{
    class ManagerRepository : IManagerRepository
    {
        readonly ILog _log;
        readonly Configuration _config;
        readonly ApplicationContext _appCtx;
        //readonly ActivityLogRepository _activityLogRepository;
        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="config"></param>
        /// <param name="appCtx "></param>
        /// <param name="logFac"></param>
        public ManagerRepository(Configuration config, ApplicationContext appCtx, ILogFactory logFac )
        {
            _config = config;
            _appCtx = appCtx;
            _log = logFac.GetLogger<OrderRepository>();
            //_activityLogRepository = Configuration.container.GetInstance<ActivityLogRepository>();
        }

        public IOrderInfo GetOrder(Guid uniqueId)
        {
            using (var db = _appCtx.DatabaseContext.Database)
            {
                return db.FirstOrDefault<IOrderInfo>("WHERE UniqueId = @0", uniqueId);
            }
        }
        public IEnumerable<OrderActivityLog> GetOrderActivityLog(Guid orderId)
        {
            using (var db = _appCtx.DatabaseContext.Database)
            {
                return db.Query<OrderActivityLog>("WHERE [Key] = @0",
                 orderId);
            }
        }
        public IEnumerable<OrderActivityLog> GetLatestActivityLogs()
        {
            using (var db = _appCtx.DatabaseContext.Database)
            {
                return db.Query<OrderActivityLog>("ORDER BY CreateDate DESC");
            }
        }
        public IEnumerable<OrderActivityLog> GetLatestActivityLogsByUser(Guid userId)
        {
            using (var db = _appCtx.DatabaseContext.Database)
            {
                return db.Query<OrderActivityLog>("WHERE UserId = @0 ORDER BY CreateDate DESC", userId);
            }
        }

        public OrderListData GetOrders()
        {
            using (var db = _appCtx.DatabaseContext.Database)
            {
                return new OrderListData(db.Query<OrderData>("ORDER BY ReferenceId"));
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

        public void AddActivityLog(string log)
        {
            using (var db = _appCtx.DatabaseContext.Database)
            {

            }
        }


        public OrderListData SearchOrders(DateTime start, DateTime end, string query, string store, string orderStatus, string payment, string shipping, string discount)
        {

            var startDate = start.ToString("yyyy-MM-dd 00:00:00");
            var endDate = end.ToString("yyyy-MM-dd 23:59:59");
            var whereQuery = "WHERE (CreateDate >= @0 AND CreateDate <= @1)";
            OrderStatus result;
            
            if (Enum.TryParse(orderStatus, out result) && result == OrderStatus.ReadyForDispatch || result == OrderStatus.Dispatched)
            {
                whereQuery = "WHERE (PaidDate >= @0 AND PaidDate <= @1) ";
            }

            if (query.Length > 0)
            {
                whereQuery += "AND (CustomerName LIKE '%" + query + "%' OR ReferenceId LIKE '%" + query + "%' OR OrderNumber LIKE '%" + query + "%' OR CustomerEmail LIKE '%" + query + "%' OR CustomerId LIKE '%" + query + "%' OR CustomerUsername LIKE '%" + query + "%')";
            }
            if (orderStatus.Length > 0)
            {
                whereQuery += "AND (OrderStatusCol = @3)";
            } else
            {
                whereQuery += "AND (OrderStatusCol = @8 OR OrderStatusCol = @9 OR OrderStatusCol = @10)";
            }

            if (store.Length > 0)
            {
                whereQuery += "AND (StoreAlias = @4) ";
            }
            if (payment.Length > 0)
            {
                whereQuery += "AND (PaymentMethod = @5) ";
            }
            if (shipping.Length > 0)
            {
                whereQuery += "AND (ShippingMethod = @6) ";
            }
            if (discount.Length > 0)
            {
                whereQuery += "AND (Discount = @7) ";
            }
            using (var db = _appCtx.DatabaseContext.Database)
            {

                return new OrderListData(db.Query<OrderData>(whereQuery,
                 startDate,
                 endDate,
                 query,
                 orderStatus,
                 store,
                 payment,
                 shipping,
                 discount,
                 OrderStatus.OfflinePayment,
                 OrderStatus.ReadyForDispatch,
                 OrderStatus.Dispatched));
            }
        }


        public OrderListData GetAllOrders(DateTime start, DateTime end)
        {
            var startDate = start.ToString("yyyy-MM-dd 00:00:00");
            var endDate = end.ToString("yyyy-MM-dd 23:59:59");

            using (var db = _appCtx.DatabaseContext.Database)
            {
                var offlinePayments = db.Query<OrderData>("WHERE (OrderStatusCol = @0) AND (UpdateDate >= @1 AND UpdateDate <= @2)",
                    OrderStatus.OfflinePayment,
                    startDate,
                    endDate);

                var readyOrders = db.Query<OrderData>("WHERE (OrderStatusCol = @0 or OrderStatusCol = @1) AND (PaidDate >= @2 AND PaidDate <= @3)",
                    OrderStatus.ReadyForDispatch,
                    OrderStatus.Dispatched,
                    startDate,
                    endDate);
                    
                return new OrderListData(offlinePayments.Concat(readyOrders).OrderByDescending(x => x.ReferenceId));
            }
        }

        public OrderListData GetOrdersByStatus(DateTime start, DateTime end, OrderStatus orderStatus)
        {
            var startDate = start.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var endDate = end.ToString("yyyy-MM-dd HH:mm:ss.fff");

            using (var db = _appCtx.DatabaseContext.Database)
            {
                return new OrderListData(db.Query<OrderData>("WHERE (OrderStatusCol = @0) AND (UpdateDate >= @1 AND UpdateDate <= @2)",
                    orderStatus,
                    startDate,
                    endDate));
            }
        }

        public IEnumerable<IStore> GetStores()
        {
            return API.Store.Instance.GetAllStores();
        }
        public object GetStoreList()
        {
            var items = API.Store.Instance.GetAllStores();
            return items.Select(x => new
            {
                value = x.Alias,
                label = x.Alias
            });
        }

        public IEnumerable<IPaymentProvider> GetPaymentProviders()
        {
            return API.Providers.Instance.GetPaymentProviders();
        }

        public IEnumerable<IShippingProvider> GetShippingProviders()
        {
            return API.Providers.Instance.GetShippingProviders();
        }

        public IEnumerable<IDiscount> GetDiscounts()
        {
            return API.Discounts.Instance.GetDiscounts();
        }
        public object GetStatusList()
        {
            var items = Enum.GetValues(typeof(OrderStatus)).Cast<OrderStatus>();

            return items.Select(x => new
            {
                value = x,
                label = x.ToString()
            });
        }

        public void UpdateStatus(Guid orderId, OrderStatus orderStatus)
        {
            API.Order.Instance.UpdateStatus(orderStatus, orderId);
            //_activityLogRepository.CreateActivityLog(orderId, $"updated the order status to {orderStatus.ToString()}");
        }
    }
}
