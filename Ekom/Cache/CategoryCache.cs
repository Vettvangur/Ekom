using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Models.Abstractions;
using Ekom.Services;
using Examine;
using Umbraco.Core.Models;

namespace Ekom.Cache
{
    class CategoryCache : PerStoreCache<ICategory>
    {
        public override string NodeAlias { get; } = "ekmCategory";

        protected override ICategory New(SearchResult r, Store store)
        {
            return new Category(r, store);
        }
        protected override ICategory New(IContent content, Store store)
        {
            return new Category(content, store);
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
