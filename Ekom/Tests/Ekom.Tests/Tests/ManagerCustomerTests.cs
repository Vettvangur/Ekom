using Ekom.Controllers;
using Ekom.Models;
using Ekom.Models.Manager;
using Ekom.Repositories;
using Ekom.Services;
using Ekom.Utilities;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Ekom.Tests.Tests;

public sealed class ManagerCustomerTests
{
    [Fact]
    public async Task PeriodSelectsCustomersButKeepsLifetimeHistoryWithoutOrderInfo()
    {
        using var f = new Fixture();
        f.Add("one@example.com", new DateTime(2026, 1, 1), 100);
        f.Add(" ONE@example.com ", new DateTime(2026, 2, 1), 200);
        f.Add("outside@example.com", new DateTime(2025, 12, 1), 999);
        using (var db = f.Factory.GetDatabase()) db.Execute("ALTER TABLE EkomOrders DROP COLUMN OrderInfo");

        var result = await f.Search(new DateTime(2026, 2, 1), new DateTime(2026, 2, 1));

        var customer = Assert.Single(result.Customers);
        Assert.Equal(2, customer.Orders);
        Assert.Equal(300, Assert.Single(customer.Spend).Total);
        Assert.Equal(31, customer.AverageReorderDays);
        Assert.Equal(100, result.Totals.ReturningPercent);
        Assert.Equal(new DateTime(2026, 1, 1), customer.FirstOrder);
    }

    [Fact]
    public async Task StoreMissingIdentifiersCurrencyAndExcludedStatusesRemainSeparate()
    {
        using var f = new Fixture();
        var day = new DateTime(2026, 2, 1);
        f.Add("one@example.com", day, 100, currency: "USD");
        f.Add("one@example.com", day, 200, currency: "EUR");
        f.Add("one@example.com", day, 9000, store: "other");
        f.Add("one@example.com", day, 8000, status: OrderStatus.Incomplete);
        f.Add("  ", day, 10);
        f.Add("", day.AddDays(-1), 10);

        var result = await f.Search(day, day);

        var customer = Assert.Single(result.Customers);
        Assert.Equal(2, customer.Orders);
        Assert.Equal(2, customer.Spend.Count);
        Assert.Equal(1, result.MissingIdentifiers);
        Assert.Equal(0, customer.AverageReorderDays);
        Assert.DoesNotContain(result.Spend, x => x.Total > 200);
    }

    [Fact]
    public async Task UsernameModeDoesNotFallBackToEmailAndHandlesUnicodeCase()
    {
        using var f = new Fixture("CustomerUsername");
        var day = new DateTime(2026, 1, 1);
        f.Add("a@example.com", day, 100, username: " ÞÓR ");
        f.Add("b@example.com", day.AddDays(1), 200, username: "þór");
        f.Add("c@example.com", day, 300, username: "");

        var result = await f.Search(day, day.AddDays(1));

        Assert.Equal("ÞÓR", Assert.Single(result.Customers).Identity);
        Assert.Equal(2, result.Customers[0].Orders);
        Assert.Equal(1, result.MissingIdentifiers);
    }

    [Fact]
    public async Task PaidStatusesUsePaidDateWithInclusiveEndDate()
    {
        using var f = new Fixture();
        var day = new DateTime(2026, 2, 1);
        f.Add("late@example.com", day.AddDays(-20), 100, paid: day.AddHours(23).AddMinutes(59));
        f.Add("tomorrow@example.com", day, 100, paid: day.AddDays(1));
        var result = await f.Search(day, day, status: "Dispatched");
        Assert.Equal("LATE@EXAMPLE.COM", Assert.Single(result.Customers).Identity);
    }

    [Fact]
    public async Task PagingSearchSegmentsAndLatestNameAreDeterministic()
    {
        using var f = new Fixture();
        var day = new DateTime(2026, 1, 1);
        f.Add("a@example.com", day.AddDays(2), 100, name: "Newest");
        f.Add("a@example.com", day, 100, name: "Older imported later");
        f.Add("b@example.com", day, 100, name: "100% customer");
        var returning = await f.Search(day, day.AddDays(5), segment: "returning");
        Assert.Equal("Newest", Assert.Single(returning.Customers).Name);
        var literal = await f.Search(day, day.AddDays(5), query: "%");
        Assert.Equal("B@EXAMPLE.COM", Assert.Single(literal.Customers).Identity);
        var first = await f.Search(day, day.AddDays(5), pageSize: 1);
        var second = await f.Search(day, day.AddDays(5), page: 2, pageSize: 1);
        Assert.NotEqual(first.Customers[0].Identity, second.Customers[0].Identity);
        Assert.Equal(2, first.TotalPages);
    }

    [Fact]
    public async Task DetailLoadsOnlyTheMatchingStoreAndCustomerAndToleratesInvalidJson()
    {
        using var f = new Fixture();
        var day = new DateTime(2026, 1, 1);
        f.Add("a@example.com", day, 100, json: "invalid JSON");
        f.Add("a@example.com", day.AddDays(1), 200, store: "other", json: "{}");
        f.Add("b@example.com", day, 100, json: "{}");
        var result = JObject.FromObject((await f.Repository.DetailAsync("main", "a@example.com", 1, false))!);
        Assert.Single((JArray)result["Orders"]!);
        Assert.True(result["Snapshot"]!["Unavailable"]!.Value<bool>());
        Assert.Null(await f.Repository.DetailAsync("main", "missing@example.com", 1, false));
    }

    [Fact]
    public async Task AllCustomerEndpointsEnforceStoreAccessBeforeQuerying()
    {
        using var f = new Fixture();
        var access = new Mock<IManagerAccessService>();
        var controller = new EkomCustomersController(f.Repository, access.Object);
        Assert.IsType<ForbidResult>(await controller.Search("hidden", DateTime.Today, DateTime.Today));
        Assert.IsType<ForbidResult>(await controller.Detail("hidden", "a@example.com"));
        Assert.IsType<ForbidResult>(await controller.Export("hidden", DateTime.Today, DateTime.Today));
    }

    [Fact]
    public async Task ExportUsesTheFilteredCohortWithoutOrderInfoAndEscapesFormulas()
    {
        using var f = new Fixture();
        var day = new DateTime(2026, 1, 1);
        f.Add("a@example.com", day, 100, name: "=1+1");
        f.Add("excluded@example.com", day.AddDays(-1), 100);
        using (var db = f.Factory.GetDatabase()) db.Execute("ALTER TABLE EkomOrders DROP COLUMN OrderInfo");
        var access = new Mock<IManagerAccessService>();
        access.Setup(x => x.CanAccessStore("main")).Returns(true);
        var controller = new EkomCustomersController(f.Repository, access.Object);
        var file = Assert.IsType<FileContentResult>(await controller.Export("main", day, day));
        var csv = System.Text.Encoding.UTF8.GetString(file.FileContents);
        Assert.Contains("'=1+1", csv, StringComparison.Ordinal);
        Assert.Contains("a@example.com", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("excluded@example.com", csv, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidDateRangeAndPagingAreRejected()
    {
        using var f = new Fixture();
        var access = new Mock<IManagerAccessService>();
        access.Setup(x => x.CanAccessStore("main")).Returns(true);
        var controller = new EkomCustomersController(f.Repository, access.Object);
        Assert.IsType<BadRequestObjectResult>(await controller.Search("main", DateTime.Today, DateTime.Today.AddDays(-1)));
        Assert.IsType<BadRequestObjectResult>(await controller.Search("main", DateTime.Today, DateTime.Today, page: 0));
        Assert.IsType<BadRequestObjectResult>(await controller.Detail("main", ""));
    }

    [Fact]
    public void SnapshotProjectsOnlyKnownFieldsAndPreservesExplicitNoConsent()
    {
        var snapshot = CustomerSnapshot.Parse(new OrderData
        {
            OrderInfo = """{"CustomerInformation":{"Customer":{"Phone":"123","Properties":{"secret":"never expose"}}},"Consent":{"Marketing":false},"PaymentProvider":{"Title":"Card","Secret":"never expose"}}""",
        });
        Assert.Equal("123", snapshot.Fields["Phone"]);
        Assert.Equal("No", snapshot.Fields["Marketing consent (order snapshot)"]);
        Assert.DoesNotContain("never expose", string.Join(" ", snapshot.Fields.Values));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string path = Path.Combine(Path.GetTempPath(), $"ekom-customers-{Guid.NewGuid():N}.sqlite");
        public DatabaseFactory Factory { get; }
        public CustomerRepository Repository { get; }
        public Fixture(string identity = "CustomerEmail")
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:umbracoDbDSN"] = $"Data Source={path};Pooling=False",
                ["ConnectionStrings:umbracoDbDSN_ProviderName"] = "Microsoft.Data.Sqlite",
            }).Build();
            var host = new Mock<IHostEnvironment>();
            host.SetupGet(x => x.ContentRootPath).Returns(Path.GetTempPath());
            Factory = new DatabaseFactory(config, host.Object);
            Repository = new CustomerRepository(Factory, Options.Create(new EkomOptions { Manager = new ManagerOptions { Customers = new CustomerManagerOptions { IdentityField = identity } } }));
            using var db = Factory.GetDatabase();
            db.CreateTable<OrderData>();
        }

        public void Add(string email, DateTime date, decimal total, string store = "main", string currency = "USD", OrderStatus status = OrderStatus.Dispatched, string username = "", DateTime? paid = null, string name = "Customer", string json = "{}")
        {
            using var db = Factory.GetDatabase();
            db.Insert(new OrderData { UniqueId = Guid.NewGuid(), CustomerEmail = email, CustomerUsername = username, CustomerName = name, StoreAlias = store, Currency = currency, OrderStatus = status, CreateDate = date, PaidDate = paid, TotalAmount = total, OrderInfo = json, OrderNumber = "test", ShippingCountry = "IS" });
        }

        public Task<CustomerSearchResult> Search(DateTime start, DateTime end, string status = "CompletedOrders", string query = "", string segment = "all", int page = 1, int pageSize = 20)
            => Repository.SearchAsync("main", start, end, status, query, segment, "newest", page, pageSize);

        public void Dispose() => File.Delete(path);
    }
}
