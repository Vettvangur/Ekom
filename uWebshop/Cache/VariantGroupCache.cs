using System;
using Examine;
using uWebshop.Models;
using uWebshop.Services;

namespace uWebshop.Cache
{
    public class VariantGroupCache : PerStoreCache<VariantGroup>
    {
        public override string nodeAlias { get; } = "uwbsProductVariantGroup";

        protected override VariantGroup New(SearchResult r, Store store)
        {
            return new VariantGroup(r, store);
        }

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="logFac"></param>
        /// <param name="config"></param>
        /// <param name="examineManager"></param>
        /// <param name="storeCache"></param>
        public VariantGroupCache(
            ILogFactory logFac,
            Configuration config,
            ExamineManager examineManager,
            IBaseCache<Store> storeCache
        )
        {
            _config = config;
            _examineManager = examineManager;
            _storeCache = storeCache;

            _log = logFac.GetLogger(typeof(VariantGroupCache));
        }
    }
}
