using Examine;
using Ekom.Models;
using Ekom.Services;
using Ekom.Models.Discounts;
using Ekom.Models.Abstractions;

namespace Ekom.Cache
{
    class DiscountCache : PerStoreCache<Discount>
    {
        public override string NodeAlias { get; } = "ekmDiscount";

        /// <summary>
        /// ctor
        /// </summary>
        public DiscountCache(
            ILogFactory logFac,
            Configuration config,
            ExamineManagerBase examineManager,
            IBaseCache<Store> storeCache
        )
            : base(config, examineManager, storeCache)
        {
            _config = config;
            _examineManager = examineManager;
            _log = logFac.GetLogger<DiscountCache>();
        }

        protected override Discount New(SearchResult r, Store s)
        {
            return new Discount(r, s);
        }
    }
}
