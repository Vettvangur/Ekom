using Ekom.Interfaces;
using Ekom.Models;

namespace Ekom.Factories
{
    class VariantGroupFactory : IPerStoreFactory<IVariantGroup>
    {
        public IVariantGroup Create(UmbracoContent item, IStore store)
        {
            return new VariantGroup(item, store);
        }
    }
}
