using Ekom.Controllers;
using Ekom.Models;
using Ekom.Models.Manager;
using Ekom.Repositories;
using Ekom.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Ekom.Tests.Tests;

[Collection("Reservations")]
public sealed class OrderDrawerTests
{
    [Fact]
    public async Task DrawerIsForbiddenForAnotherStoreAndLoadsNoSection()
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var access = new Mock<IManagerAccessService>();
        access.Setup(x => x.CanAccessStore("main")).Returns(false);
        var logs = new Mock<IOrderActivityLogService>(MockBehavior.Strict);
        var actions = new Mock<IOrderManagerActionService>(MockBehavior.Strict);
        var recovery = new Mock<IOrderManagerGiftcardRecoveryService>(MockBehavior.Strict);
        var reviews = new Mock<IOrderReviewService>(MockBehavior.Strict);

        var result = await Controller(f, access.Object, logs.Object, actions.Object)
            .GetOrderDrawerAsync(f.OrderId, recovery.Object, reviews.Object);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task DrawerReturnsEverySectionAndPagesThroughOpenReviews()
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var log = new[] { new OrderActivityLogEntry() };
        var action = new[] { new OrderManagerAction { Key = "print", Label = "Print" } };
        var attempts = new[] { new GiftcardRecoveryView() };
        var logs = new Mock<IOrderActivityLogService>();
        logs.Setup(x => x.GetOrderLogsAsync(f.OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(log);
        var actions = new Mock<IOrderManagerActionService>();
        actions.Setup(x => x.GetActionsAsync(It.IsAny<IOrderInfo>(), It.IsAny<CancellationToken>())).ReturnsAsync(action);
        var recovery = new Mock<IOrderManagerGiftcardRecoveryService>();
        recovery.Setup(x => x.GetGiftcardRecoveryAsync(f.OrderId, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(attempts);
        var reviews = new Mock<IOrderReviewService>();
        reviews.Setup(x => x.GetReviewsAsync(f.OrderId, false, 0, 200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(0, 200).Select(_ => new OrderReview()).ToList());
        reviews.Setup(x => x.GetReviewsAsync(f.OrderId, false, 200, 200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OrderReview> { new() });

        var drawer = await Controller(f, AllowMain(), logs.Object, actions.Object)
            .LoadOrderDrawerSectionsAsync(f.OrderId, Mock.Of<IOrderInfo>(), recovery.Object, reviews.Object);

        Assert.Same(log, drawer.Logs);
        Assert.Same(action, drawer.Actions);
        Assert.NotNull(drawer.LockStatus);
        Assert.Same(attempts, drawer.GiftcardRecovery);
        Assert.Equal(201, drawer.Reviews!.Count);
        actions.Verify(x => x.GetActionsAsync(It.IsAny<IOrderInfo>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FailingSectionIsNullAndTheOtherSectionsStillLoad()
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var logs = new Mock<IOrderActivityLogService>();
        logs.Setup(x => x.GetOrderLogsAsync(f.OrderId, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("logs"));
        var actions = new Mock<IOrderManagerActionService>();
        actions.Setup(x => x.GetActionsAsync(It.IsAny<IOrderInfo>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<OrderManagerAction>());
        var recovery = new Mock<IOrderManagerGiftcardRecoveryService>();
        recovery.Setup(x => x.GetGiftcardRecoveryAsync(f.OrderId, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("recovery"));
        var reviews = new Mock<IOrderReviewService>();
        reviews.Setup(x => x.GetReviewsAsync(f.OrderId, false, 0, 200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OrderReview>());

        var drawer = await Controller(f, AllowMain(), logs.Object, actions.Object)
            .LoadOrderDrawerSectionsAsync(f.OrderId, Mock.Of<IOrderInfo>(), recovery.Object, reviews.Object);

        Assert.Null(drawer.Logs);
        Assert.NotNull(drawer.Actions);
        Assert.NotNull(drawer.LockStatus);
        Assert.Null(drawer.GiftcardRecovery);
        Assert.Empty(drawer.Reviews!);
    }

    private static IManagerAccessService AllowMain()
    {
        var access = new Mock<IManagerAccessService>();
        access.Setup(x => x.CanAccessStore("main")).Returns(true);
        access.Setup(x => x.GetAllowedStoreAliases()).Returns(new[] { "main" });
        return access.Object;
    }

    private static EkomManagerController Controller(CheckoutPaymentAttemptTests.Fixture f, IManagerAccessService access,
        IOrderActivityLogService logs, IOrderManagerActionService actions)
        => new(new ManagerRepository(NullLogger<ManagerRepository>.Instance,
                new Configuration(new ConfigurationBuilder().Build()), f.Database.Factory, Mock.Of<IStoreService>()),
            access, Mock.Of<INodeService>(), logs, actions, null!, NullLogger<EkomManagerController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
}
