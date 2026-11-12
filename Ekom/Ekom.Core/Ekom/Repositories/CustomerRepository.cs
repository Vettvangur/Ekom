using Ekom.Models;
using Ekom.Models.Manager;
using Ekom.Models.Orders;
using Ekom.Services;
using Ekom.Utilities;
using LinqToDB.Data;
using LinqToDB.Interceptors;
using LinqToDB.Mapping;
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

    // Only what the search reads. DatabaseService.EnsureCustomerListIndex covers these columns,
    // so add a column to both or the load goes back to scanning the whole orders table.
    internal const string HistoryColumns = "OrderStatusCol, OrderStateCol, PaymentStateCol, FulfillmentStateCol, CustomerName, CustomerEmail, CustomerUsername, TotalAmount, Currency, CreateDate, PaidDate";

    // The customers matching the search, worked out once from the history. Totals, spend and the
    // page of customers all read this table instead of each regrouping and refiltering the history.
    private string Selected => databaseFactory.IsSqlite ? "temp.EkomCustomerSelected" : "#EkomCustomerSelected";

    // Creates and fills both work tables. SQL Server takes two round trips and SQLite one.
    private async Task LoadWorkTablesAsync(DbContext db, string orderStatus, string? currency, object args, CancellationToken ct)
    {
        string where = $"FROM EkomOrders WHERE StoreAlias = @store{ManagerCurrencyScope.Predicate(currency)} AND {Qualification(orderStatus)}";
        string date = orderStatus is "ReadyForDispatch" or "Dispatched" ? "PaidDate" : "CreateDate";
        string gap = databaseFactory.IsSqlite ? "(julianday(MAX(CreateDate)) - julianday(MIN(CreateDate)))" : "(DATEDIFF_BIG(second, MIN(CreateDate), MAX(CreateDate)) / 86400.0)";
        string select = $@"WITH G AS (
 SELECT [Identity], COUNT(*) AS Orders, MIN(CreateDate) AS FirstOrder, MAX(CreateDate) AS LastOrder,
 {gap} / NULLIF(COUNT(*) - 1, 0) AS AverageReorderDays
 FROM {History} WHERE [Identity] <> '' GROUP BY [Identity]
)
INSERT INTO {Selected} ([Identity], Orders, FirstOrder, LastOrder, AverageReorderDays)
SELECT G.[Identity], G.Orders, G.FirstOrder, G.LastOrder, G.AverageReorderDays FROM G
 WHERE EXISTS (SELECT 1 FROM {History} Q WHERE Q.[Identity] = G.[Identity] AND {date} >= @start AND {date} < @end
 AND {Selection(orderStatus)})
 AND (@segment = 'all' OR (@segment = 'new' AND G.Orders = 1) OR (@segment = 'returning' AND G.Orders > 1))
 AND (@query = '' OR EXISTS (SELECT 1 FROM {History} Q WHERE Q.[Identity] = G.[Identity] AND
 ({SearchColumn("CustomerName")} OR {SearchColumn("CustomerEmail")} OR {SearchColumn("CustomerUsername")})))";

        if (databaseFactory.IsSqlite)
        {
            // SQLite joins by nested loops, so both tables need the identity indexed.
            await db.ExecuteAsync(string.Join(";\n",
                DropWorkTables,
                $"CREATE TEMP TABLE EkomCustomerHistory AS SELECT ReferenceId, {HistoryColumns}, {Key} AS [Identity] {where}",
                "CREATE INDEX temp.IX_EkomCustomerHistory_Identity ON EkomCustomerHistory ([Identity])",
                "CREATE TEMP TABLE EkomCustomerSelected ([Identity] TEXT NOT NULL PRIMARY KEY, Orders INTEGER NOT NULL, FirstOrder, LastOrder, AverageReorderDays REAL)",
                select), ct, args).ConfigureAwait(false);
            return;
        }

        // The cast stops SELECT INTO copying the identity property, which would reject the insert.
        string columns = $"CAST(ReferenceId AS int) AS ReferenceId, {HistoryColumns}, {Key} AS [Identity]";
        // Created without parameters so the tables belong to the session, not to one parameterised call.
        await db.ExecuteAsync(string.Join(";\n",
            DropWorkTables,
            $"SELECT TOP 0 {columns} INTO {History} FROM EkomOrders",
            $"CREATE TABLE {Selected} ([Identity] nvarchar(4000) COLLATE Latin1_General_100_BIN2 NOT NULL, Orders int NOT NULL, FirstOrder datetime2 NOT NULL, LastOrder datetime2 NOT NULL, AverageReorderDays float NULL)"), ct).ConfigureAwait(false);
        await db.ExecuteAsync($"INSERT INTO {History} SELECT {columns} {where};\n{select}", ct, args).ConfigureAwait(false);
    }

    private string DropWorkTables => string.Join(";\n", new[] { Selected, History }.Select(table => databaseFactory.IsSqlite
        ? $"DROP TABLE IF EXISTS {table}"
        : $"IF OBJECT_ID('tempdb..{table}') IS NOT NULL DROP TABLE {table}"));

    // The search's five result sets, read in one round trip.
    private sealed class SearchSets
    {
        [ResultSetIndex(0)] public List<CustomerSummary> Customers { get; set; } = new();
        [ResultSetIndex(1)] public List<CustomerSpend> PageSpend { get; set; } = new();
        [ResultSetIndex(2)] public List<CustomerTotals> Totals { get; set; } = new();
        [ResultSetIndex(3)] public List<CustomerSpend> Spend { get; set; } = new();
        [ResultSetIndex(4)] public List<int> Missing { get; set; } = new();
    }

    private const string NameOrder = "CASE WHEN Q.CustomerName IS NULL OR Q.CustomerName = '' THEN 1 ELSE 0 END, Q.CreateDate DESC, Q.ReferenceId DESC";

    // The identities on the requested page. Only sorting by name needs anything beyond the selected customers.
    private string PageKeys(string sort) => sort == "name"
        ? $@"SELECT S.[Identity], S.Orders, S.FirstOrder, S.LastOrder FROM {Selected} S
 JOIN (SELECT Q.[Identity], Q.CustomerName AS Name, ROW_NUMBER() OVER (PARTITION BY Q.[Identity] ORDER BY {NameOrder}) AS NameRank FROM {History} Q) N
 ON N.[Identity] = S.[Identity] AND N.NameRank = 1
 ORDER BY N.Name ASC, S.[Identity] {PageSql}"
        : $"SELECT S.[Identity], S.Orders, S.FirstOrder, S.LastOrder FROM {Selected} S ORDER BY {Sort(sort)}, S.[Identity] {PageSql}";

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
        try
        {
            await LoadWorkTablesAsync(db, status, currency, args, ct).ConfigureAwait(false);
            string pageKeys = PageKeys(sort);
            const string currencyColumn = "CASE WHEN @currency = '' THEN Q.Currency ELSE @currency END";
            string date = status is "ReadyForDispatch" or "Dispatched" ? "PaidDate" : "CreateDate";
            // Names, contact details and last status are ranked for the customers on the page only.
            string customers = $@"WITH P AS ({pageKeys}), R AS (
 SELECT Q.*,
 ROW_NUMBER() OVER (PARTITION BY Q.[Identity] ORDER BY {NameOrder}) AS NameRank,
 ROW_NUMBER() OVER (PARTITION BY Q.[Identity] ORDER BY CASE WHEN Q.CustomerEmail IS NULL OR Q.CustomerEmail = '' THEN 1 ELSE 0 END, Q.CreateDate DESC, Q.ReferenceId DESC) AS EmailRank,
 ROW_NUMBER() OVER (PARTITION BY Q.[Identity] ORDER BY CASE WHEN Q.CustomerUsername IS NULL OR Q.CustomerUsername = '' THEN 1 ELSE 0 END, Q.CreateDate DESC, Q.ReferenceId DESC) AS UsernameRank,
 ROW_NUMBER() OVER (PARTITION BY Q.[Identity] ORDER BY Q.CreateDate DESC, Q.ReferenceId DESC) AS LatestRank
 FROM {History} Q JOIN P ON P.[Identity] = Q.[Identity]
)
SELECT P.[Identity], P.Orders, P.FirstOrder, P.LastOrder,
 MAX(CASE WHEN R.NameRank = 1 THEN R.CustomerName END) AS Name,
 MAX(CASE WHEN R.EmailRank = 1 THEN R.CustomerEmail END) AS Email,
 MAX(CASE WHEN R.UsernameRank = 1 THEN R.CustomerUsername END) AS Username,
 MAX(CASE WHEN R.LatestRank = 1 THEN R.OrderStatusCol END) AS LastStatus
 FROM P JOIN R ON R.[Identity] = P.[Identity]
 GROUP BY P.[Identity], P.Orders, P.FirstOrder, P.LastOrder
 ORDER BY {Sort(sort)}, P.[Identity]";
            // Both spend queries return the same columns in the same order: linq2db maps a type by
            // column position once per command, so a differing order would misread the second set.
            string pageSpend = $"WITH P AS ({pageKeys}) SELECT Q.[Identity], {currencyColumn} AS Currency, SUM(Q.TotalAmount) AS Total, COUNT(*) AS Orders, 0 AS Customers FROM {History} Q JOIN P ON P.[Identity] = Q.[Identity] GROUP BY Q.[Identity], {currencyColumn}";
            string totals = $"SELECT COUNT(*) AS Customers, COALESCE(SUM(CASE WHEN Orders > 1 THEN 1 ELSE 0 END),0) AS [Returning], COALESCE(SUM(Orders),0) AS Orders, AVG(AverageReorderDays) AS AverageReorderDays FROM {Selected}";
            string spend = $"SELECT '' AS [Identity], {currencyColumn} AS Currency, SUM(Q.TotalAmount) AS Total, COUNT(*) AS Orders, COUNT(DISTINCT Q.[Identity]) AS Customers FROM {History} Q INNER JOIN {Selected} S ON Q.[Identity] = S.[Identity] GROUP BY {currencyColumn}";
            string missing = $"SELECT COUNT(*) FROM {History} WHERE [Identity] = '' AND {date} >= @start AND {date} < @end AND {Selection(status)}";
            var sets = await new CommandInfo(db, string.Join(";\n", customers, pageSpend, totals, spend, missing), args).QueryMultipleAsync<SearchSets>(ct).ConfigureAwait(false);
            var spendByIdentity = sets.PageSpend.ToLookup(x => x.Identity, StringComparer.Ordinal);
            foreach (var customer in sets.Customers)
            {
                customer.Spend = spendByIdentity[customer.Identity].ToArray();
            }
            return new CustomerSearchResult { Customers = sets.Customers, Totals = sets.Totals[0], Spend = sets.Spend, MissingIdentifiers = sets.Missing[0], IdentityField = IdentityField, Page = page, PageSize = pageSize, TotalPages = (int)Math.Ceiling((double)sets.Totals[0].Customers / pageSize) };
        }
        finally
        {
            await db.ExecuteAsync(DropWorkTables).ConfigureAwait(false);
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
