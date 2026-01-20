using Ekom.Interfaces;
using Ekom.Models;

namespace Ekom.Factories
{
    class CategoryFactory : IPerStoreFactory<ICategory>
    {
        public ICategory Create(UmbracoContent item, IStore store)
        {
            return new Category(item, store);
        }
    }
}
