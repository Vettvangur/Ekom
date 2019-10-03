using Ekom.Interfaces;
using Umbraco.Core.Composing;
using Umbraco.Core.Logging;

namespace Ekom.Cache
{
    class VariantGroupCache : PerStoreCache<IVariantGroup>
    {
        public override string NodeAlias { get; } = "ekmProductVariantGroup";

        public VariantGroupCache(
            Configuration config,
            ILogger logger,
            IFactory factory,
            IBaseCache<IStore> storeCache,
            IPerStoreFactory<IVariantGroup> perStoreCache
        ) : base(config, logger, factory, storeCache, perStoreCache)
        {
        }
    }
}
