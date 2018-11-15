using Ekom.Helpers;
using Ekom.Interfaces;
using Ekom.Manager.Models;
using Ekom.Models.Data;
using Ekom.Services;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core;

namespace Ekom.Repository
{
    class ManagerRepository : IManagerRepository
    {
        readonly ILog _log;
        readonly Configuration _config;
        readonly ApplicationContext _appCtx;
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
            _log = logFac.GetLogger<OrderRepository>();
        }

        public OrderData GetOrder(Guid uniqueId)
        {
            using (var db = _appCtx.DatabaseContext.Database)
            {
                return db.FirstOrDefault<OrderData>("WHERE UniqueId = @0", uniqueId);
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


        public OrderListData SearchOrders(DateTime start, DateTime end, string query, string store, string payment, string shipping, string discount)
        {

            var startDate = start.ToString("yyyy-MM-dd 00:00:00");
            var endDate = end.ToString("yyyy-MM-dd 23:59:59");

            var whereQuery = "WHERE (PaidDate >= @0 AND PaidDate <= @1) ";

            if (query.Length > 0)
            {
                whereQuery += "AND (CustomerName LIKE '%" + query + "%' OR ReferenceId LIKE '%" + query + "%' OR OrderNumber LIKE '%" + query + "%' OR CustomerEmail LIKE '%" + query + "%' OR CustomerId LIKE '%" + query + "%' OR CustomerUsername LIKE '%" + query + "%')";
            }

            if (store.Length > 0)
            {
                whereQuery += "AND (StoreAlias = @3) ";
            }
            if (payment.Length > 0)
            {
                whereQuery += "AND (PaymentMethod = @4) ";
            }
            if (shipping.Length > 0)
            {
                whereQuery += "AND (ShippingMethod = @5) ";
            }
            if (discount.Length > 0)
            {
                whereQuery += "AND (Discount = @6) ";
            }
            using (var db = _appCtx.DatabaseContext.Database)
            {
                return new OrderListData(db.Query<OrderData>(whereQuery,
                 startDate,
                 endDate,
                 query,
                 store,
                 payment,
                 shipping,
                 discount));
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

        public void UpdateStatus(Guid orderId, OrderStatus orderStatus)
        {
            API.Order.Instance.UpdateStatus(orderStatus, orderId);
        }
    }
}
