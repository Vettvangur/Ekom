using Ekom.Models;
using Ekom.Models.Manager;
using Ekom.Services;
using LinqToDB.Data;
using LinqToDB.Interceptors;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using System.Data.Common;

namespace Ekom.Repositories;

public sealed class CustomerRepository(DatabaseFactory databaseFactory, IOptions<EkomOptions> options)
{
    internal const string Qualified = "OrderStatusCol IN ('ReadyForDispatch','OfflinePayment','ReadyForDispatchWhenStockArrives','Dispatched','Closed','ReadyForPickup')";
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

    internal string BuildCte(string orderStatus)
    {
        string date = orderStatus is "ReadyForDispatch" or "Dispatched" ? "PaidDate" : "CreateDate";
        string gap = databaseFactory.IsSqlite ? "(julianday(MAX(CreateDate)) - julianday(MIN(CreateDate)))" : "(DATEDIFF_BIG(second, MIN(CreateDate), MAX(CreateDate)) / 86400.0)";
        return $@"WITH Q AS (
 SELECT {Columns}, {Key} AS [Identity] FROM EkomOrders WHERE StoreAlias = @store AND {Qualified}
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
 AND (@status IN ('CompletedOrders','AllOrders','') OR OrderStatusCol = @status))
 AND (@segment = 'all' OR (@segment = 'new' AND G.Orders = 1) OR (@segment = 'returning' AND G.Orders > 1))
 AND (@query = '' OR EXISTS (SELECT 1 FROM Q WHERE Q.[Identity] = G.[Identity] AND
 ({NormalizeColumn("CustomerName")} LIKE @pattern ESCAPE '~' OR {NormalizeColumn("CustomerEmail")} LIKE @pattern ESCAPE '~' OR {NormalizeColumn("CustomerUsername")} LIKE @pattern ESCAPE '~')))
) ";
    }

    private static string Sort(string sort) => sort switch
    {
        "oldest" => "FirstOrder ASC",
        "lastOrder" => "LastOrder DESC",
        "orders" => "Orders DESC",
        "name" => "Name ASC",
        _ => "FirstOrder DESC",
    };

    public async Task<CustomerSearchResult> SearchAsync(string store, DateTime start, DateTime end, string status, string query, string segment, string sort, int page, int pageSize, CancellationToken ct = default)
    {
        await using var db = OpenDatabase();
        var args = Arguments(store, start, end, status, query, segment, page, pageSize);
        string cte = BuildCte(status);
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
        var pageSpend = await db.QueryToListAsync<CustomerSpend>(profiles + "SELECT Q.[Identity], Q.Currency, SUM(Q.TotalAmount) AS Total, COUNT(*) AS Orders FROM Q JOIN PageKeys P ON P.[Identity] = Q.[Identity] GROUP BY Q.[Identity], Q.Currency", ct, args).ConfigureAwait(false);
        var spendByIdentity = pageSpend.ToLookup(x => x.Identity, StringComparer.Ordinal);
        foreach (var customer in customers)
        {
            customer.Spend = spendByIdentity[customer.Identity].ToArray();
        }
        var totals = await db.QueryToListAsync<CustomerTotals>(cte + "SELECT COUNT(*) AS Customers, COALESCE(SUM(CASE WHEN Orders > 1 THEN 1 ELSE 0 END),0) AS [Returning], COALESCE(SUM(Orders),0) AS Orders, AVG(AverageReorderDays) AS AverageReorderDays FROM Selected", ct, args).ConfigureAwait(false);
        var spend = await db.QueryToListAsync<CustomerSpend>(cte + "SELECT Currency, SUM(TotalAmount) AS Total, COUNT(*) AS Orders, COUNT(DISTINCT Q.[Identity]) AS Customers FROM Q INNER JOIN Selected S ON Q.[Identity] = S.[Identity] GROUP BY Currency", ct, args).ConfigureAwait(false);
        string date = status is "ReadyForDispatch" or "Dispatched" ? "PaidDate" : "CreateDate";
        var missing = await db.ExecuteAsync<int>($"SELECT COUNT(*) FROM EkomOrders WHERE StoreAlias = @store AND {Qualified} AND {Key} = '' AND {date} >= @start AND {date} < @end AND (@status IN ('CompletedOrders','AllOrders','') OR OrderStatusCol = @status)", ct, args).ConfigureAwait(false);
        return new CustomerSearchResult { Customers = customers, Totals = totals[0], Spend = spend, MissingIdentifiers = missing, IdentityField = IdentityField, Page = page, PageSize = pageSize, TotalPages = (int)Math.Ceiling((double)totals[0].Customers / pageSize) };
    }

    internal static object Arguments(string store, DateTime start, DateTime end, string status, string query, string segment, int page, int pageSize)
        => new { store, start = start.Date, end = end.Date.AddDays(1), status, query, pattern = "%" + query.ToUpperInvariant().Replace("~", "~~", StringComparison.Ordinal).Replace("%", "~%", StringComparison.Ordinal).Replace("_", "~_", StringComparison.Ordinal).Replace("[", "~[", StringComparison.Ordinal) + "%", segment, take = pageSize, skip = (page - 1) * pageSize };

    private async Task<List<CustomerSpend>> SpendAsync(DataConnection db, string store, string identity, CancellationToken ct)
        => await db.QueryToListAsync<CustomerSpend>($"SELECT Currency, SUM(TotalAmount) AS Total, COUNT(*) AS Orders FROM EkomOrders WHERE StoreAlias = @store AND {Key} = @identity AND {Qualified} GROUP BY Currency", ct, new { store, identity }).ConfigureAwait(false);

    public async Task<object?> DetailAsync(string store, string identity, int page, bool other, CancellationToken ct = default)
    {
        await using var db = OpenDatabase();
        identity = identity.Trim(' ').ToUpperInvariant();
        var args = new { store, identity, take = 20, skip = (page - 1) * 20 };
        string where = $"StoreAlias = @store AND {Key} = @identity";
        var summaries = await db.QueryToListAsync<CustomerSummary>($"SELECT {Key} AS [Identity], COUNT(*) AS Orders, MIN(CreateDate) AS FirstOrder, MAX(CreateDate) AS LastOrder FROM EkomOrders WHERE {where} AND {Qualified} GROUP BY {Key}", ct, args).ConfigureAwait(false);
        var summary = summaries.FirstOrDefault();
        if (summary == null) return null;
        summary.Spend = await SpendAsync(db, store, identity, ct).ConfigureAwait(false);
        string scope = where + (other ? $" AND NOT ({Qualified})" : $" AND {Qualified}");
        var orders = await db.QueryToListAsync<OrderData>($"SELECT {Columns} FROM EkomOrders WHERE {scope} ORDER BY CreateDate DESC, ReferenceId DESC {PageSql}", ct, args).ConfigureAwait(false);
        var count = await db.ExecuteAsync<int>($"SELECT COUNT(*) FROM EkomOrders WHERE {scope}", ct, args).ConfigureAwait(false);
        string top = databaseFactory.IsSqlite ? "" : "TOP 1 ";
        string limit = databaseFactory.IsSqlite ? " LIMIT 1" : "";
        var snapshots = await db.QueryToListAsync<OrderData>($"SELECT {top}{Columns}, OrderInfo FROM EkomOrders WHERE {where} AND {Qualified} ORDER BY CreateDate DESC, ReferenceId DESC{limit}", ct, args).ConfigureAwait(false);
        var snapshot = snapshots.FirstOrDefault();
        if (snapshot == null) return null;
        summary.Name = snapshot.CustomerName;
        summary.Email = snapshot.CustomerEmail;
        summary.Username = snapshot.CustomerUsername;
        summary.ShippingCountry = snapshot.ShippingCountry;
        return new { Customer = summary, Orders = orders, Count = count, Page = page, TotalPages = (int)Math.Ceiling(count / 20.0), Snapshot = CustomerSnapshot.Parse(snapshot), IdentityField };
    }
}
