using Examine;
using Ekom.Models;
using Ekom.Services;

namespace Ekom.Cache
{
    class VariantCache : PerStoreCache<Variant>
    {
        public override string NodeAlias { get; } = "ekmProductVariant";

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
