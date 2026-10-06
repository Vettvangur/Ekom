using Ekom.Analytics;
using Ekom.Controllers;
using Ekom.Models;
using Ekom.Services;
using LinqToDB;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Ekom.Tests.Tests;

public sealed class AnalyticsAuthorizationTests
{
    [Fact]
    public async Task ForeignStoreReportsAndRebuildAreDeniedBeforeSchemaOrQueryProcessing()
    {
        using var fixture = new AnalyticsReportDatabase();
        var controller = Controller(fixture, true);
        var filter = fixture.Filter();
        filter.Store = "foreign";
        // Invalid dates and pagination prove authorization precedes validation/query execution.
        filter.End = filter.Start;

        Assert.IsType<ForbidResult>(await controller.Sales(filter, default));
        Assert.IsType<ForbidResult>(await controller.DailySales(filter, default));
        Assert.IsType<ForbidResult>(await controller.Products(filter, page: 0));
        Assert.IsType<ForbidResult>(await controller.Distribution("invalid", filter, default));
        Assert.IsType<ForbidResult>(await controller.Promotions(filter, page: 0));
        Assert.IsType<ForbidResult>(await controller.Orders(filter, page: 0));
        Assert.IsType<ForbidResult>(await controller.StartRebuild(new AnalyticsRebuildRequest("foreign")));
        Assert.IsType<ForbidResult>(await controller.ListJobs("foreign"));

        using var connection = fixture.Factory.GetDbConnection();
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name LIKE 'EkomAnalytics%'";
        Assert.Equal(0L, await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task ForeignOrderRefreshAndJobControlLeaveSourceAndAnalyticsUnchanged()
    {
        using var fixture = new AnalyticsReportDatabase();
        await fixture.InitializeAsync();
        var orderId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        using (var db = fixture.Factory.GetDatabase())
        {
            db.CreateTable<OrderData>();
            db.CreateTable<OrderActivityLog>();
            db.Insert(new OrderData
            {
                UniqueId = orderId, StoreAlias = "foreign", Currency = "en-US",
                OrderStatusCol = "ReadyForDispatch", OrderInfo = "not-json-must-not-be-processed",
            });
            var projection = fixture.NewOrder(42);
            projection.OrderId = orderId;
            projection.StoreAlias = "foreign";
            db.Insert(projection);
            db.Insert(new AnalyticsJobData
            {
                JobId = jobId, StoreAlias = "foreign", Status = "Pending",
                StartedAtUtc = new DateTime(2026, 1, 1), UpdatedAtUtc = new DateTime(2026, 1, 1),
            });
        }
        var controller = Controller(fixture, true);

        Assert.IsType<ForbidResult>(await controller.RefreshOrder(orderId));
        Assert.IsType<ForbidResult>(await controller.GetJob(jobId));
        foreach (var command in new[] { "pause", "resume", "cancel" })
            Assert.IsType<ForbidResult>(await controller.ControlJob(jobId, new AnalyticsJobControlRequest(command)));

        using var verification = fixture.Factory.GetDatabase();
        Assert.Equal("not-json-must-not-be-processed", verification.GetTable<OrderData>().Single().OrderInfo);
        Assert.Empty(verification.GetTable<OrderActivityLog>());
        Assert.Equal(42m, verification.GetTable<AnalyticsOrderData>().Single().GrandTotal);
        var job = verification.GetTable<AnalyticsJobData>().Single();
        Assert.Equal("Pending", job.Status);
        Assert.Null(job.Control);
        Assert.Equal(new DateTime(2026, 1, 1), job.UpdatedAtUtc);
        Assert.Equal(0L, job.ProcessedCount);
    }

    [Fact]
    public async Task ManagerDenialPrecedesOrderAndJobLookups()
    {
        using var fixture = new AnalyticsReportDatabase();
        var controller = Controller(fixture, false);

        Assert.IsType<ForbidResult>(await controller.RefreshOrder(Guid.NewGuid()));
        Assert.IsType<ForbidResult>(await controller.GetJob(Guid.NewGuid()));
        Assert.IsType<ForbidResult>(await controller.ControlJob(Guid.NewGuid(), new AnalyticsJobControlRequest("cancel")));
        Assert.IsType<ForbidResult>(await controller.StartRebuild(new AnalyticsRebuildRequest("main")));
        Assert.IsType<ForbidResult>(await controller.Sales(fixture.Filter(), default));
        Assert.IsType<ForbidResult>(await controller.ListJobs("main"));
    }

    private static EkomAnalyticsController Controller(AnalyticsReportDatabase fixture, bool manager)
    {
        var access = new Mock<IManagerAccessService>();
        access.Setup(x => x.CanAccessManager()).Returns(manager);
        access.Setup(x => x.CanAccessStore(It.IsAny<string?>())).Returns((string? store) => store == "main");
        var projection = fixture.Projection();
        var jobs = new AnalyticsJobService(fixture.Factory, fixture.Schema, projection, fixture.Options,
            NullLogger<AnalyticsJobService>.Instance);
        return new EkomAnalyticsController(access.Object, fixture.Reports, projection, jobs, fixture.Factory,
            fixture.Options, NullLogger<EkomAnalyticsController>.Instance);
    }
}
