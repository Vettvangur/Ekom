using Examine;
using Ekom.Models;
using Ekom.Services;
using Ekom.Models.Abstractions;

namespace Ekom.Cache
{
    class ProductCache : PerStoreCache<Product>
    {
        public override string NodeAlias { get; } = "ekmProduct";

        protected override Product New(SearchResult r, Store store)
        {
            return new Product(r, store);
        }

        public ProductCache(
            ILogFactory logFac,
            Configuration config,
            ExamineManagerBase examineManager,
            IBaseCache<Store> storeCache
        ) : base(config, examineManager, storeCache)
        {
            _log = logFac.GetLogger(typeof(ProductCache));
        }
    }
}
