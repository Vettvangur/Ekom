using Ekom.Interfaces;
using Ekom.Models;

namespace Ekom.Factories
{
    class VariantFactory : IPerStoreFactory<IVariant>
    {
        public IVariant Create(UmbracoContent item, IStore store)
        {
            return new Variant(item, store);
        }
    }
}
