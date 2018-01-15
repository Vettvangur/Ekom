using Ekom.Models;
using System.Collections.Generic;

namespace Ekom.Interfaces
{
    interface IStoreService
    {
        IEnumerable<Store> GetAllStores();
        Store GetStoreByAlias(string alias);
        Store GetStoreByDomain(string domain = "");
        Store GetStoreFromCache();
    }
}
