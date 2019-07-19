using Ekom.Interfaces;
using Ekom.Services;
using Umbraco.Core.Composing;
using Umbraco.Core.Logging;

namespace Ekom.Cache
{
    class ShippingProviderCache : PerStoreCache<IShippingProvider>
    {
        public override string NodeAlias { get; } = "ekmShippingProvider";

        public ShippingProviderCache(
            Configuration config,
            ILogger logger,
            IFactory factory,
            IBaseCache<IStore> storeCache,
            IPerStoreFactory<IShippingProvider> perStoreFactory
        ) : base(config, logger, factory, storeCache, perStoreFactory)
        {
        }
    }
}
