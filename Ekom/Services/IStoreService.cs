using Ekom.Models;
using System.Collections.Generic;

namespace Ekom.Services
{
    interface IStoreService
    {
        IEnumerable<IStore> GetAllStores();
        IStore GetStoreByAlias(string alias);
        IStore GetStoreByDomain(string domain = "", string culture = "");
        IStore GetStoreFromCache();
        IEnumerable<UmbracoDomain> GetDomains();
    }
}
