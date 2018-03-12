﻿using Ekom.Interfaces;
using Ekom.Models.Abstractions;
using Ekom.Services;

namespace Ekom.Cache
{
    class VariantCache : PerStoreCache<IVariant>
    {
        public override string NodeAlias { get; } = "ekmProductVariant";

        public VariantCache(
            ILogFactory logFac,
            Configuration config,
            IBaseCache<IStore> storeCache,
            IPerStoreFactory<IVariant> perStoreFactory
        ) : base(config, storeCache, perStoreFactory)
        {
            _log = logFac.GetLogger(typeof(VariantCache));
        }
    }
}
