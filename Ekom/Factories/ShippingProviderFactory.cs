using Ekom.Interfaces;
using Ekom.Models;
using Examine;
using Umbraco.Core.Models;

namespace Ekom.Factories
{
    class ShippingProviderFactory : IPerStoreFactory<IShippingProvider>
    {
        public IShippingProvider Create(SearchResult item, IStore store)
        {
            return new ShippingProvider(item, store);
        }

        public IShippingProvider Create(IContent item, IStore store)
        {
            return new ShippingProvider(item, store);
        }
    }
}
