using Ekom.Algolia.Indexing;
using Xunit;

namespace Ekom.Tests.Tests;

public class AlgoliaIndexingScopeTests
{
    [Fact]
    public void Suppress_SetsIsSuppressedUntilTheOutermostScopeIsDisposed()
    {
        Assert.False(AlgoliaIndexingScope.IsSuppressed);

        using (AlgoliaIndexingScope.Suppress())
        {
            Assert.True(AlgoliaIndexingScope.IsSuppressed);

            using (AlgoliaIndexingScope.Suppress())
            {
                Assert.True(AlgoliaIndexingScope.IsSuppressed);
            }

            Assert.True(AlgoliaIndexingScope.IsSuppressed);
        }

        Assert.False(AlgoliaIndexingScope.IsSuppressed);
    }

    [Fact]
    public void Suppress_DisposeCanBeCalledMultipleTimes()
    {
        var scope = AlgoliaIndexingScope.Suppress();

        scope.Dispose();
        scope.Dispose();

        Assert.False(AlgoliaIndexingScope.IsSuppressed);
    }

    [Fact]
    public async Task Suppress_FlowsToTasksStartedWithinTheScope()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Task task;
        using (AlgoliaIndexingScope.Suppress())
        {
            task = Task.Run(async () =>
            {
                started.SetResult();
                await release.Task;
                Assert.True(AlgoliaIndexingScope.IsSuppressed);
            });

            await started.Task;
        }

        Assert.False(AlgoliaIndexingScope.IsSuppressed);
        release.SetResult();
        await task;
    }
}
