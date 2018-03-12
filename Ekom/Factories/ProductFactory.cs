using Ekom.Interfaces;
using Ekom.Models;
using Examine;
using Umbraco.Core.Models;

namespace Ekom.Factories
{
    class ProductFactory : IPerStoreFactory<IProduct>
    {
        public IProduct Create(SearchResult item, IStore store)
        {
            return new Product(item, store);
        }

        public IProduct Create(IContent item, IStore store)
        {
            return new Product(item, store);
        }
    }
}
