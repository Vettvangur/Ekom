using Ekom.Interfaces;
using Ekom.Models.Abstractions;
using Ekom.Services;

namespace Ekom.Cache
{
    class ProductCache : PerStoreCache<IProduct>
    {
        public override string NodeAlias { get; } = "ekmProduct";

        public ProductCache(
            ILogFactory logFac,
            Configuration config,
            IBaseCache<IStore> storeCache,
            IPerStoreFactory<IProduct> perStoreFactory
        ) : base(config, storeCache, perStoreFactory)
        {
            _log = logFac.GetLogger<ProductCache>();
        }
    }
}
