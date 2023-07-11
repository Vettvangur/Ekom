using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Models.Manager;
using Ekom.Services;
using Ekom.Utilities;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Ekom.Repositories
{
    public class ManagerRepository
    {
        readonly ILogger _logger;
        readonly Configuration _config;
        readonly DatabaseFactory _databaseFactory;

        /// <summary>
        /// ctor
        /// </summary>
        public ManagerRepository(
            ILogger<ManagerRepository> logger,
            Configuration config,
            DatabaseFactory databaseFactory)
        {
            _logger = logger;
            _config = config;
            _databaseFactory = databaseFactory;
        }

        public async Task<IEnumerable<OrderData>> GetOrdersAsync()
        {
            using (var db = _databaseFactory.GetDatabase())
            {
                var data = await db.OrderData.OrderByDescending(x => x.ReferenceId).ToListAsync().ConfigureAwait(false);

                return data;
            }
        }

        public async Task<OrderData> GetOrderAsync(Guid orderId)
        {
            using (var db = _databaseFactory.GetDatabase())
            {
                var data = await db.OrderData.FirstAsync(x => x.UniqueId == orderId).ConfigureAwait(false);

                return data;
            }
        }

        public IOrderInfo GetOrderInfo(Guid orderId)
        {
            try
            {
                return API.Order.Instance.GetOrder(orderId);
            }
            catch (Exception ex)
            {
                ExceptionHandler.Handle<HttpResponseException>(ex);
                throw;
            }
        }

        public async Task<OrderListData> SearchOrdersAsync(DateTime start, DateTime end, string query, string store, string orderStatus, string page, string pageSize)
        {
            var sqlBuilder = new StringBuilder("SELECT ReferenceId,UniqueId,OrderNumber,OrderStatusCol,CustomerEmail,CustomerName,CustomerId,CustomerUsername,ShippingCountry,TotalAmount,Currency,StoreAlias,CreateDate,UpdateDate,PaidDate FROM EkomOrders");
            var sqlTotalBuilder = new StringBuilder("SELECT COUNT(ReferenceId) as Count, AVG(TotalAmount) as AverageAmount, SUM(TotalAmount) as TotalAmount FROM EkomOrders");

            if (Enum.TryParse(orderStatus, out OrderStatus result) && (result == OrderStatus.ReadyForDispatch || result == OrderStatus.Dispatched))
            {
                var where = " WHERE PaidDate >= @startDate AND PaidDate <= @endDate";
                sqlBuilder.Append(where);
                sqlTotalBuilder.Append(where);
            } else
            {
                var where = " WHERE CreateDate >= @startDate AND CreateDate <= @endDate";
                sqlBuilder.Append(where);
                sqlTotalBuilder.Append(where);
            }

            if (!string.IsNullOrEmpty(query))
            {
                var where = " AND (CustomerName LIKE @query OR ReferenceId LIKE @query OR OrderNumber LIKE @query OR CustomerEmail LIKE @query OR CustomerId LIKE @query OR CustomerUsername LIKE @query)";
                sqlBuilder.Append(where);
                sqlTotalBuilder.Append(where);
            }
            if (!string.IsNullOrEmpty(orderStatus) && orderStatus != "CompletedOrders")
            {
                var where = " AND OrderStatusCol = @orderStatus";
                sqlBuilder.Append(where);
                sqlTotalBuilder.Append(where);
            } else if (!string.IsNullOrEmpty(orderStatus) && orderStatus == "CompletedOrders")
            {
                var where = " AND (OrderStatusCol = 'ReadyForDispatch' OR OrderStatusCol = 'OfflinePayment' OR OrderStatusCol = 'ReadyForDispatchWhenStockArrives' OR OrderStatusCol = 'Dispatched' OR OrderStatusCol = 'Closed')";
                sqlBuilder.Append(where);
                sqlTotalBuilder.Append(where);
            }
            if (!string.IsNullOrEmpty(store))
            {
                var where = " AND StoreAlias = @store";
                sqlBuilder.Append(where);
                sqlTotalBuilder.Append(where);
            }

            sqlBuilder.Append(" ORDER BY ReferenceId desc");

            int _page;
            int _pageSize;
            if (!string.IsNullOrEmpty(page) && int.TryParse(page, out _page) && !string.IsNullOrEmpty(pageSize) && int.TryParse(pageSize, out _pageSize))
            {
               
            }
            else
            {
                _page = 1;
                _pageSize = 30;
            }

            sqlBuilder.Append(" OFFSET (" + _page + " - 1) * " + _pageSize + " ROWS\r\nFETCH NEXT " + _pageSize + " ROWS ONLY;");

            var sqlQuery = sqlBuilder.ToString();
            var sqlTotalQuery = sqlTotalBuilder.ToString();

            _logger.LogInformation(sqlQuery);

            var param = new
            {
                startDate = start,
                endDate = end,
                query = "%" + query + "%",
                orderStatus,
                store
            };

            using (var db = _databaseFactory.GetDatabase())
            {
                var orders = await db.QueryToListAsync<OrderData>(sqlQuery, param);

                var totals = db.Execute<OrderListDataTotals>(sqlTotalQuery, param);

                var orderListData = new OrderListData(orders, totals);

                orderListData.Page = _page;
                orderListData.PageSize = _pageSize;

                return orderListData;
            }
        }

        public object GetStatusList()
        {
            var items = Enum.GetValues(typeof(OrderStatus)).Cast<OrderStatus>();

            return items.Select(x => new
            {
                value = x,
                label = string.Concat(x.ToString().Select(x => Char.IsUpper(x) ? " " + x : x.ToString()))
            });
        }

    }
}
