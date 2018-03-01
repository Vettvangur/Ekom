using Examine;
using Ekom.Models;
using Ekom.Services;
using Ekom.Models.Abstractions;
using Ekom.Interfaces;
using Umbraco.Core.Models;

namespace Ekom.Cache
{
    class VariantCache : PerStoreCache<IVariant>
    {
        public override string NodeAlias { get; } = "ekmProductVariant";

        protected override IVariant New(SearchResult r, Store store)
        {
            return new Variant(r, store);
        }
        protected override IVariant New(IContent content, Store store)
        {
            return new Variant(content, store);
        }

        public VariantCache(
            ILogFactory logFac,
            Configuration config,
            ExamineManagerBase examineManager,
            IBaseCache<Store> storeCache
        ) : base(config, examineManager, storeCache)
        {
            _log = logFac.GetLogger(typeof(VariantCache));
        }
    }
}
