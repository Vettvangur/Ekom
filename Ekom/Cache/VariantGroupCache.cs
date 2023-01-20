using Ekom.Interfaces;
using Ekom.Models;
using Microsoft.Extensions.Logging;
using System;

namespace Ekom.Cache
{
    class VariantGroupCache : PerStoreCache<IVariantGroup>
    {
        public override string NodeAlias { get; } = "ekmProductVariantGroup";

        public VariantGroupCache(
            Configuration config,
            ILogger<IPerStoreCache<IVariantGroup>> logger,
            IBaseCache<IStore> storeCache,
            IPerStoreFactory<IVariantGroup> perStoreFactory,
            IServiceProvider serviceProvider
        ) : base(config, logger, storeCache, perStoreFactory, serviceProvider)
        {
        }
    }
}
