using Ekom.Interfaces;
using Ekom.Models.Abstractions;
using Ekom.Services;

namespace Ekom.Cache
{
    class PaymentProviderCache : PerStoreCache<IPaymentProvider>
    {
        public override string NodeAlias { get; } = "ekmPaymentProvider";

        public PaymentProviderCache(
            ILogFactory logFac,
            Configuration config,
            IBaseCache<IStore> storeCache,
            IPerStoreFactory<IPaymentProvider> perStoreFactory
        ) : base(config, storeCache, perStoreFactory)
        {
            _log = logFac.GetLogger(typeof(PaymentProviderCache));
        }
    }
}
