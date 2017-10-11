using Examine;
using uWebshop.Models;
using uWebshop.Services;

namespace uWebshop.Cache
{
    class VariantCache : PerStoreCache<Variant>
    {
        public override string NodeAlias { get; } = "uwbsProductVariant";

        protected override Variant New(SearchResult r, Store store)
        {
            return new Variant(r, store);
        }

        public VariantCache(
            ILogFactory logFac,
            Configuration config,
            ExamineManager examineManager,
            IBaseCache<Store> storeCache
        ) : base(config, examineManager, storeCache)
        {
            _log = logFac.GetLogger(typeof(VariantCache));
        }
    }
}
