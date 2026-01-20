using Ekom.Interfaces;
using Ekom.Models;

namespace Ekom.Factories
{
    class ProductFactory : IPerStoreFactory<IProduct>
    {
        public IProduct Create(UmbracoContent item, IStore store)
        {
            return new Product(item, store);
        }
    }
}
