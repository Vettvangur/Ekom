using Ekom.Models;
using Ekom.Models.Abstractions;
using Ekom.Services;
using Examine;

namespace Ekom.Cache
{
    class VariantGroupCache : PerStoreCache<VariantGroup>
    {
        public override string NodeAlias { get; } = "ekmProductVariantGroup";

        protected override VariantGroup New(SearchResult r, Store store)
        {
            return new VariantGroup(r, store);
            //throw new NotImplementedException("Should use the VariantGroup Factory");
        }

        public VariantGroupCache(
            ILogFactory logFac,
            Configuration config,
            ExamineManagerBase examineManager,
            IBaseCache<Store> storeCache
        //IObjectFactory<VariantGroup> objFac
        ) : base(config, examineManager, storeCache)
        {
            //_objFac = objFac;

            _log = logFac.GetLogger(typeof(VariantGroupCache));
        }
    }
}
