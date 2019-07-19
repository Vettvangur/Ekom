using Ekom.Interfaces;
using Ekom.Services;
using Umbraco.Core.Composing;
using Umbraco.Core.Logging;

namespace Ekom.Cache
{
    class VariantCache : PerStoreCache<IVariant>
    {
        public override string NodeAlias { get; } = "ekmProductVariant";

        public VariantCache(
            Configuration config,
            ILogger logger,
            IFactory factory,
            IBaseCache<IStore> storeCache,
            IPerStoreFactory<IVariant> perStoreFactory
        ) : base(config, logger, factory, storeCache, perStoreFactory)
        {
        }
    }
}
