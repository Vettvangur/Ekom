using Ekom.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Ekom.Umb;

internal sealed class EkomCacheRefreshService : ICacheRefreshService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public EkomCacheRefreshService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public void RefreshCache()
    {
        using var scope = _scopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<EkomCacheInitializer>().Initialize(true);
    }
}
