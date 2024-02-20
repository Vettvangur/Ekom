using Ekom.Interfaces;
using Ekom.Models;
using Microsoft.Extensions.Logging;

namespace Ekom.Cache
{
    class CategoryCache : PerStoreCache<ICategory>
    {
        public override string NodeAlias { get; } = "ekmCategory";

        public CategoryCache(
            Configuration config,
            ILogger<IPerStoreCache<ICategory>> logger,
            IBaseCache<IStore> storeCache,
            IPerStoreFactory<ICategory> perStoreFactory,
            IServiceProvider serviceProvider
        ) : base(config, logger, storeCache, perStoreFactory, serviceProvider)
        {
        }
    }
}
