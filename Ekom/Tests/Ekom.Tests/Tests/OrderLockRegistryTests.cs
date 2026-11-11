using Ekom.Services;
using Xunit;

namespace Ekom.Tests.Tests;

public class OrderLockRegistryTests
{
    [Fact]
    public void Get_ReturnsOneLockPerOrder()
    {
        var registry = new OrderLockRegistry();
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();

        Assert.Same(registry.Get(first), registry.Get(first));
        Assert.NotSame(registry.Get(first), registry.Get(second));
    }

    [Fact]
    public void Get_DropsOnlyIdleLocksThatNobodyHolds()
    {
        long now = 0;
        var registry = new OrderLockRegistry(TimeSpan.FromMinutes(1), () => now, pruneAt: 3);
        Guid idle = Guid.NewGuid();
        Guid held = Guid.NewGuid();
        Guid recent = Guid.NewGuid();
        SemaphoreSlim idleLock = registry.Get(idle);
        SemaphoreSlim heldLock = registry.Get(held);
        heldLock.Wait();

        now = 120_000;
        SemaphoreSlim recentLock = registry.Get(recent);
        registry.Get(Guid.NewGuid());

        Assert.Equal(3, registry.Count);
        Assert.Same(heldLock, registry.Get(held));
        Assert.Same(recentLock, registry.Get(recent));
        Assert.NotSame(idleLock, registry.Get(idle));
    }

    [Fact]
    public void Get_DoesNotGrowWithoutBoundWhenOrdersComeAndGo()
    {
        long now = 0;
        var registry = new OrderLockRegistry(TimeSpan.FromMinutes(1), () => now);

        for (int i = 0; i < 20_000; i++)
        {
            now += 1_000;
            registry.Get(Guid.NewGuid());
        }

        Assert.InRange(registry.Count, 1, 1_024);
    }

    [Fact]
    public async Task Get_KeepsEditsOfOneOrderApartWhileOtherOrdersChurn()
    {
        var registry = new OrderLockRegistry();
        Guid order = Guid.NewGuid();
        int inside = 0;
        int overlaps = 0;
        int total = 0;

        async Task EditAsync()
        {
            for (int i = 0; i < 500; i++)
            {
                registry.Get(Guid.NewGuid());
                SemaphoreSlim semaphore = registry.Get(order);
                await semaphore.WaitAsync();
                try
                {
                    if (Interlocked.Increment(ref inside) != 1) Interlocked.Increment(ref overlaps);
                    total++;
                    await Task.Yield();
                    Interlocked.Decrement(ref inside);
                }
                finally
                {
                    semaphore.Release();
                }
            }
        }

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(EditAsync)));

        Assert.Equal(0, overlaps);
        Assert.Equal(4_000, total);
    }
}
