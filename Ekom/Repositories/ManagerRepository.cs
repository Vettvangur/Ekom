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

        public async Task<OrderListData> SearchOrdersAsync(DateTime start, DateTime end, string query, string store, string orderStatus)
        {
            var whereQuery = new StringBuilder("SELECT ReferenceId,UniqueId,OrderNumber,OrderStatusCol,CustomerEmail,CustomerName,CustomerId,CustomerUsername,ShippingCountry,TotalAmount,Currency,StoreAlias,CreateDate,UpdateDate,PaidDate FROM EkomOrders");

            if (Enum.TryParse(orderStatus, out OrderStatus result) && (result == OrderStatus.ReadyForDispatch || result == OrderStatus.Dispatched))
            {
                whereQuery.Clear();
                whereQuery.Append(" WHERE PaidDate >= @startDate AND PaidDate <= @endDate");
            } else
            {
                whereQuery.Append(" WHERE CreateDate >= @startDate AND CreateDate <= @endDate");
            }

            if (!string.IsNullOrEmpty(query))
            {
                whereQuery.Append(" AND (CustomerName LIKE @query OR ReferenceId LIKE @query OR OrderNumber LIKE @query OR CustomerEmail LIKE @query OR CustomerId LIKE @query OR CustomerUsername LIKE @query)");
            }
            if (!string.IsNullOrEmpty(orderStatus))
            {
                whereQuery.Append(" AND OrderStatusCol = @orderStatus");
            }
            if (!string.IsNullOrEmpty(store))
            {
                whereQuery.Append(" AND StoreAlias = @store");
            }

            whereQuery.Append(" ORDER BY ReferenceId desc");

            var sqlQuery = whereQuery.ToString();

            using (var db = _databaseFactory.GetDatabase())
            {
                var orders = await db.QueryToListAsync<OrderData>(sqlQuery, new
                {
                    startDate = start,
                    endDate = end,
                    query = "%" + query + "%",
                    orderStatus,
                    store
                });

                return new OrderListData(orders);
            }
        }

    }
}
