using Ekom.Models;

namespace Ekom.Services;

public interface IStoreService
{
    IEnumerable<IStore> GetAllStores();
    IStore? GetStoreByAlias(string? alias);
    IStore? GetStoreByDomain(string domain = "", string? culture = null);
    IStore? GetStoreFromCache();
    IStore? SetStore(string storeAlias);
    IEnumerable<UmbracoDomain> GetDomains();
}
