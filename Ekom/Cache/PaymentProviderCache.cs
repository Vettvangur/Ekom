using Examine;
using Ekom.Models;
using Ekom.Services;
using Ekom.Models.Abstractions;
using Ekom.Interfaces;
using Umbraco.Core.Models;

namespace Ekom.Cache
{
    class PaymentProviderCache : PerStoreCache<IPaymentProvider>
    {
        public override string NodeAlias { get; } = "ekmPaymentProvider";

        protected override IPaymentProvider New(SearchResult r, Store store)
        {
            return new PaymentProvider(r, store);
        }
        protected override IPaymentProvider New(IContent content, Store store)
        {
            return new PaymentProvider(content, store);
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
