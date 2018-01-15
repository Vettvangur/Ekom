using Ekom.Models;
using Ekom.Models.Abstractions;
using Ekom.Services;
using Examine;

namespace Ekom.Cache
{
    class CategoryCache : PerStoreCache<Category>
    {
        public override string NodeAlias { get; } = "ekmCategory";

        protected override Category New(SearchResult r, Store store)
        {
            return new Category(r, store);
        }

        public CategoryCache(
            ILogFactory logFac,
            Configuration config,
            ExamineManagerBase examineManager,
            IBaseCache<Store> storeCache
        ) : base(config, examineManager, storeCache)
        {
            _log = logFac.GetLogger(typeof(CategoryCache));
        }
    }
}
