using Ekom.Cache;
using Xunit;

namespace Ekom.Tests.Tests;

public class PriceCacheTests
{
    [Fact]
    public void GetItemGeneration_InvokesAsyncHandler()
    {
        var itemKey = CreateItemKey();
        var expectedGeneration = Guid.NewGuid().ToString("N");

        ValueTask Handler(PriceCache.PriceGenerationEventArgs args, CancellationToken ct)
        {
            args.Generation = expectedGeneration;
            return ValueTask.CompletedTask;
        }

        PriceCache.OnGenerationCreatedAsync += Handler;

        try
        {
            var generation = PriceCache.GetItemGeneration(itemKey);

            Assert.Equal(expectedGeneration, generation);
        }
        finally
        {
            PriceCache.OnGenerationCreatedAsync -= Handler;
        }
    }

    [Fact]
    public void GetItemGeneration_InvokesAsyncHandlersInRegistrationOrder()
    {
        var itemKey = CreateItemKey();
        var invocations = new List<int>();

        ValueTask FirstHandler(PriceCache.PriceGenerationEventArgs args, CancellationToken ct)
        {
            invocations.Add(1);
            return ValueTask.CompletedTask;
        }

        ValueTask SecondHandler(PriceCache.PriceGenerationEventArgs args, CancellationToken ct)
        {
            invocations.Add(2);
            return ValueTask.CompletedTask;
        }

        PriceCache.OnGenerationCreatedAsync += FirstHandler;
        PriceCache.OnGenerationCreatedAsync += SecondHandler;

        try
        {
            PriceCache.GetItemGeneration(itemKey);

            Assert.Equal([1, 2], invocations);
        }
        finally
        {
            PriceCache.OnGenerationCreatedAsync -= SecondHandler;
            PriceCache.OnGenerationCreatedAsync -= FirstHandler;
        }
    }

    [Fact]
    public async Task GetItemGenerationAsync_InvokesAsyncHandler()
    {
        var itemKey = CreateItemKey();
        var expectedGeneration = Guid.NewGuid().ToString("N");

        ValueTask Handler(PriceCache.PriceGenerationEventArgs args, CancellationToken ct)
        {
            args.Generation = expectedGeneration;
            return ValueTask.CompletedTask;
        }

        PriceCache.OnGenerationCreatedAsync += Handler;

        try
        {
            var generation = await PriceCache.GetItemGenerationAsync(itemKey);

            Assert.Equal(expectedGeneration, generation);
        }
        finally
        {
            PriceCache.OnGenerationCreatedAsync -= Handler;
        }
    }

    [Fact]
    public async Task GetItemGenerationAsync_InvokesAsyncHandlersInRegistrationOrder()
    {
        var itemKey = CreateItemKey();
        var invocations = new List<int>();

        ValueTask FirstHandler(PriceCache.PriceGenerationEventArgs args, CancellationToken ct)
        {
            invocations.Add(1);
            return ValueTask.CompletedTask;
        }

        ValueTask SecondHandler(PriceCache.PriceGenerationEventArgs args, CancellationToken ct)
        {
            invocations.Add(2);
            return ValueTask.CompletedTask;
        }

        PriceCache.OnGenerationCreatedAsync += FirstHandler;
        PriceCache.OnGenerationCreatedAsync += SecondHandler;

        try
        {
            await PriceCache.GetItemGenerationAsync(itemKey);

            Assert.Equal([1, 2], invocations);
        }
        finally
        {
            PriceCache.OnGenerationCreatedAsync -= SecondHandler;
            PriceCache.OnGenerationCreatedAsync -= FirstHandler;
        }
    }

    [Fact]
    public async Task GetItemGenerationAsync_ThrowsWhenCancelled()
    {
        var itemKey = CreateItemKey();
        var invoked = false;

        ValueTask Handler(PriceCache.PriceGenerationEventArgs args, CancellationToken ct)
        {
            invoked = true;
            return ValueTask.CompletedTask;
        }

        PriceCache.OnGenerationCreatedAsync += Handler;

        try
        {
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            await Assert.ThrowsAsync<OperationCanceledException>(
                async () => await PriceCache.GetItemGenerationAsync(itemKey, cts.Token));

            Assert.False(invoked);
        }
        finally
        {
            PriceCache.OnGenerationCreatedAsync -= Handler;
        }
    }

    [Fact]
    public void BeginBulkInvalidation_PreservesItemInvalidationEvents()
    {
        var itemKey = CreateItemKey();
        var invalidatedGenerations = new List<string>();

        void Handler(object? sender, PriceCache.PriceGenerationEventArgs args)
        {
            if (args.ItemKey == itemKey)
            {
                invalidatedGenerations.Add(args.Generation);
            }
        }

        PriceCache.OnGenerationInvalidated += Handler;
        try
        {
            using (PriceCache.BeginBulkInvalidation())
            {
                PriceCache.InvalidateItem(itemKey);
                PriceCache.InvalidateItem(itemKey);
            }

            Assert.Equal(2, invalidatedGenerations.Count);
            Assert.NotEqual(invalidatedGenerations[0], invalidatedGenerations[1]);
        }
        finally
        {
            PriceCache.OnGenerationInvalidated -= Handler;
        }
    }

    private static string CreateItemKey() => $"test-{Guid.NewGuid():N}";
}
