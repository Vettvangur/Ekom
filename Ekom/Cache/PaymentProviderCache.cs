using Examine;
using uWebshop.Models;
using uWebshop.Services;

namespace uWebshop.Cache
{
    class PaymentProviderCache : PerStoreCache<PaymentProvider>
    {
        public override string NodeAlias { get; } = "uwbsPaymentProvider";

        protected override PaymentProvider New(SearchResult r, Store store)
        {
            return new PaymentProvider(r, store);
        }

        public PaymentProviderCache(
            ILogFactory logFac,
            Configuration config,
            ExamineManager examineManager,
            IBaseCache<Store> storeCache
        ) : base(config, examineManager, storeCache)
        {
            _log = logFac.GetLogger(typeof(PaymentProviderCache));
        }
    }
}
