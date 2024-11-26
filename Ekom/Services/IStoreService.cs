using Ekom.Models;

namespace Ekom.Services;

interface IStoreService
{
    IEnumerable<IStore> GetAllStores();
    IStore? GetStoreByAlias(string alias);
    IStore? GetStoreByDomain(string domain = "", string culture = "");
    IStore? GetStoreFromCache();
    IStore? SetStore(string storeAlias);
    IEnumerable<UmbracoDomain> GetDomains();
}
