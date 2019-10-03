using Ekom.Interfaces;
using Umbraco.Core.Composing;
using Umbraco.Core.Logging;

namespace Ekom.Cache
{
    class ProductCache : PerStoreCache<IProduct>
    {
        public override string NodeAlias { get; } = "ekmProduct";

        public ProductCache(
            Configuration config,
            ILogger logger,
            IFactory factory,
            IBaseCache<IStore> storeCache,
            IPerStoreFactory<IProduct> perStoreFactory
        ) : base(config, logger, factory, storeCache, perStoreFactory)
        {
        }
    }
}
