using Examine;
using Ekom.Models;
using Ekom.Services;
using Ekom.Models.Abstractions;
using Ekom.Interfaces;
using Umbraco.Core.Models;

namespace Ekom.Cache
{
    class ShippingProviderCache : PerStoreCache<IShippingProvider>
    {
        public override string NodeAlias { get; } = "ekmShippingProvider";

        protected override IShippingProvider New(SearchResult r, Store store)
        {
            return new ShippingProvider(r, store);
        }
        protected override IShippingProvider New(IContent content, Store store)
        {
            return new ShippingProvider(content, store);
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
