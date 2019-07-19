using Ekom.Interfaces;
using Ekom.Models;
using Examine;
using Umbraco.Core.Models;

namespace Ekom.Factories
{
    class VariantGroupFactory : IPerStoreFactory<IVariantGroup>
    {
        public IVariantGroup Create(ISearchResult item, IStore store)
        {
            return new VariantGroup(item, store);
        }

        public IVariantGroup Create(IContent item, IStore store)
        {
            return new VariantGroup(item, store);
        }
    }
}
