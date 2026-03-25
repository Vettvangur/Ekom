using Ekom.Models;
using Ekom.Models.Manager;
using Ekom.Services;
using Ekom.Utilities;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Ekom.Repositories;
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

    public async Task<IEnumerable<OrderData>> GetOrdersAsync(IReadOnlyCollection<string> allowedStoreAliases)
    {
        if (allowedStoreAliases.Count == 0)
        {
            return Array.Empty<OrderData>();
        }

        await using DbContext db = _databaseFactory.GetDatabase();

        string[] stores = allowedStoreAliases.ToArray();

        List<OrderData> data = await db.OrderData
            .Where(x => stores.Contains(x.StoreAlias))
            .OrderByDescending(x => x.ReferenceId)
            .ToListAsync()
            .ConfigureAwait(false);

        return data;
    }

    public async Task<OrderData> GetOrderAsync(Guid orderId, CancellationToken ct = default)
    {
        await using DbContext db = _databaseFactory.GetDatabase();

        OrderData data = await db.OrderData.FirstAsync(x => x.UniqueId == orderId, token: ct).ConfigureAwait(false);

        return data;
    }

    public async Task<IOrderInfo?> GetOrderInfoAsync(Guid orderId, CancellationToken ct = default)
    {
        var culture = Thread.CurrentThread.CurrentCulture.Name;

        var order = await API.Order.Instance.GetOrderAsync(orderId, ct);

        return order;
    }

    public async Task<OrderListData> SearchOrdersAsync(DateTime start, DateTime end, string query, string store, string orderStatus, string paymentProvider, string page, string pageSize)
    {
        string whereClause = GenerateWhereClause(orderStatus, query, store, paymentProvider);

        var sqlBuilder = new StringBuilder($"SELECT ReferenceId,UniqueId,OrderNumber,OrderStatusCol,CustomerEmail,CustomerName,CustomerId,CustomerUsername,ShippingCountry,TotalAmount,Currency,StoreAlias,CreateDate,UpdateDate,PaidDate FROM EkomOrders {whereClause} ORDER BY ReferenceId desc");
        var sqlTotalBuilder = new StringBuilder($"SELECT COUNT(ReferenceId) as Count, AVG(TotalAmount) as AverageAmount, SUM(TotalAmount) as TotalAmount FROM EkomOrders {whereClause}");

        int _page = string.IsNullOrEmpty(page) || !int.TryParse(page, out int tempPage) ? 1 : tempPage;
        int _pageSize = string.IsNullOrEmpty(pageSize) || !int.TryParse(pageSize, out int tempPageSize) ? 30 : tempPageSize;
        int offset = (_page - 1) * _pageSize;

        if (_databaseFactory.IsSqlite)
        {
            sqlBuilder.Append(" LIMIT @pageSize OFFSET @offset;");
        }
        else
        {
            sqlBuilder.Append(" OFFSET @offset ROWS\r\nFETCH NEXT @pageSize ROWS ONLY;");
        }

        string sqlQuery = sqlBuilder.ToString();
        string sqlTotalQuery = sqlTotalBuilder.ToString();

        string? paymentProviderValue = null;
        if (!string.IsNullOrEmpty(paymentProvider) && Guid.TryParse(paymentProvider, out Guid parsedPaymentProvider))
        {
            paymentProviderValue = parsedPaymentProvider.ToString();
        }

        var param = new
        {
            startDate = start.Date,
            endDate = end.Date.AddDays(1).AddTicks(-1),
            query = "%" + query + "%",
            orderStatus,
            store,
            paymentProvider = paymentProviderValue,
            pageSize = _pageSize,
            offset
        };

        await using DbContext db = _databaseFactory.GetDatabase();

        var orders = await db.QueryToListAsync<OrderData>(sqlQuery, param);

        var totals = db.Execute<OrderListDataTotals>(sqlTotalQuery, param);

        var orderListData = new OrderListData(orders, totals)
        {
            Page = _page,
            PageSize = _pageSize
        };

        return orderListData;
    }

    private string GenerateWhereClause(string orderStatus, string query, string store, string paymentProvider)
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

        if (!string.IsNullOrEmpty(paymentProvider) && Guid.TryParse(paymentProvider, out _))
        {
            if (_databaseFactory.IsSqlite)
            {
                whereClause.Append(" AND json_extract(OrderInfo, '$.PaymentProvider.Key') = @paymentProvider");
            }
            else
            {
                whereClause.Append(" AND JSON_VALUE(OrderInfo, '$.PaymentProvider.Key') = @paymentProvider");
            }
        }

        if (!string.IsNullOrEmpty(orderStatus) && orderStatus != "CompletedOrders" && orderStatus != "AllOrders")
        {
            whereClause.Append(" AND OrderStatusCol = @orderStatus");
        }
        else if (!string.IsNullOrEmpty(orderStatus) && orderStatus == "CompletedOrders" && orderStatus != "AllOrders")
        {
            whereClause.Append(" AND (OrderStatusCol = 'ReadyForDispatch' OR OrderStatusCol = 'OfflinePayment' OR OrderStatusCol = 'ReadyForDispatchWhenStockArrives' OR OrderStatusCol = 'Dispatched' OR OrderStatusCol = 'Closed' OR OrderStatusCol = 'ReadyForPickup')");
        }

        if (!string.IsNullOrEmpty(store))
        {
            whereClause.Append(" AND StoreAlias = @store");
        }

        return whereClause.ToString();
    }

    public async Task<List<MostSoldProduct>> MostSoldProducts(DateTime start, DateTime end, string store, string orderStatus)
    {
        string whereClause = "O.OrderInfo IS NOT NULL AND LTRIM(RTRIM(O.OrderInfo)) <> ''";

        if (Enum.TryParse(orderStatus, out OrderStatus result) && (result == OrderStatus.ReadyForDispatch || result == OrderStatus.Dispatched))
        {
            whereClause += " AND PaidDate >= @startDate AND PaidDate <= @endDate";
        }
        else
        {
            whereClause += " AND CreateDate >= @startDate AND CreateDate <= @endDate";
        }

        if (!string.IsNullOrEmpty(orderStatus) && orderStatus != "CompletedOrders")
        {
            whereClause += " AND OrderStatusCol = @orderStatus";
        }
        else if (!string.IsNullOrEmpty(orderStatus) && orderStatus == "CompletedOrders")
        {
            whereClause += " AND (OrderStatusCol = 'ReadyForDispatch' OR OrderStatusCol = 'OfflinePayment' OR OrderStatusCol = 'ReadyForDispatchWhenStockArrives' OR OrderStatusCol = 'Dispatched' OR OrderStatusCol = 'Closed' OR OrderStatusCol = 'ReadyForPickup')";
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

        var sqlBuilder = _databaseFactory.IsSqlite
            ? new StringBuilder(@"SELECT 
                MAX(json_extract(OL.value, '$.Product.SKU')) as SKU,
                MAX(json_extract(OL.value, '$.Product.Title')) as Title,
                json_extract(OL.value, '$.Product.Id') as Id,
                SUM(CAST(json_extract(OL.value, '$.Quantity') AS REAL)) AS ProductCount 
            FROM 
                EkomOrders O
            JOIN 
                json_each(O.OrderInfo, '$.OrderLines') AS OL
            WHERE ")
            : new StringBuilder(@"SELECT 
                MAX(OL.SKU) as SKU,
                MAX(OL.Title) as Title,
                OL.Id,
                SUM(OL.Quantity) AS ProductCount 
            FROM 
                EkomOrders O
            CROSS APPLY 
                OPENJSON (O.OrderInfo, '$.OrderLines')
                WITH (
                    SKU nvarchar(200) '$.Product.SKU',
                    Title nvarchar(200) '$.Product.Title',
                    Id int '$.Product.Id',
                    Quantity decimal '$.Quantity'
                ) AS OL
            WHERE ");

        // Add the where clause
        sqlBuilder.Append(whereClause);

        sqlBuilder.Append(@" 
            GROUP BY
                OL.Id
            ORDER BY 
                ProductCount DESC");

        var asd = sqlBuilder.ToString();

        await using DbContext db = _databaseFactory.GetDatabase();
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
