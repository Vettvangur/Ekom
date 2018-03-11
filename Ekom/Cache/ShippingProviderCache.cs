using Ekom.Interfaces;
using Ekom.Models.Abstractions;
using Ekom.Services;

namespace Ekom.Cache
{
    class ShippingProviderCache : PerStoreCache<IShippingProvider>
    {
        public override string NodeAlias { get; } = "ekmShippingProvider";

        public ShippingProviderCache(
            Configuration config,
            ILogFactory logFac,
            ExamineManagerBase examineManager,
            IBaseCache<IStore> storeCache,
            IPerStoreFactory<IShippingProvider> perStoreFactory
        ) : base(config, examineManager, storeCache, perStoreFactory)
        {
            _log = logFac.GetLogger(typeof(ShippingProviderCache));
        }
    }
}
