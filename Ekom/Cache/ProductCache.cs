using Examine;
using uWebshop.Models;
using uWebshop.Services;

namespace uWebshop.Cache
{
    class ProductCache : PerStoreCache<Product>
    {
        public override string NodeAlias { get; } = "uwbsProduct";

        protected override Product New(SearchResult r, Store store)
        {
            return new Product(r, store);
        }

        public ProductCache(
            ILogFactory logFac,
            Configuration config,
            ExamineManager examineManager,
            IBaseCache<Store> storeCache
        ) : base(config, examineManager, storeCache)
        {
            _log = logFac.GetLogger(typeof(ProductCache));
        }
    }
}
