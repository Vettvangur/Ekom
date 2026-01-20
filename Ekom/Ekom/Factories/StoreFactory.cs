using Ekom.Interfaces;
using Ekom.Models;

namespace Ekom.Factories
{
    class StoreFactory : IObjectFactory<IStore>
    {
        public IStore Create(UmbracoContent item)
        {
            return new Store(item);
        }
    }
}
