using Ekom.Interfaces;
using Ekom.Models.Abstractions;
using Ekom.Services;

namespace Ekom.Cache
{
    class VariantGroupCache : PerStoreCache<IVariantGroup>
    {
        public override string NodeAlias { get; } = "ekmProductVariantGroup";

        public VariantGroupCache(
            ILogger logger,
            Configuration config,
            IBaseCache<IStore> storeCache,
            IPerStoreFactory<IVariantGroup> perStoreCache
        ) : base(config, storeCache, perStoreCache)
        {
            _logger = logger;
        }
    }
}
