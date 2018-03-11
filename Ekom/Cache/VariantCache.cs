using Ekom.Interfaces;
using Ekom.Models.Abstractions;
using Ekom.Services;

namespace Ekom.Cache
{
    class VariantCache : PerStoreCache<IVariant>
    {
        public override string NodeAlias { get; } = "ekmProductVariant";

        public VariantCache(
            ILogFactory logFac,
            Configuration config,
            ExamineManagerBase examineManager,
            IBaseCache<IStore> storeCache,
            IPerStoreFactory<IVariant> perStoreFactory
        ) : base(config, examineManager, storeCache, perStoreFactory)
        {
            _log = logFac.GetLogger(typeof(VariantCache));
        }
    }
}
