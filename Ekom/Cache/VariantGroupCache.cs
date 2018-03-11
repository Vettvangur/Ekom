using Ekom.Interfaces;
using Ekom.Models.Abstractions;
using Ekom.Services;

namespace Ekom.Cache
{
    class VariantGroupCache : PerStoreCache<IVariantGroup>
    {
        public override string NodeAlias { get; } = "ekmProductVariantGroup";

        public VariantGroupCache(
            ILogFactory logFac,
            Configuration config,
            ExamineManagerBase examineManager,
            IBaseCache<IStore> storeCache,
            IPerStoreFactory<IVariantGroup> perStoreCache
        ) : base(config, examineManager, storeCache, perStoreCache)
        {
            _log = logFac.GetLogger<VariantGroupCache>();
        }
    }
}
