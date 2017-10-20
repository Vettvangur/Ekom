using Examine;
using System;
using uWebshop.Interfaces;
using uWebshop.Models;
using uWebshop.Services;

namespace uWebshop.Cache
{
    class VariantGroupCache : PerStoreCache<VariantGroup>
    {
        public override string NodeAlias { get; } = "uwbsProductVariantGroup";

        protected override VariantGroup New(SearchResult r, Store store)
        {
            throw new NotImplementedException("Should use the VariantGroup Factory");
        }

        public VariantGroupCache(
            ILogFactory logFac,
            Configuration config,
            ExamineManager examineManager,
            IBaseCache<Store> storeCache,
            IObjectFactory<VariantGroup> objFac
        ) : base(config, examineManager, storeCache)
        {
            _objFac = objFac;

            _log = logFac.GetLogger(typeof(VariantGroupCache));
        }
    }
}
