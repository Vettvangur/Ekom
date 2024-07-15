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
            await using var db = _databaseFactory.GetDatabase();
            
            var data = await db.OrderData.OrderByDescending(x => x.ReferenceId).ToListAsync().ConfigureAwait(false);

            return data;
        }

        public async Task<OrderData> GetOrderAsync(Guid orderId)
        {
            await using var db = _databaseFactory.GetDatabase();
            
            var data = await db.OrderData.FirstAsync(x => x.UniqueId == orderId).ConfigureAwait(false);

            return data;
        }

        public async Task<IOrderInfo> GetOrderInfoAsync(Guid orderId)
        {
            try
            {
                return await API.Order.Instance.GetOrderAsync(orderId);
            }
            catch (Exception ex)
            {
                ExceptionHandler.Handle<HttpResponseException>(ex);
                throw;
            }
        }

        public async Task<OrderListData> SearchOrdersAsync(DateTime start, DateTime end, string query, string store, string orderStatus, string page, string pageSize)
        {
            string whereClause = GenerateWhereClause(orderStatus, query, store);
            
            var sqlBuilder = new StringBuilder($"SELECT ReferenceId,UniqueId,OrderNumber,OrderStatusCol,CustomerEmail,CustomerName,CustomerId,CustomerUsername,ShippingCountry,TotalAmount,Currency,StoreAlias,CreateDate,UpdateDate,PaidDate FROM EkomOrders {whereClause} ORDER BY ReferenceId desc");
            var sqlTotalBuilder = new StringBuilder($"SELECT COUNT(ReferenceId) as Count, AVG(TotalAmount) as AverageAmount, SUM(TotalAmount) as TotalAmount FROM EkomOrders {whereClause}");

            var _page = string.IsNullOrEmpty(page) || !int.TryParse(page, out int tempPage) ? 1 : tempPage;
            var _pageSize = string.IsNullOrEmpty(pageSize) || !int.TryParse(pageSize, out int tempPageSize) ? 30 : tempPageSize;

            sqlBuilder.Append(" OFFSET (" + _page + " - 1) * " + _pageSize + " ROWS\r\nFETCH NEXT " + _pageSize + " ROWS ONLY;");

            var sqlQuery = sqlBuilder.ToString();
            var sqlTotalQuery = sqlTotalBuilder.ToString();

            var param = new
            {
                startDate = start.Date,
                endDate = end.Date.AddDays(1).AddTicks(-1),
                query = "%" + query + "%",
                orderStatus,
                store
            };

            await using var db = _databaseFactory.GetDatabase();
            
            var orders = await db.QueryToListAsync<OrderData>(sqlQuery, param);

            var totals = db.Execute<OrderListDataTotals>(sqlTotalQuery, param);

            var orderListData = new OrderListData(orders, totals)
            {
                Page = _page,
                PageSize = _pageSize
            };

            return orderListData;
        }

        private string GenerateWhereClause(string orderStatus, string query, string store)
        {
            var whereClause = new StringBuilder();

            if (Enum.TryParse(orderStatus, out OrderStatus result) && (result == OrderStatus.ReadyForDispatch || result == OrderStatus.Dispatched))
            {
                whereClause.Append(" WHERE PaidDate >= @startDate AND PaidDate <= @endDate");
            }
            else
            {
                whereClause.Append(" WHERE CreateDate >= @startDate AND CreateDate <= @endDate");
            }

            if (!string.IsNullOrEmpty(query))
            {
                whereClause.Append(" AND (CustomerName LIKE @query OR ReferenceId LIKE @query OR OrderNumber LIKE @query OR CustomerEmail LIKE @query OR CustomerId LIKE @query OR CustomerUsername LIKE @query)");
            }

            if (!string.IsNullOrEmpty(orderStatus) && orderStatus != "CompletedOrders")
            {
                whereClause.Append(" AND OrderStatusCol = @orderStatus");
            }
            else if (!string.IsNullOrEmpty(orderStatus) && orderStatus == "CompletedOrders")
            {
                whereClause.Append(" AND (OrderStatusCol = 'ReadyForDispatch' OR OrderStatusCol = 'OfflinePayment' OR OrderStatusCol = 'ReadyForDispatchWhenStockArrives' OR OrderStatusCol = 'Dispatched' OR OrderStatusCol = 'Closed')");
            }

            if (!string.IsNullOrEmpty(store))
            {
                whereClause.Append(" AND StoreAlias = @store");
            }

            return whereClause.ToString();
        }

        public async Task<List<MostSoldProduct>> MostSoldProducts(DateTime start, DateTime end, string store, string orderStatus)
        {
            var whereClause = "O.OrderInfo IS NOT NULL AND LTRIM(RTRIM(O.OrderInfo)) <> ''";

            if (Enum.TryParse(orderStatus, out OrderStatus result) && (result == OrderStatus.ReadyForDispatch || result == OrderStatus.Dispatched))
            {
                whereClause += " AND PaidDate >= @startDate AND PaidDate <= @endDate";
            }
            else
            {
                whereClause +=" AND CreateDate >= @startDate AND CreateDate <= @endDate";
            }

            if (!string.IsNullOrEmpty(orderStatus) && orderStatus != "CompletedOrders")
            {
                whereClause += " AND OrderStatusCol = @orderStatus";
            }
            else if (!string.IsNullOrEmpty(orderStatus) && orderStatus == "CompletedOrders")
            {
                whereClause += " AND (OrderStatusCol = 'ReadyForDispatch' OR OrderStatusCol = 'OfflinePayment' OR OrderStatusCol = 'ReadyForDispatchWhenStockArrives' OR OrderStatusCol = 'Dispatched' OR OrderStatusCol = 'Closed')";
            }

            if (!string.IsNullOrEmpty(store))
            {
                whereClause += " AND StoreAlias = @store";
            }

            var param = new
            {
                startDate = start,
                endDate = end,
                orderStatus,
                store
            };

            var sqlBuilder = new StringBuilder(@"SELECT 
                MAX(OL.SKU) as SKU,
                MAX(OL.Title) as Title,
                OL.Id,
                COUNT(*) AS ProductCount
            FROM 
                EkomOrders O
            CROSS APPLY 
                OPENJSON (O.OrderInfo, '$.OrderLines')
                WITH (
                    SKU nvarchar(200) '$.Product.SKU',
                    Title nvarchar(200) '$.Product.Title',
                    Id int '$.Product.Id'
                ) AS OL
            WHERE ");

            // Add the where clause
            sqlBuilder.Append(whereClause);

            sqlBuilder.Append(@" 
                GROUP BY
                    OL.Id
                ORDER BY 
                    ProductCount DESC");

            await using var db = _databaseFactory.GetDatabase();
            var products = await db.QueryToListAsync<MostSoldProduct>(sqlBuilder.ToString(), param);

            return products;
        }

        public object GetStatusList()
        {
            var items = Enum.GetValues(typeof(OrderStatus)).Cast<OrderStatus>();

            return items.Select(x => new
            {
                value = x,
                label = string.Concat(x.ToString().Select(x => Char.IsUpper(x) ? " " + x : x.ToString())),
                enumValue = x.ToString()
            });
        }
    }
}
