using Ekom.Models;
using Ekom.Repositories;
using Ekom.Services;
using Ekom.Utilities;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Ekom.Tests.Tests;

public sealed class ManagerCustomerSqlServerTests(ITestOutputHelper output)
{
    [SqlServerFact]
    public async Task SearchMatchesDisplayedCustomersAndRetainsScope()
    {
        foreach (string collation in new[] { "SQL_Latin1_General_CP1_CI_AS", "Latin1_General_100_BIN2", "Icelandic_100_CI_AS" })
        {
            await VerifyAsync(collation);
        }
    }

    private async Task VerifyAsync(string collation)
    {
        string database = "EkomCustomerTest_" + Guid.NewGuid().ToString("N");
        var masterBuilder = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("EKOM_TEST_SQLSERVER"))
        {
            InitialCatalog = "master", Pooling = false,
        };
        await using var master = new SqlConnection(masterBuilder.ConnectionString);
        await master.OpenAsync();
        await using var command = master.CreateCommand();
        command.CommandText = $"CREATE DATABASE [{database}] COLLATE {collation}";
        await command.ExecuteNonQueryAsync();
        try
        {
            var builder = new SqlConnectionStringBuilder(masterBuilder.ConnectionString) { InitialCatalog = database };
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:umbracoDbDSN"] = builder.ConnectionString,
                ["ConnectionStrings:umbracoDbDSN_ProviderName"] = "Microsoft.Data.SqlClient",
            }).Build();
            var factory = new DatabaseFactory(config, Mock.Of<IHostEnvironment>(x => x.ContentRootPath == AppContext.BaseDirectory));
            var repository = new CustomerRepository(factory, Options.Create(new EkomOptions()));
            await using var db = factory.GetDatabase();
            db.CreateTable<OrderData>();
            var day = new DateTime(2026, 11, 9);
            void Add(string email, string name, string username = "", string store = "main", int days = 0, OrderStatus status = OrderStatus.Dispatched)
                => db.Insert(new OrderData { UniqueId = Guid.NewGuid(), CustomerEmail = email, CustomerName = name,
                    CustomerUsername = username, StoreAlias = store, Currency = "ISK", CreateDate = day.AddDays(days),
                    PaidDate = day.AddDays(days), OrderStatus = status, TotalAmount = 100m, OrderNumber = "test", OrderInfo = "{}" });
            Add("dogg@kalon.is", "Dögg Ívarsdóttir", "dogg-user");
            Add("other@example.test", "Other");
            Add("literal%_[@example.test", "Literal");
            Add("hidden@example.test", "Hidden", store: "other");
            Add("old@example.test", "Old", days: -10);
            Add("incomplete@example.test", "Incomplete", status: OrderStatus.Incomplete);
            // Prove neither path can read OrderInfo.
            db.Execute("ALTER TABLE EkomOrders DROP COLUMN OrderInfo");
            var visible = await repository.SearchAsync("main", day, day, "CompletedOrders", "", "all", "newest", 1, 20);
            Assert.Equal(3, visible.Customers.Count);
            Assert.Contains(visible.Customers, c => c.Email == "dogg@kalon.is");
            foreach (string query in new[] { "dogg@kalon.is", "dogg", "DOGG@KALON.IS", "Dögg", "Ívarsdóttir", "dogg-user", " dogg@kalon.is " })
            {
                var found = await repository.SearchAsync("main", day, day, "CompletedOrders", query, "all", "newest", 1, 20);
                Assert.Equal("dogg@kalon.is", Assert.Single(found.Customers).Email);
                Assert.Equal(1, found.Totals.Customers);
                int oldCount = await LegacyCountAsync(db, repository, day, query);
                output.WriteLine($"Collation={collation}; Query={query}; old matches={oldCount}; new matches={found.Customers.Count}");
            }
            foreach (string query in new[] { "hidden@example.test", "old@example.test", "incomplete@example.test", "absent" })
                Assert.Empty((await repository.SearchAsync("main", day, day, "CompletedOrders", query, "all", "newest", 1, 20)).Customers);
            foreach (string query in new[] { "%", "_", "[", "%_[" })
                Assert.Equal("literal%_[@example.test", Assert.Single((await repository.SearchAsync("main", day, day, "CompletedOrders", query, "all", "newest", 1, 20)).Customers).Email);
            output.WriteLine($"Scope, wildcard literals, and no-OrderInfo checks passed for {collation}.");
        }
        finally
        {
            command.CommandText = $"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]";
            await command.ExecuteNonQueryAsync();
        }
    }

    private static Task<int> LegacyCountAsync(DataConnection db, CustomerRepository repository, DateTime day, string query)
    {
        string cte = repository.BuildCte("CompletedOrders");
        foreach (string column in new[] { "CustomerName", "CustomerEmail", "CustomerUsername" })
            cte = cte.Replace($"CHARINDEX(@query, COALESCE(Q.{column}, '') COLLATE Latin1_General_100_CI_AS) > 0",
                $"UPPER(LTRIM(RTRIM(COALESCE({column}, ''))) COLLATE Latin1_General_100_CI_AS) COLLATE Latin1_General_100_BIN2 LIKE @pattern ESCAPE '~'", StringComparison.Ordinal);
        Assert.DoesNotContain("CHARINDEX", cte, StringComparison.Ordinal);
        string pattern = "%" + query.ToUpperInvariant().Replace("~", "~~", StringComparison.Ordinal).Replace("%", "~%", StringComparison.Ordinal)
            .Replace("_", "~_", StringComparison.Ordinal).Replace("[", "~[", StringComparison.Ordinal) + "%";
        return db.ExecuteAsync<int>(cte + "SELECT COUNT(*) FROM Selected", new { store = "main", start = day, end = day.AddDays(1), status = "CompletedOrders", query, pattern, segment = "all" });
    }
}

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("EKOM_TEST_SQLSERVER")))
            Skip = "Set EKOM_TEST_SQLSERVER to an isolated SQL Server instance with CREATE DATABASE permission.";
    }
}
