using Ekom.Cache;
using Xunit;

namespace Ekom.Tests.Tests;

public class CacheInitializationScopeTests
{
    [Fact]
    public void Begin_SetsIsActiveUntilTheOutermostScopeIsDisposed()
    {
        Assert.False(CacheInitializationScope.IsActive);

        using (CacheInitializationScope.Begin())
        {
            Assert.True(CacheInitializationScope.IsActive);

            using (CacheInitializationScope.Begin())
            {
                Assert.True(CacheInitializationScope.IsActive);
            }

            Assert.True(CacheInitializationScope.IsActive);
        }

        Assert.False(CacheInitializationScope.IsActive);
    }
}
