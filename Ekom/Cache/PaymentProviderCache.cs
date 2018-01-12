using Examine;
using Ekom.Models;
using Ekom.Services;
using Ekom.Models.Abstractions;

namespace Ekom.Cache
{
    class PaymentProviderCache : PerStoreCache<PaymentProvider>
    {
        public override string NodeAlias { get; } = "ekmPaymentProvider";

        protected override PaymentProvider New(SearchResult r, Store store)
        {
            return new PaymentProvider(r, store);
        }

        public PaymentProviderCache(
            ILogFactory logFac,
            Configuration config,
            ExamineManagerBase examineManager,
            IBaseCache<Store> storeCache
        ) : base(config, examineManager, storeCache)
        {
            _log = logFac.GetLogger(typeof(PaymentProviderCache));
        }
    }
}
