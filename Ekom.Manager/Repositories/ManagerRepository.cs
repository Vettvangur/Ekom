using Ekom.API;
using Ekom.Interfaces;
using Ekom.Manager.Models;
using Ekom.Models;
using Ekom.Repositories;
using Ekom.Utilities;
using NPoco;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Umbraco.Core.Logging;
using Umbraco.Core.Scoping;

namespace Ekom.Repository
{
    class ManagerRepository : IManagerRepository
    {
        readonly ILogger _logger;
        readonly Configuration _config;
        readonly IScopeProvider _scopeProvider;
        readonly ActivityLogRepository _activityLogRepository;

        //readonly ActivityLogRepository _activityLogRepository;
        /// <summary>
        /// ctor
        /// </summary>
        public ManagerRepository(
            Configuration config,
            IScopeProvider scopeProvider,
            ILogger logger,
            ActivityLogRepository activityLogRepository)
        {
            _config = config;
            _scopeProvider = scopeProvider;
            _logger = logger;
            _activityLogRepository = activityLogRepository;
        }

        public async Task<IOrderInfo> GetOrderAsync(Guid uniqueId)
        {
            using (var db = _scopeProvider.CreateScope().Database)
            {
                return await db.FirstOrDefaultAsync<IOrderInfo>("WHERE UniqueId = @0", uniqueId)
                    .ConfigureAwait(false);
            }
        }
        public async Task<IEnumerable<OrderActivityLog>> GetOrderActivityLogAsync(Guid orderId)
        {
            using (var db = _scopeProvider.CreateScope().Database)
            {
                return await db.FetchAsync<OrderActivityLog>("WHERE [Key] = @0", orderId)
                    .ConfigureAwait(false);
            }
        }
        public async Task<IEnumerable<OrderActivityLog>> GetLatestActivityLogsAsync()
        {
            return await _activityLogRepository.GetLatestActivityLogsOrdersAsync()
                .ConfigureAwait(false);
        }

        public async Task<IEnumerable<OrderActivityLog>> GetLatestActivityLogsByUserAsync(string UserName)
        {
            return await _activityLogRepository.GetLatestActivityLogsOrdersByUserAsync(UserName)
                .ConfigureAwait(false);
        }

        public async Task<IEnumerable<OrderActivityLog>> GetLogsAsync(Guid uniqueId)
        {
            return await _activityLogRepository.GetLogsAsync(uniqueId)
                .ConfigureAwait(false);
        }

        public async Task<OrderListData> GetOrdersAsync()
        {
            using (var db = _scopeProvider.CreateScope().Database)
            {
                var orders = await db.FetchAsync<OrderData>("ORDER BY ReferenceId")
                    .ConfigureAwait(false);

                return new OrderListData(orders);
            }
        }

        public async Task InsertOrderAsync(OrderData orderData)
        {
            using (var db = _scopeProvider.CreateScope().Database)
            {
                await db.InsertAsync(orderData)
                    .ConfigureAwait(false);
            }
        }

        public async Task UpdateOrderAsync(OrderData orderData)
        {
            using (var db = _scopeProvider.CreateScope().Database)
            {
                await db.UpdateAsync(orderData)
                    .ConfigureAwait(false);
            }
        }

        public async Task AddActivityLogAsync(string log)
        {
            throw new NotImplementedException();

            using (var db = _scopeProvider.CreateScope().Database)
            {

            }
        }

        public async Task<OrderListData> SearchOrdersAsync(DateTime start, DateTime end, string query, string store, string orderStatus, string payment, string shipping, string discount)
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
            }
            else
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
            whereQuery += " ORDER BY ReferenceId desc";

            using (var db = _scopeProvider.CreateScope().Database)
            {
                var orders = await db.FetchAsync<OrderData>(whereQuery,
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
                 OrderStatus.Dispatched)
                    .ConfigureAwait(false);

                return new OrderListData(orders);
            }
        }

        public async Task<PaymentData> GetPaymentData(string orderId)
        {
            try
            {
                using (var db = _scopeProvider.CreateScope().Database)
                {
                    var customOrderId = await db.FirstOrDefaultAsync<Guid>("SELECT UniqueId FROM [NetPaymentOrder] WHERE Custom = @orderId", new { orderId = orderId }).ConfigureAwait(false);

                    if (customOrderId != null && customOrderId != Guid.Empty)
                    {
                        return await db.FirstOrDefaultAsync<PaymentData>("SELECT * FROM [NetPayments] WHERE Id = @Id", new { Id = customOrderId }).ConfigureAwait(false);
                    }
                }

            } catch
            {

            }

            return null;
        }

        public async Task<OrderListData> GetAllOrdersAsync(DateTime start, DateTime end)
        {
            var startDate = start.ToString("yyyy-MM-dd 00:00:00");
            var endDate = end.ToString("yyyy-MM-dd 23:59:59");

            using (var db = _scopeProvider.CreateScope().Database)
            {
                var offlinePayments = await db.FetchAsync<OrderData>(
                    "WHERE (OrderStatusCol = @0) AND (UpdateDate >= @1 AND UpdateDate <= @2)",
                    OrderStatus.OfflinePayment,
                    startDate,
                    endDate)
                    .ConfigureAwait(false);

                var readyOrders = await db.FetchAsync<OrderData>(
                    "WHERE (OrderStatusCol = @0 or OrderStatusCol = @1) AND (PaidDate >= @2 AND PaidDate <= @3)",
                    OrderStatus.ReadyForDispatch,
                    OrderStatus.Dispatched,
                    startDate,
                    endDate)
                    .ConfigureAwait(false);

                return new OrderListData(offlinePayments.Concat(readyOrders).OrderByDescending(x => x.ReferenceId));
            }
        }

        public async Task<OrderListData> GetOrdersByStatusAsync(DateTime start, DateTime end, OrderStatus orderStatus)
        {
            var startDate = start.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var endDate = end.ToString("yyyy-MM-dd HH:mm:ss.fff");

            using (var db = _scopeProvider.CreateScope().Database)
            {
                var orders = await db.FetchAsync<OrderData>("WHERE (OrderStatusCol = @0) AND (UpdateDate >= @1 AND UpdateDate <= @2)",
                    orderStatus,
                    startDate,
                    endDate)
                    .ConfigureAwait(false);

                return new OrderListData(orders);
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

        public async Task UpdateStatusAsync(Guid orderId, OrderStatus orderStatus, ChangeOrderSettings settings)
        {
            await API.Order.Instance.UpdateStatusAsync(orderStatus, orderId, settings: settings)
                .ConfigureAwait(false);
        }
    }
}
