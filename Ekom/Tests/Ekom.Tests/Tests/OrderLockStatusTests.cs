using Ekom.Controllers;
using Ekom.Models;
using Ekom.Models.Manager;
using Ekom.Repositories;
using Ekom.Services;
using LinqToDB;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace Ekom.Tests.Tests;

[Collection("Reservations")]
public sealed class OrderLockStatusTests
{
    [Fact]
    public async Task OrderWithoutOwnershipOrReviewHasNoLock()
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();

        var status = await Repository(f).GetOrderLockStatusAsync(f.OrderId);

        Assert.False(status.HasLock);
        Assert.False(status.PaymentOperationLocked);
        Assert.False(status.PreparationLocked);
        Assert.False(status.ReconciliationRequired);
        Assert.Null(status.AttemptId);
        Assert.Null(status.AttemptState);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, true, true)]
    public async Task ReportsIndependentBlockersWithoutLeakingOwnership(bool payment, bool preparation, bool review)
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var attempt = await f.SubmittedAsync();
        using var db = f.Database.Factory.GetDatabase();
        const string secret = "private-owner-capability";
        await db.GetTable<CheckoutPaymentOperationData>().Where(x => x.OrderId == f.OrderId)
            .Set(x => x.Owner, payment ? secret : null)
            .Set(x => x.ReconciliationRequired, review).UpdateAsync();
        await db.GetTable<OrderData>().Where(x => x.UniqueId == f.OrderId)
            .Set(x => x.OrderStatusCol, "Incomplete").UpdateAsync();
        await db.InsertOrReplaceAsync(new CheckoutPreparationData
        {
            OrderId = f.OrderId,
            Owner = preparation ? secret : null,
        });

        var status = await Repository(f).GetOrderLockStatusAsync(f.OrderId);

        Assert.True(status.HasLock);
        Assert.Equal(payment, status.PaymentOperationLocked);
        Assert.Equal(preparation, status.PreparationLocked);
        Assert.Equal(review, status.ReconciliationRequired);
        Assert.Equal(attempt.AttemptId, status.AttemptId);
        Assert.Equal("Submitted", status.AttemptState);
        Assert.DoesNotContain(secret, JsonConvert.SerializeObject(status), StringComparison.Ordinal);
        Assert.Equal("Incomplete", (await f.ReadAsync()).OrderStatusCol);
    }

    [Fact]
    public async Task ReportsAttemptReconciliationEvenWithoutOperationFlagAndRefreshesAfterOwnerClears()
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        await f.SubmittedAsync();
        using var db = f.Database.Factory.GetDatabase();
        await db.GetTable<CheckoutPaymentAttemptData>()
            .Set(x => x.State, CheckoutPaymentAttemptState.ReconciliationRequired).UpdateAsync();
        await db.GetTable<CheckoutPaymentOperationData>().Set(x => x.Owner, "owner").UpdateAsync();
        var repository = Repository(f);
        Assert.True((await repository.GetOrderLockStatusAsync(f.OrderId)).PaymentOperationLocked);

        await db.GetTable<CheckoutPaymentOperationData>().Set(x => x.Owner, (string?)null).UpdateAsync();
        var refreshed = await repository.GetOrderLockStatusAsync(f.OrderId);

        Assert.False(refreshed.PaymentOperationLocked);
        Assert.True(refreshed.ReconciliationRequired);
        Assert.True(refreshed.HasLock);
        Assert.Equal("ReconciliationRequired", refreshed.AttemptState);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LockStatusEndpointAuthorizesTheOrdersStore(bool allowed)
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var access = new Mock<IManagerAccessService>();
        access.Setup(x => x.CanAccessStore("main")).Returns(allowed);
        var controller = new EkomManagerController(Repository(f), access.Object,
            Mock.Of<INodeService>(), Mock.Of<IOrderActivityLogService>(), Mock.Of<IOrderManagerActionService>(),
            null!, NullLogger<EkomManagerController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        var result = await controller.GetOrderLockStatusAsync(f.OrderId);

        if (allowed)
            Assert.IsType<OrderLockStatus>(Assert.IsType<OkObjectResult>(result).Value);
        else
            Assert.IsType<ForbidResult>(result);
    }

    private static ManagerRepository Repository(CheckoutPaymentAttemptTests.Fixture f)
        => new(NullLogger<ManagerRepository>.Instance,
            new Configuration(new ConfigurationBuilder().Build()), f.Database.Factory, Mock.Of<IStoreService>());
}
