using Ekom.Events;
using Ekom.Models;
using Ekom.Models.Manager;
using Ekom.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Ekom.Tests.Tests;

public sealed class OrderManagerActionEventsTests
{
    [Fact]
    public async Task ExecuteAsync_Raises_Event_For_Success_Result()
    {
        var orderInfo = new Mock<IOrderInfo>().Object;
        var result = new OrderManagerActionSuccessResult();
        var sut = CreateService(result);
        OrderManagerActionExecutedEventArgs? receivedArgs = null;

        Task Handler(object sender, OrderManagerActionExecutedEventArgs args, CancellationToken ct)
        {
            receivedArgs = args;
            return Task.CompletedTask;
        }

        OrderManagerEvents.ActionExecutedAsync += Handler;
        try
        {
            await sut.ExecuteAsync(orderInfo, "dropp-print-label", "manager@example.com");
        }
        finally
        {
            OrderManagerEvents.ActionExecutedAsync -= Handler;
        }

        Assert.NotNull(receivedArgs);
        Assert.Same(orderInfo, receivedArgs.OrderInfo);
        Assert.Equal("dropp-print-label", receivedArgs.ActionKey);
        Assert.Equal("manager@example.com", receivedArgs.UserName);
        Assert.Same(result, receivedArgs.Result);
    }

    [Fact]
    public async Task ExecuteAsync_Raises_Event_For_File_Result()
    {
        var result = new OrderManagerActionFileResult
        {
            Content = [1, 2, 3],
            ContentType = "application/pdf"
        };
        var sut = CreateService(result);
        var raised = false;

        Task Handler(object sender, OrderManagerActionExecutedEventArgs args, CancellationToken ct)
        {
            raised = true;
            return Task.CompletedTask;
        }

        OrderManagerEvents.ActionExecutedAsync += Handler;
        try
        {
            await sut.ExecuteAsync(new Mock<IOrderInfo>().Object, "posturinn-print-label");
        }
        finally
        {
            OrderManagerEvents.ActionExecutedAsync -= Handler;
        }

        Assert.True(raised);
    }

    [Fact]
    public async Task ExecuteAsync_Does_Not_Raise_Event_For_Bad_Request_Result()
    {
        var sut = CreateService(new OrderManagerActionBadRequestResult());
        var raised = false;

        Task Handler(object sender, OrderManagerActionExecutedEventArgs args, CancellationToken ct)
        {
            raised = true;
            return Task.CompletedTask;
        }

        OrderManagerEvents.ActionExecutedAsync += Handler;
        try
        {
            await sut.ExecuteAsync(new Mock<IOrderInfo>().Object, "dropp-print-label");
        }
        finally
        {
            OrderManagerEvents.ActionExecutedAsync -= Handler;
        }

        Assert.False(raised);
    }

    [Fact]
    public async Task ExecuteAsync_Does_Not_Raise_Event_For_Unknown_Action()
    {
        var sut = CreateService(null);
        var raised = false;

        Task Handler(object sender, OrderManagerActionExecutedEventArgs args, CancellationToken ct)
        {
            raised = true;
            return Task.CompletedTask;
        }

        OrderManagerEvents.ActionExecutedAsync += Handler;
        try
        {
            await sut.ExecuteAsync(new Mock<IOrderInfo>().Object, "unknown-action");
        }
        finally
        {
            OrderManagerEvents.ActionExecutedAsync -= Handler;
        }

        Assert.False(raised);
    }

    [Fact]
    public async Task ExecuteAsync_Propagates_Event_Handler_Exception()
    {
        var sut = CreateService(new OrderManagerActionSuccessResult());

        Task Handler(object sender, OrderManagerActionExecutedEventArgs args, CancellationToken ct)
            => throw new InvalidOperationException("Failed to process label");

        OrderManagerEvents.ActionExecutedAsync += Handler;
        try
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.ExecuteAsync(new Mock<IOrderInfo>().Object, "dropp-print-label"));

            Assert.Equal("Failed to process label", exception.Message);
        }
        finally
        {
            OrderManagerEvents.ActionExecutedAsync -= Handler;
        }
    }

    private static OrderManagerActionService CreateService(OrderManagerActionExecutionResult? result)
    {
        var provider = new TestOrderManagerActionProvider(result);
        return new OrderManagerActionService(
            [provider],
            new Mock<ILogger<OrderManagerActionService>>().Object);
    }

    private sealed class TestOrderManagerActionProvider : IOrderManagerActionProvider
    {
        private readonly OrderManagerActionExecutionResult? _result;

        public TestOrderManagerActionProvider(OrderManagerActionExecutionResult? result)
        {
            _result = result;
        }

        public Task<IReadOnlyCollection<OrderManagerAction>> GetActionsAsync(IOrderInfo orderInfo, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyCollection<OrderManagerAction>>(Array.Empty<OrderManagerAction>());

        public Task<OrderManagerActionExecutionResult?> ExecuteAsync(IOrderInfo orderInfo, string actionKey, string? userName = null, CancellationToken ct = default)
            => Task.FromResult(_result);
    }
}
