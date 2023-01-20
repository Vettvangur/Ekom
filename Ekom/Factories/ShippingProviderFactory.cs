using Ekom.Interfaces;
using Ekom.Models;

namespace Ekom.Factories
{
    class ShippingProviderFactory : IPerStoreFactory<IShippingProvider>
    {
        public IShippingProvider Create(UmbracoContent item, IStore store)
        {
            return new ShippingProvider(item, store);
        }
    }
}
