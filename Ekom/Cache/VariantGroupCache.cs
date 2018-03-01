using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Models.Abstractions;
using Ekom.Services;
using Examine;
using Umbraco.Core.Models;

namespace Ekom.Cache
{
    class VariantGroupCache : PerStoreCache<IVariantGroup>
    {
        public override string NodeAlias { get; } = "ekmProductVariantGroup";

        protected override IVariantGroup New(SearchResult r, Store store)
        {
            return new VariantGroup(r, store);
        }
        protected override IVariantGroup New(IContent r, Store store)
        {
            return new VariantGroup(r, store);
        }

        public VariantGroupCache(
            ILogFactory logFac,
            Configuration config,
            ExamineManagerBase examineManager,
            IBaseCache<Store> storeCache
        ) : base(config, examineManager, storeCache)
        {
            _log = logFac.GetLogger<VariantGroupCache>();
        }
    }
}
