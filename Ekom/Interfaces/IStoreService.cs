using System.Collections.Generic;

namespace Ekom.Interfaces
{
    interface IStoreService
    {
        IEnumerable<IStore> GetAllStores();
        IStore GetStoreByAlias(string alias);
        IStore GetStoreByDomain(string domain = "");
        IStore GetStoreFromCache();
    }
}
