using Ekom.Interfaces;
using Ekom.Models;
using Examine;
using Umbraco.Core.Models;

namespace Ekom.Factories
{
    class StoreFactory : IObjectFactory<IStore>
    {
        public IStore Create(ISearchResult item)
        {
            return new Store(item);
        }

        public IStore Create(IContent item)
        {
            return new Store(item);
        }
    }
}
