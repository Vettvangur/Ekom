using Ekom;
using Ekom.Cache;
using Ekom.Interfaces;
using Ekom.Models;
using Microsoft.Extensions.Logging;
using System;

namespace Ekom.Cache
{
    class ProductCache : PerStoreCache<IProduct>
    {
        public override string NodeAlias { get; } = "ekmProduct";

        public ProductCache(
            Configuration config,
            ILogger<IPerStoreCache<IProduct>> logger,
            IBaseCache<IStore> storeCache,
            IPerStoreFactory<IProduct> perStoreFactory,
            IServiceProvider serviceProvider
        ) : base(config, logger, storeCache, perStoreFactory, serviceProvider)
        {
        }
    }
}
