using Examine;
using System;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Services;

namespace Ekom.Cache
{
    class VariantGroupCache : PerStoreCache<VariantGroup>
    {
        public override string NodeAlias { get; } = "ekmProductVariantGroup";

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
