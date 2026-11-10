using Ekom.Controllers;
using Ekom.Models;
using Ekom.Models.Manager;
using Ekom.Repositories;
using Ekom.Services;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Security.Claims;
using Xunit;

namespace Ekom.Tests.Tests;

public sealed class OrderReviewTests
{
    [Fact]
    public async Task RepeatedFailureReusesOpenIssueAndLaterFailurePreservesHistory()
    {
        using var f = new Fixture();
        var first = await f.RaiseAsync();
        var duplicate = await f.Service.RaiseAsync(f.OrderId, " DK ", " FAILED ", "Other title", "Other details");
        Assert.Equal(first.Id, duplicate.Id);
        Assert.Equal(first.Message, duplicate.Message);
        Assert.Single(await f.Service.GetReviewsAsync(f.OrderId));

        var resolved = await f.Service.ResolveAsync(f.OrderId, first.Id, "Anna", " Checked invoice ");
        Assert.NotNull(resolved!.ResolvedUtc);
        Assert.Equal("Checked invoice", resolved.ResolutionComment);
        Assert.Empty(await f.Service.GetReviewsAsync(f.OrderId));
        var next = await f.RaiseAsync();
        Assert.NotEqual(first.Id, next.Id);
        Assert.Equal(2, (await f.Service.GetReviewsAsync(f.OrderId, includeResolved: true)).Count);
        Assert.Single(await f.Service.GetReviewsAsync(f.OrderId));
    }

    [Fact]
    public async Task ResolveIsIdempotentAndCannotResolveAnotherOrdersIssue()
    {
        using var f = new Fixture();
        var review = await f.RaiseAsync();
        Assert.Null(await f.Service.ResolveAsync(Guid.NewGuid(), review.Id, "Wrong order"));
        var first = await f.Service.ResolveAsync(f.OrderId, review.Id, "Anna");
        var second = await f.Service.ResolveAsync(f.OrderId, review.Id, "Other user", "Overwrite");
        Assert.Equal(first!.ResolvedUtc, second!.ResolvedUtc);
        Assert.Equal("Anna", second.ResolvedBy);
        Assert.Null(second.ResolutionComment);
    }

    [Fact]
    public async Task SearchFlagsAndFiltersOrdersWithoutMultiplyingCountsOrTotals()
    {
        using var f = new Fixture();
        var first = await f.RaiseAsync();
        var second = await f.Service.RaiseAsync(f.OrderId, "shipping", "failed", "Label failed", "Timeout");
        f.AddOrder("main");
        var outsideStore = f.AddOrder("other");
        await f.Service.RaiseAsync(outsideStore, "dk", "failed", "Failed", "Timeout");
        var outsideDate = f.AddOrder("main", DateTime.UtcNow.AddYears(-1));
        await f.Service.RaiseAsync(outsideDate, "dk", "failed", "Failed", "Timeout");

        var all = await f.SearchAsync(null);
        Assert.Equal(2, all.Count);
        Assert.True(Assert.IsAssignableFrom<OrderListItem>(all.Orders.Single(x => x.UniqueId == f.OrderId)).NeedsReview);
        Assert.False(Assert.IsAssignableFrom<OrderListItem>(all.Orders.Single(x => x.UniqueId != f.OrderId)).NeedsReview);
        var flagged = await f.SearchAsync(true);
        Assert.Equal(flagged.Count, (await f.SearchAsync(null, "NeedsReview")).Count);
        Assert.Equal(f.OrderId, Assert.Single(await f.ExportReviewsAsync()).UniqueId);
        Assert.Equal(1, flagged.Count);
        Assert.Equal(f.OrderId, Assert.Single(flagged.Orders).UniqueId);
        Assert.Equal(1, (await f.SearchAsync(false)).Count);
        Assert.Equal(flagged.GrandTotal, flagged.AverageAmount);

        await f.Service.ResolveAsync(f.OrderId, first.Id, "Anna");
        Assert.Equal(1, (await f.SearchAsync(true)).Count);
        await f.Service.ResolveAsync(f.OrderId, second.Id, "Anna");
        Assert.Equal(0, (await f.SearchAsync(true)).Count);
        Assert.All((await f.SearchAsync(null)).Orders, x => Assert.False(Assert.IsAssignableFrom<OrderListItem>(x).NeedsReview));
    }

    [Fact]
    public async Task NotesStayOpenAndResolutionLogsOnceWithAuthenticatedReviewer()
    {
        using var f = new Fixture();
        var review = await f.RaiseAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.ReviewAsync(f.OrderId, review.Id, "Anna", " ", false));
        await f.Service.ReviewAsync(f.OrderId, review.Id, "Anna", "Checked provider", false);
        Assert.Single(await f.Service.GetReviewsAsync(f.OrderId));
        await f.Service.ResolveAsync(f.OrderId, review.Id, "Anna", "Invoice corrected");
        await f.Service.ResolveAsync(f.OrderId, review.Id, "Other", "Duplicate");
        using var db = f.Factory.GetDatabase();
        var logs = await db.OrderActivityLog.ToListAsync();
        Assert.Equal(2, logs.Count);
        Assert.All(logs, log => Assert.Equal("Anna", log.UserName));
        Assert.Contains(logs, log => log.Log.Contains("Checked provider"));
        Assert.Contains(logs, log => log.Log.Contains("Invoice corrected"));
        Assert.Empty(await f.Service.GetReviewsAsync(f.OrderId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StaleNoteReturnsConflictWithoutChangingResolvedReview(bool resolve)
    {
        using var f = new Fixture();
        var review = await f.RaiseAsync();
        await f.Service.ResolveAsync(f.OrderId, review.Id, "Anna", "Resolved externally");
        var access = new Mock<IManagerAccessService>();
        access.Setup(x => x.CanAccessManager()).Returns(true);
        access.Setup(x => x.CanAccessStore("main")).Returns(true);
        var security = new Mock<ISecurityService>();
        security.Setup(x => x.GetAuthenticatedUserName()).Returns("Other user");
        var controller = new EkomOrderReviewsController(f.Service, access.Object, f.Factory, security.Object);
        var result = await controller.ResolveAsync(f.OrderId, review.Id,
            new ResolveOrderReviewRequest { Comment = "Unsaved investigation", Resolved = resolve });
        Assert.IsType<ConflictObjectResult>(result);
        var saved = Assert.Single(await f.Service.GetReviewsAsync(f.OrderId, includeResolved: true));
        Assert.Equal("Anna", saved.ResolvedBy);
        Assert.Equal("Resolved externally", saved.ResolutionComment);
        using var db = f.Factory.GetDatabase();
        Assert.Single(await db.OrderActivityLog.ToListAsync());
    }

    [Fact]
    public async Task BackofficeIdentityCanReviewWithoutHttpPrincipalName()
    {
        using var f = new Fixture();
        var review = await f.RaiseAsync();
        var access = new Mock<IManagerAccessService>();
        access.Setup(x => x.CanAccessManager()).Returns(true);
        access.Setup(x => x.CanAccessStore("main")).Returns(true);
        var security = new Mock<ISecurityService>();
        security.Setup(x => x.GetAuthenticatedUserName()).Returns("backoffice-user");
        var controller = new EkomOrderReviewsController(f.Service, access.Object, f.Factory, security.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
        var result = Assert.IsType<OkObjectResult>(await controller.ResolveAsync(f.OrderId, review.Id, new ResolveOrderReviewRequest { Comment = "Checked" }));
        Assert.Equal("backoffice-user", Assert.IsType<OrderReview>(result.Value).ResolvedBy);
    }

    [Fact]
    public async Task HistoryIsPaginatedAndValidatesInput()
    {
        using var f = new Fixture();
        await f.RaiseAsync();
        await f.Service.RaiseAsync(f.OrderId, "shipping", "failed", "Failed", "Details");
        var first = Assert.Single(await f.Service.GetReviewsAsync(f.OrderId, take: 1));
        var second = Assert.Single(await f.Service.GetReviewsAsync(f.OrderId, skip: 1, take: 1));
        Assert.NotEqual(first.Id, second.Id);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => f.Service.GetReviewsAsync(f.OrderId, take: 201));
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.RaiseAsync(f.OrderId, " ", "x", "Title", "Message"));
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.RaiseAsync(f.OrderId, "dk", "x", new string('x', 201), "Message"));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => f.Service.RaiseAsync(Guid.NewGuid(), "dk", "x", "Title", "Message"));
        Assert.Equal(2, (await f.Service.GetReviewsAsync(f.OrderId)).Count);
    }

    [Fact]
    public async Task EndpointsEnforceStoreAccessAndUseAuthenticatedReviewer()
    {
        using var f = new Fixture();
        var review = await f.RaiseAsync();
        var access = new Mock<IManagerAccessService>();
        access.Setup(x => x.CanAccessManager()).Returns(true);
        access.Setup(x => x.CanAccessStore("main")).Returns(false);
        var controller = new EkomOrderReviewsController(f.Service, access.Object, f.Factory)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "Anna") }, "Test")),
                },
            },
        };
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(await controller.GetAsync(f.OrderId)).StatusCode);
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(await controller.RaiseAsync(f.OrderId, new RaiseOrderReviewRequest())).StatusCode);
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(await controller.ResolveAsync(f.OrderId, review.Id, new ResolveOrderReviewRequest())).StatusCode);
        Assert.Null(Assert.Single(await f.Service.GetReviewsAsync(f.OrderId)).ResolvedUtc);

        access.Setup(x => x.CanAccessStore("main")).Returns(true);
        Assert.IsType<NotFoundResult>(await controller.GetAsync(Guid.NewGuid()));
        Assert.IsType<BadRequestObjectResult>(await controller.RaiseAsync(f.OrderId, new RaiseOrderReviewRequest()));
        Assert.IsType<NotFoundResult>(await controller.ResolveAsync(f.OrderId, Guid.NewGuid(), new ResolveOrderReviewRequest()));
        Assert.IsType<OkObjectResult>(await controller.GetAsync(f.OrderId));
        var result = Assert.IsType<OkObjectResult>(await controller.ResolveAsync(f.OrderId, review.Id, new ResolveOrderReviewRequest { Comment = "Checked" }));
        var resolved = Assert.IsType<OrderReview>(result.Value);
        Assert.Equal("Anna", resolved.ResolvedBy);
        Assert.Equal("Checked", resolved.ResolutionComment);
    }

    [Fact]
    public async Task SchemaEnsureIsRepeatableAndPreservesReviews()
    {
        using var f = new Fixture();
        var review = await f.RaiseAsync();
        f.Schema.EnsureOrderReviewsTable();
        f.Schema.EnsureOrderReviewsTable();
        Assert.Equal(review.Id, Assert.Single(await f.Service.GetReviewsAsync(f.OrderId)).Id);
        using var db = f.Factory.GetDatabase();
        Assert.Equal(new[] { "OrderId", "ResolvedUtc" }, db.Query<string>("SELECT name FROM pragma_index_info('IX_EkomOrderReviews_OrderId_ResolvedUtc') ORDER BY seqno").ToArray());
    }

    private sealed class Fixture : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ManagerRepository _manager;
        public Fixture()
        {
            string connectionString = $"Data Source=reviews-{Guid.NewGuid():N};Mode=Memory;Cache=Shared;Pooling=False";
            _connection = new SqliteConnection(connectionString);
            _connection.Open();
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:umbracoDbDSN"] = connectionString,
                ["ConnectionStrings:umbracoDbDSN_ProviderName"] = "Microsoft.Data.Sqlite",
            }).Build();
            Factory = new DatabaseFactory(config, Mock.Of<IHostEnvironment>(x => x.ContentRootPath == AppContext.BaseDirectory));
            Schema = new DatabaseService(Factory, NullLogger<DatabaseService>.Instance, new StockReservationReadiness());
            using var db = Factory.GetDatabase();
            db.CreateTable<OrderData>();
            db.CreateTable<OrderActivityLog>();
            Schema.EnsureOrderReviewsTable();
            Service = new OrderReviewService(Factory);
            var stores = new Mock<IStoreService>();
            stores.Setup(x => x.GetStoreByAlias("main")).Returns(Mock.Of<IStore>(x => x.Currency == new CurrencyModel { CurrencyValue = "en-US" }));
            _manager = new ManagerRepository(NullLogger<ManagerRepository>.Instance, new Configuration(config), Factory, stores.Object);
            OrderId = AddOrder("main");
        }

        public Guid OrderId { get; }
        public DatabaseFactory Factory { get; }
        public DatabaseService Schema { get; }
        public IOrderReviewService Service { get; }
        public Task<OrderReview> RaiseAsync() => Service.RaiseAsync(OrderId, "dk", "failed", "Accounting failed", "Timeout");
        public Task<OrderListData> SearchAsync(bool? needsReview, string view = "AllOrders") => _manager.SearchOrdersAsync(DateTime.UtcNow.Date, DateTime.UtcNow.Date,
            "", "main", view, "", "", "", "", "", "", "", "", "1", "30", needsReview: needsReview);
        public Task<List<OrderData>> ExportReviewsAsync() => _manager.GetOrdersForExportAsync(DateTime.UtcNow.Date, DateTime.UtcNow.Date,
            "", "main", "NeedsReview", "", "", "", "", "", "", "", "", false);

        public Guid AddOrder(string store, DateTime? date = null)
        {
            var id = Guid.NewGuid();
            using var db = Factory.GetDatabase();
            db.Insert(new OrderData
            {
                UniqueId = id,
                OrderStatusCol = "Pending",
                StoreAlias = store,
                Currency = "en-US",
                CreateDate = date ?? DateTime.UtcNow,
                TotalAmount = 100,
                OrderInfo = "Invalid JSON deliberately: reviews must not deserialize the order.",
            });
            return id;
        }

        public void Dispose() => _connection.Dispose();
    }
}
