using System;
using Examine;
using uWebshop.Models;
using uWebshop.Services;
using uWebshop.Models.Data;

namespace uWebshop.Cache
{
    public class StockCache : PerStoreCache<StockData>
    {
        public override string nodeAlias { get; } = "";

        protected override StockData New(SearchResult r, Store store)
        {
            return null;
        }

        public override void FillCache()
        {
            
        }

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="logFac"></param>
        /// <param name="config"></param>
        /// <param name="examineManager"></param>
        /// <param name="storeCache"></param>
        public StockCache(
            ILogFactory logFac,
            Configuration config,
            ExamineManager examineManager,
            IBaseCache<Store> storeCache
        )
        {
            _config = config;
            _examineManager = examineManager;
            _storeCache = storeCache;

            _log = logFac.GetLogger(typeof(StockCache));
        }
    }
}
