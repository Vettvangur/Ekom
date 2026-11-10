using Ekom.Cache;
using System.Collections.Concurrent;
using Xunit;

namespace Ekom.Tests.Tests;

public class CacheRebuildRefreshTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Rebuild_SurvivesRefreshableUpdateAndAppliesItAfterPublishing(bool keepUpdateOpen)
    {
        var coordinator = new CacheRebuildCoordinator();
        var owner = new object();
        ConcurrentDictionary<int, decimal> Stock() => coordinator.Get(owner, "stock", static () => new ConcurrentDictionary<int, decimal>());
        Stock()[1] = 10;
        var committed = 10m;
        IDisposable? update = null;
        try
        {
            CacheRebuildCoordinator.Rebuild rebuild;
            using (rebuild = coordinator.Begin())
            {
                // The rebuild reads the row before the live update commits.
                Stock()[1] = committed;
                await OutsideRebuild(() =>
                {
                    committed = 9;
                    update = coordinator.UpdateRefreshable("stock-1", () =>
                    {
                        Stock()[1] = committed;
                        return Task.CompletedTask;
                    });
                    Stock()[1] = committed;
                    if (!keepUpdateOpen) update.Dispose();
                });

                rebuild.Commit();
            }

            await rebuild.Refreshed;
            Assert.Equal(9, Stock()[1]);
        }
        finally { update?.Dispose(); }
    }

    [Fact]
    public async Task Refreshes_WithTheSameKeyRunOnceAndOneFailureDoesNotStopOthers()
    {
        var coordinator = new CacheRebuildCoordinator();
        var calls = new ConcurrentQueue<string>();
        var rebuild = coordinator.Begin();
        await OutsideRebuild(() =>
        {
            coordinator.UpdateRefreshable("a", () => { calls.Enqueue("a-old"); return Task.CompletedTask; }).Dispose();
            coordinator.UpdateRefreshable("a", () => { calls.Enqueue("a"); return Task.CompletedTask; }).Dispose();
            coordinator.UpdateRefreshable("b", () => throw new InvalidOperationException("refresh failed")).Dispose();
            coordinator.UpdateRefreshable("c", () => { calls.Enqueue("c"); return Task.CompletedTask; }).Dispose();
        });

        rebuild.Commit();
        await rebuild.Refreshed;

        Assert.Equal(["a", "c"], calls.OrderBy(x => x));
    }

    [Fact]
    public async Task Refresh_IsNotRunWhenNoRebuildIsRunningOrTheRebuildFails()
    {
        var coordinator = new CacheRebuildCoordinator();
        var calls = 0;
        coordinator.UpdateRefreshable("idle", () => { calls++; return Task.CompletedTask; }).Dispose();

        var failed = coordinator.Begin();
        await OutsideRebuild(() =>
            coordinator.UpdateRefreshable("failed", () => { calls++; return Task.CompletedTask; }).Dispose());
        failed.Dispose();
        await failed.Refreshed;

        using var next = coordinator.Begin();
        next.Commit();
        await next.Refreshed;

        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task ContentUpdate_StillRejectsTheRebuildWithAConflict()
    {
        var coordinator = new CacheRebuildCoordinator();
        using var rebuild = coordinator.Begin();
        await OutsideRebuild(() => coordinator.Update().Dispose());

        Assert.Throws<CacheRebuildConflictException>(() => rebuild.Commit());
    }

    private static Task OutsideRebuild(Action action)
    {
        using (ExecutionContext.SuppressFlow()) return Task.Run(action);
    }
}
