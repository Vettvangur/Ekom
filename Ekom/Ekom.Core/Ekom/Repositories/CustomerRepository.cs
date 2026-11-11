using Ekom.Models;
using Ekom.Models.Manager;
using Ekom.Models.Orders;
using Ekom.Services;
using Ekom.Utilities;
using LinqToDB.Data;
using LinqToDB.Interceptors;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using System.Data.Common;

namespace Ekom.Repositories;

public sealed class CustomerRepository(DatabaseFactory databaseFactory, IOptions<EkomOptions> options)
{
    private static string Qualification(string? status = null)
    {
        // Lifetime metrics share the selected lifecycle scope; payment and fulfillment
        // filters only decide which customers qualify for the selected period.
        var lifecycle = status?.StartsWith("states:", StringComparison.Ordinal) == true
            ? status[7..].Split(';')[0] : "";
        return OrderStateQuery.Predicate($"states:{(string.IsNullOrEmpty(lifecycle) ? "Open,Closed" : lifecycle)};;")!;
    }

    private static string Selection(string status) => OrderStateQuery.Predicate(status)
        ?? "(@status IN ('CompletedOrders','AllOrders','') OR OrderStatusCol = @status)";

    public string IdentityField => options.Value.Manager.Customers.IdentityField switch
    {
        "CustomerEmail" => "CustomerEmail",
        "CustomerUsername" => "CustomerUsername",
        _ => throw new InvalidOperationException("Ekom:Manager:Customers:IdentityField must be CustomerEmail or CustomerUsername."),
    };
    private string Key => NormalizeColumn(IdentityField);

    private string NormalizeColumn(string column) => databaseFactory.IsSqlite
        ? $"EkomCustomerKey({column})"
        : $"UPPER(LTRIM(RTRIM(COALESCE({column}, ''))) COLLATE Latin1_General_100_CI_AS) COLLATE Latin1_General_100_BIN2";

    // Search display fields directly; identity normalization is only for customer grouping.
    private string SearchColumn(string column) => databaseFactory.IsSqlite
        ? $"instr(EkomCustomerKey(Q.{column}), @normalizedQuery) > 0"
        : $"CHARINDEX(@query, COALESCE(Q.{column}, '') COLLATE Latin1_General_100_CI_AS) > 0";

    private DbContext OpenDatabase()
    {
        var db = databaseFactory.GetDatabase();
        if (databaseFactory.IsSqlite) db.AddInterceptor(new IdentityInterceptor());
        return db;
    }

    private sealed class IdentityInterceptor : ConnectionInterceptor
    {
        public override void ConnectionOpened(ConnectionEventData eventData, DbConnection connection)
            => ((SqliteConnection)connection).CreateFunction<string?, string>("EkomCustomerKey", value => (value ?? "").Trim(' ').ToUpperInvariant(), isDeterministic: true);

        public override Task ConnectionOpenedAsync(ConnectionEventData eventData, DbConnection connection, CancellationToken cancellationToken)
        {
            ConnectionOpened(eventData, connection);
            return Task.CompletedTask;
        }
    }
    private string PageSql => databaseFactory.IsSqlite ? "LIMIT @take OFFSET @skip" : "OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY";
    private const string Columns = "UniqueId, ReferenceId, OrderNumber, OrderStatusCol, CustomerName, CustomerEmail, CustomerUsername, ShippingCountry, TotalAmount, Currency, StoreAlias, CreateDate, UpdateDate, PaidDate";

    // The store's qualifying orders, read once per search. Every customer query below reads this
    // narrow copy instead of rescanning EkomOrders for each reference to Q.
    private string History => databaseFactory.IsSqlite ? "temp.EkomCustomerHistory" : "#EkomCustomerHistory";

    private async Task LoadHistoryAsync(DbContext db, string orderStatus, string? currency, object args, CancellationToken ct)
    {
        string where = $"FROM EkomOrders WHERE StoreAlias = @store{ManagerCurrencyScope.Predicate(currency)} AND {Qualification(orderStatus)}";
        await DropHistoryAsync(db).ConfigureAwait(false);
        if (databaseFactory.IsSqlite)
        {
            await db.ExecuteAsync($"CREATE TEMP TABLE EkomCustomerHistory AS SELECT {Columns}, OrderStateCol, PaymentStateCol, FulfillmentStateCol, {Key} AS [Identity] {where}", ct, args).ConfigureAwait(false);
            return;
        }

        // The cast stops SELECT INTO copying the identity property, which would reject the insert.
        string columns = $"UniqueId, CAST(ReferenceId AS int) AS ReferenceId{Columns["UniqueId, ReferenceId".Length..]}, OrderStateCol, PaymentStateCol, FulfillmentStateCol, {Key} AS [Identity]";
        // Created without parameters so the table belongs to the session, not to one parameterised call.
        await db.ExecuteAsync($"SELECT TOP 0 {columns} INTO {History} FROM EkomOrders", ct).ConfigureAwait(false);
        await db.ExecuteAsync($"INSERT INTO {History} SELECT {columns} {where}", ct, args).ConfigureAwait(false);
    }

    private Task DropHistoryAsync(DbContext db) => db.ExecuteAsync(databaseFactory.IsSqlite
        ? $"DROP TABLE IF EXISTS {History}"
        : $"IF OBJECT_ID('tempdb..{History}') IS NOT NULL DROP TABLE {History}");

    internal string BuildCte(string orderStatus)
    {
        string date = orderStatus is "ReadyForDispatch" or "Dispatched" ? "PaidDate" : "CreateDate";
        string gap = databaseFactory.IsSqlite ? "(julianday(MAX(CreateDate)) - julianday(MIN(CreateDate)))" : "(DATEDIFF_BIG(second, MIN(CreateDate), MAX(CreateDate)) / 86400.0)";
        return $@"WITH Q AS (
 SELECT * FROM {History}
), R AS (
 SELECT Q.*,
 ROW_NUMBER() OVER (PARTITION BY [Identity] ORDER BY CASE WHEN CustomerName IS NULL OR CustomerName = '' THEN 1 ELSE 0 END, CreateDate DESC, ReferenceId DESC) AS NameRank,
 ROW_NUMBER() OVER (PARTITION BY [Identity] ORDER BY CASE WHEN CustomerEmail IS NULL OR CustomerEmail = '' THEN 1 ELSE 0 END, CreateDate DESC, ReferenceId DESC) AS EmailRank,
 ROW_NUMBER() OVER (PARTITION BY [Identity] ORDER BY CASE WHEN CustomerUsername IS NULL OR CustomerUsername = '' THEN 1 ELSE 0 END, CreateDate DESC, ReferenceId DESC) AS UsernameRank,
 ROW_NUMBER() OVER (PARTITION BY [Identity] ORDER BY CreateDate DESC, ReferenceId DESC) AS LatestRank
 FROM Q
), G AS (
 SELECT [Identity], COUNT(*) AS Orders, MIN(CreateDate) AS FirstOrder, MAX(CreateDate) AS LastOrder,
 {gap} / NULLIF(COUNT(*) - 1, 0) AS AverageReorderDays
 FROM Q WHERE [Identity] <> '' GROUP BY [Identity]
), Selected AS (
 SELECT G.* FROM G WHERE EXISTS (SELECT 1 FROM Q WHERE Q.[Identity] = G.[Identity] AND {date} >= @start AND {date} < @end
 AND {Selection(orderStatus)})
 AND (@segment = 'all' OR (@segment = 'new' AND G.Orders = 1) OR (@segment = 'returning' AND G.Orders > 1))
 AND (@query = '' OR EXISTS (SELECT 1 FROM Q WHERE Q.[Identity] = G.[Identity] AND
 ({SearchColumn("CustomerName")} OR {SearchColumn("CustomerEmail")} OR {SearchColumn("CustomerUsername")})))
) ";
    }

    public async Task<CustomerAnalytics> GetAnalyticsAsync(string store, DateTime start, DateTime end, string status, CancellationToken ct = default, string? currency = null)
    {
        string date = status is "ReadyForDispatch" or "Dispatched" ? "PaidDate" : "CreateDate";
        string cte = $@"WITH History AS (
 SELECT ReferenceId, CreateDate, PaidDate, TotalAmount, OrderStatusCol, OrderStateCol, PaymentStateCol, FulfillmentStateCol, {Key} AS CustomerIdentity,
 ROW_NUMBER() OVER (PARTITION BY {Key} ORDER BY COALESCE(PaidDate, CreateDate), ReferenceId) AS PurchaseNumber
 FROM EkomOrders WHERE StoreAlias = @store{ManagerCurrencyScope.Predicate(currency)} AND {Qualification(status)}
), Selected AS (
 SELECT * FROM History WHERE {date} >= @start AND {date} < @end
 AND {Selection(status)}
) ";
        var args = new { store, start = start.Date, end = end.Date.AddDays(1), status };
        await using var db = OpenDatabase();
        var rows = await db.QueryToListAsync<CustomerAnalytics>(cte + @"
SELECT COUNT(DISTINCT NULLIF(CustomerIdentity, '')) AS UniqueCustomers,
 COUNT(DISTINCT CASE WHEN CustomerIdentity <> '' AND PurchaseNumber = 1 THEN CustomerIdentity END) AS NewCustomers,
 COUNT(DISTINCT CASE WHEN CustomerIdentity <> '' AND PurchaseNumber > 1 THEN CustomerIdentity END) AS ReturningCustomers,
 COALESCE(SUM(CASE WHEN CustomerIdentity <> '' AND PurchaseNumber = 1 THEN 1 ELSE 0 END),0) AS FirstOrders,
 COALESCE(SUM(CASE WHEN CustomerIdentity <> '' AND PurchaseNumber > 1 THEN 1 ELSE 0 END),0) AS RepeatOrders,
 COALESCE(SUM(CASE WHEN CustomerIdentity <> '' AND PurchaseNumber = 1 THEN TotalAmount ELSE 0 END),0) AS FirstOrderAmount,
 COALESCE(SUM(CASE WHEN CustomerIdentity <> '' AND PurchaseNumber > 1 THEN TotalAmount ELSE 0 END),0) AS RepeatOrderAmount,
 COALESCE(SUM(CASE WHEN CustomerIdentity = '' THEN 1 ELSE 0 END),0) AS MissingIdentifiers
FROM Selected", ct, args).ConfigureAwait(false);
        var result = rows[0];
        result.IdentityField = IdentityField;
        string weekday = databaseFactory.IsSqlite ? "(CAST(strftime('%w', CreateDate) AS INTEGER) + 6) % 7" : "((DATEDIFF(day, '19000101', CreateDate) % 7) + 7) % 7";
        string hour = databaseFactory.IsSqlite ? "CAST(strftime('%H', CreateDate) AS INTEGER)" : "DATEPART(hour, CreateDate)";
        // No historical ranking is needed for the time-of-order breakdown.
        result.OrderTimes = await db.QueryToListAsync<CustomerOrderTime>($@"
SELECT {weekday} AS Weekday, {hour} AS Hour, COUNT(*) AS Orders FROM EkomOrders
WHERE StoreAlias = @store{ManagerCurrencyScope.Predicate(currency)} AND {Qualification(status)} AND {date} >= @start AND {date} < @end
AND {Selection(status)}
GROUP BY {weekday}, {hour}", ct, args).ConfigureAwait(false);
        return result;
    }

    private static string Sort(string sort) => sort switch
    {
        "oldest" => "FirstOrder ASC",
        "lastOrder" => "LastOrder DESC",
        "orders" => "Orders DESC",
        "name" => "Name ASC",
        _ => "FirstOrder DESC",
    };

    public async Task<CustomerSearchResult> SearchAsync(string store, DateTime start, DateTime end, string status, string query, string segment, string sort, int page, int pageSize, CancellationToken ct = default, string? currency = null)
    {
        await using var db = OpenDatabase();
        var args = Arguments(store, start, end, status, query, segment, page, pageSize, currency);
        string cte = BuildCte(status);
        await LoadHistoryAsync(db, status, currency, args, ct).ConfigureAwait(false);
        try
        {
            string profiles = cte + @", Profiles AS (
SELECT S.[Identity], S.Orders, S.FirstOrder, S.LastOrder,
 MAX(CASE WHEN R.NameRank = 1 THEN CustomerName END) AS Name,
 MAX(CASE WHEN R.EmailRank = 1 THEN CustomerEmail END) AS Email,
 MAX(CASE WHEN R.UsernameRank = 1 THEN CustomerUsername END) AS Username,
 MAX(CASE WHEN R.LatestRank = 1 THEN OrderStatusCol END) AS LastStatus
 FROM Selected S JOIN R ON R.[Identity] = S.[Identity]
 GROUP BY S.[Identity], S.Orders, S.FirstOrder, S.LastOrder
), PageKeys AS (SELECT * FROM Profiles ORDER BY " + Sort(sort) + ", [Identity] " + PageSql + ") ";
            var customers = await db.QueryToListAsync<CustomerSummary>(profiles + "SELECT * FROM PageKeys ORDER BY " + Sort(sort) + ", [Identity]", ct, args).ConfigureAwait(false);
            var pageSpend = await db.QueryToListAsync<CustomerSpend>(profiles + "SELECT Q.[Identity], CASE WHEN @currency = '' THEN Q.Currency ELSE @currency END AS Currency, SUM(Q.TotalAmount) AS Total, COUNT(*) AS Orders FROM Q JOIN PageKeys P ON P.[Identity] = Q.[Identity] GROUP BY Q.[Identity], CASE WHEN @currency = '' THEN Q.Currency ELSE @currency END", ct, args).ConfigureAwait(false);
            var spendByIdentity = pageSpend.ToLookup(x => x.Identity, StringComparer.Ordinal);
            foreach (var customer in customers)
            {
                customer.Spend = spendByIdentity[customer.Identity].ToArray();
            }
            var totals = await db.QueryToListAsync<CustomerTotals>(cte + "SELECT COUNT(*) AS Customers, COALESCE(SUM(CASE WHEN Orders > 1 THEN 1 ELSE 0 END),0) AS [Returning], COALESCE(SUM(Orders),0) AS Orders, AVG(AverageReorderDays) AS AverageReorderDays FROM Selected", ct, args).ConfigureAwait(false);
            var spend = await db.QueryToListAsync<CustomerSpend>(cte + "SELECT CASE WHEN @currency = '' THEN Currency ELSE @currency END AS Currency, SUM(TotalAmount) AS Total, COUNT(*) AS Orders, COUNT(DISTINCT Q.[Identity]) AS Customers FROM Q INNER JOIN Selected S ON Q.[Identity] = S.[Identity] GROUP BY CASE WHEN @currency = '' THEN Currency ELSE @currency END", ct, args).ConfigureAwait(false);
            string date = status is "ReadyForDispatch" or "Dispatched" ? "PaidDate" : "CreateDate";
            var missing = await db.ExecuteAsync<int>($"SELECT COUNT(*) FROM {History} WHERE [Identity] = '' AND {date} >= @start AND {date} < @end AND {Selection(status)}", ct, args).ConfigureAwait(false);
            return new CustomerSearchResult { Customers = customers, Totals = totals[0], Spend = spend, MissingIdentifiers = missing, IdentityField = IdentityField, Page = page, PageSize = pageSize, TotalPages = (int)Math.Ceiling((double)totals[0].Customers / pageSize) };
        }
        finally
        {
            await DropHistoryAsync(db).ConfigureAwait(false);
        }
    }

    internal static object Arguments(string store, DateTime start, DateTime end, string status, string query, string segment, int page, int pageSize, string? currency = null)
        => new { currency = ManagerCurrencyScope.Code(currency) ?? "", store, start = start.Date, end = end.Date.AddDays(1), status, query = query.Trim(), normalizedQuery = query.Trim().ToUpperInvariant(), segment, take = pageSize, skip = (page - 1) * pageSize };

    private async Task<List<CustomerSpend>> SpendAsync(DataConnection db, string store, string identity, CancellationToken ct, string? currency, string? status)
        => await db.QueryToListAsync<CustomerSpend>($"SELECT CASE WHEN @currency = '' THEN Currency ELSE @currency END AS Currency, SUM(TotalAmount) AS Total, COUNT(*) AS Orders FROM EkomOrders WHERE StoreAlias = @store{ManagerCurrencyScope.Predicate(currency)} AND {Key} = @identity AND {Qualification(status)} GROUP BY CASE WHEN @currency = '' THEN Currency ELSE @currency END", ct, new { store, identity, currency = ManagerCurrencyScope.Code(currency) ?? "" }).ConfigureAwait(false);

    public async Task<object?> DetailAsync(string store, string identity, int page, bool other, CancellationToken ct = default, string? currency = null, string? status = null)
    {
        await using var db = OpenDatabase();
        identity = identity.Trim(' ').ToUpperInvariant();
        var args = new { store, identity, take = 20, skip = (page - 1) * 20 };
        string where = $"StoreAlias = @store{ManagerCurrencyScope.Predicate(currency)} AND {Key} = @identity";
        var summaries = await db.QueryToListAsync<CustomerSummary>($"SELECT {Key} AS [Identity], COUNT(*) AS Orders, MIN(CreateDate) AS FirstOrder, MAX(CreateDate) AS LastOrder FROM EkomOrders WHERE {where} AND {Qualification(status)} GROUP BY {Key}", ct, args).ConfigureAwait(false);
        var summary = summaries.FirstOrDefault();
        if (summary == null) return null;
        summary.Spend = await SpendAsync(db, store, identity, ct, currency, status).ConfigureAwait(false);
        string scope = where + (other ? $" AND NOT ({Qualification(status)})" : $" AND {Qualification(status)}");
        var orders = await db.QueryToListAsync<OrderData>($"SELECT {Columns}, OrderStateCol, PaymentStateCol, FulfillmentStateCol, OriginalOrderStatusCol, StatusMappingVersion, StatusRevision, AutoCloseSuppressed, FulfilledQuantitiesJson FROM EkomOrders WHERE {scope} ORDER BY CreateDate DESC, ReferenceId DESC {PageSql}", ct, args).ConfigureAwait(false);
        var count = await db.ExecuteAsync<int>($"SELECT COUNT(*) FROM EkomOrders WHERE {scope}", ct, args).ConfigureAwait(false);
        string top = databaseFactory.IsSqlite ? "" : "TOP 1 ";
        string limit = databaseFactory.IsSqlite ? " LIMIT 1" : "";
        var snapshots = await db.QueryToListAsync<OrderData>($"SELECT {top}{Columns}, OrderInfo FROM EkomOrders WHERE {where} AND {Qualification(status)} ORDER BY CreateDate DESC, ReferenceId DESC{limit}", ct, args).ConfigureAwait(false);
        var snapshot = snapshots.FirstOrDefault();
        if (snapshot == null) return null;
        summary.Name = snapshot.CustomerName;
        summary.Email = snapshot.CustomerEmail;
        summary.Username = snapshot.CustomerUsername;
        summary.ShippingCountry = snapshot.ShippingCountry;
        return new { Customer = summary, Orders = orders, Count = count, Page = page, TotalPages = (int)Math.Ceiling(count / 20.0), Snapshot = CustomerSnapshot.Parse(snapshot), IdentityField };
    }
}
