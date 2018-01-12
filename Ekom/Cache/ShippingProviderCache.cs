﻿using Examine;
using Ekom.Models;
using Ekom.Services;
using Ekom.Models.Abstractions;

namespace Ekom.Cache
{
    class ShippingProviderCache : PerStoreCache<ShippingProvider>
    {
        public override string NodeAlias { get; } = "ekmShippingProvider";

        protected override ShippingProvider New(SearchResult r, Store store)
        {
            return new ShippingProvider(r, store);
        }

        public ShippingProviderCache(
            Configuration config,
            ILogFactory logFac,
            ExamineManagerBase examineManager,
            IBaseCache<Store> storeCache
        ) : base(config, examineManager, storeCache)
        {
            _log = logFac.GetLogger(typeof(ShippingProviderCache));
        }
    }
}
