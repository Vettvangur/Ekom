using System.Collections.Generic;
using System.Web;
using Umbraco.Core.Models;
using Ekom.Models;

namespace Ekom.Interfaces
{
    public interface IStoreService
    {
        IEnumerable<Store> GetAllStores();
        Store GetStore(IDomain umbracoDomain, HttpContextBase httpContext);
        Store GetStoreByAlias(string alias);
        Store GetStoreByDomain(string domain = "");
        Store GetStoreFromCache();
    }
}
