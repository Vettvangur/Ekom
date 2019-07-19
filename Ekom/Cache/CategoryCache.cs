using Ekom.Interfaces;
using Ekom.Services;
using Umbraco.Core.Composing;
using Umbraco.Core.Logging;

namespace Ekom.Cache
{
    class CategoryCache : PerStoreCache<ICategory>
    {
        public override string NodeAlias { get; } = "ekmCategory";

        public CategoryCache(
            Configuration config,
            ILogger logger,
            IFactory factory,
            IBaseCache<IStore> storeCache,
            IPerStoreFactory<ICategory> perStoreFactory
        ) : base(config, logger, factory, storeCache, perStoreFactory)
        {
        }
    }
}
