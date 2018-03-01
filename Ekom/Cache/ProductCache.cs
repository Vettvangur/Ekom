using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Models.Abstractions;
using Ekom.Services;
using Examine;
using Umbraco.Core.Models;

namespace Ekom.Cache
{
    class ProductCache : PerStoreCache<IProduct>
    {
        public override string NodeAlias { get; } = "ekmProduct";

        protected override IProduct New(SearchResult r, Store store)
        {
            return new Product(r, store);
        }
        protected override IProduct New(IContent ontent, Store store)
        {
            return new Product(ontent, store);
        }

        public ProductCache(
            ILogFactory logFac,
            Configuration config,
            ExamineManagerBase examineManager,
            IBaseCache<Store> storeCache
        ) : base(config, examineManager, storeCache)
        {
            _log = logFac.GetLogger<ProductCache>();
        }
    }
}
