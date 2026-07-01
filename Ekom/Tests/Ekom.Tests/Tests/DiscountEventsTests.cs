using Ekom.Events;
using Xunit;

namespace Ekom.Tests.Tests;

public class DiscountEventsTests
{
    [Fact]
    public async Task BeforeEvaluateDiscountsAsync_InvokesHandlersInRegistrationOrder()
    {
        var discountEvents = new DiscountEvents();
        var invocations = new List<int>();

        Task FirstHandler(object? sender, DiscountEvents.ProductDiscountEvaluationEventArgs args)
        {
            invocations.Add(1);
            return Task.CompletedTask;
        }

        Task SecondHandler(object? sender, DiscountEvents.ProductDiscountEvaluationEventArgs args)
        {
            invocations.Add(2);
            return Task.CompletedTask;
        }

        discountEvents.BeforeEvaluateDiscountsAsync += FirstHandler;
        discountEvents.BeforeEvaluateDiscountsAsync += SecondHandler;

        await discountEvents.RaiseBeforeEvaluateDiscountsAsync(
            this,
            new DiscountEvents.ProductDiscountEvaluationEventArgs(),
            CancellationToken.None);

        Assert.Equal([1, 2], invocations);
    }

    [Fact]
    public async Task BeforeEvaluateDiscountsAsync_AllowsHandlerToUpdateArgs()
    {
        var discountEvents = new DiscountEvents();

        Task Handler(object? sender, DiscountEvents.ProductDiscountEvaluationEventArgs args)
        {
            args.StoreAlias = "updated";
            return Task.CompletedTask;
        }

        var eventArgs = new DiscountEvents.ProductDiscountEvaluationEventArgs
        {
            StoreAlias = "original"
        };

        discountEvents.BeforeEvaluateDiscountsAsync += Handler;

        await discountEvents.RaiseBeforeEvaluateDiscountsAsync(this, eventArgs, CancellationToken.None);

        Assert.Equal("updated", eventArgs.StoreAlias);
    }
}
