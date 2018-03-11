using Ekom.Interfaces;
using Ekom.Models.Abstractions;
using Ekom.Services;

namespace Ekom.Cache
{
    class CategoryCache : PerStoreCache<ICategory>
    {
        public override string NodeAlias { get; } = "ekmCategory";

        public CategoryCache(
            ILogFactory logFac,
            Configuration config,
            ExamineManagerBase examineManager,
            IBaseCache<IStore> storeCache,
            IPerStoreFactory<ICategory> perStoreFactory
        ) : base(config, examineManager, storeCache, perStoreFactory)
        {
            _log = logFac.GetLogger(typeof(CategoryCache));
        }
    }
}
