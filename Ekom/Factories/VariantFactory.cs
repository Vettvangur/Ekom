using Ekom.Interfaces;
using Ekom.Models;
using Examine;
using Umbraco.Core.Models;

namespace Ekom.Factories
{
    class VariantFactory : IPerStoreFactory<IVariant>
    {
        public IVariant Create(ISearchResult item, IStore store)
        {
            return new Variant(item, store);
        }

        public IVariant Create(IContent item, IStore store)
        {
            return new Variant(item, store);
        }
    }
}
