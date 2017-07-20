using System;
using Examine;
using uWebshop.Models;
using uWebshop.Services;

namespace uWebshop.Cache
{
    public class VariantCache : PerStoreCache<Variant>
    {
        public override string nodeAlias { get; } = "uwbsProductVariant";

        protected override Variant New(SearchResult r, Store store)
        {
            return new Variant(r, store);
        }

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="logFac"></param>
        /// <param name="config"></param>
        /// <param name="examineManager"></param>
        /// <param name="storeCache"></param>
        public VariantCache(
            ILogFactory logFac,
            Configuration config,
            ExamineManager examineManager,
            IBaseCache<Store> storeCache
        )
        {
            _config = config;
            _examineManager = examineManager;
            _storeCache = storeCache;

            _log = logFac.GetLogger(typeof(VariantCache));
        }
    }
}
