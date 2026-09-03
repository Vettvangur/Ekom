using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Services;
using Microsoft.Extensions.DependencyInjection;


namespace Ekom.API;

/// <summary>
/// The Ekom API, get current or all stores.
/// </summary>
public class Store
{
    /// <summary>
    /// Store Instance
    /// </summary>
    public static Store Instance => Configuration.Resolver.GetService<Store>();

    readonly IStoreService _storeSvc;
    readonly ICacheRefreshService _cacheRefreshService;
    /// <summary>
    /// ctor
    /// </summary>
    internal Store(
        IStoreService storeService,
        ICacheRefreshService cacheRefreshService)
    {
        _storeSvc = storeService;
        _cacheRefreshService = cacheRefreshService;
    }

    /// <summary>
    /// Get store from <see cref="Ekom.Models.ContentRequest"/> or first store available
    /// </summary>
    /// <returns></returns>
    public IStore? GetStore()
    {
        return _storeSvc.GetStoreFromCache();
    }

    /// <summary>
    /// Get store by alias
    /// </summary>
    /// <param name="storeAlias"></param>
    /// <returns></returns>
    public IStore? GetStore(string? storeAlias)
    {
        return _storeSvc.GetStoreByAlias(storeAlias);
    }

    /// <summary>
    /// Get store by domain
    /// </summary>
    /// <returns></returns>
    public IStore? GetStoreByDomain(string domain, string culture)
    {
        return _storeSvc.GetStoreByDomain(domain, culture);
    }

    /// <summary>
    /// Get all stores
    /// </summary>
    /// <returns></returns>
    public IEnumerable<IStore> GetAllStores()
    {
        return _storeSvc.GetAllStores();
    }


    /// <summary>
    /// Get domains
    /// </summary>
    /// <returns></returns>
    public IEnumerable<UmbracoDomain> GetDomains()
    {
        return _storeSvc.GetDomains();
    }

    /// <summary>
    /// Set store from <see cref="Ekom.Models.ContentRequest"/>
    /// </summary>
    /// <returns></returns>
    public IStore? SetStore(string storeAlias)
    {
        return _storeSvc.SetStore(storeAlias);
    }

    public void RefreshCache()
    {
        _cacheRefreshService.RefreshCache();
    }

}
