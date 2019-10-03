using Ekom.Interfaces;
using Umbraco.Core.Composing;
using Umbraco.Core.Logging;
namespace Ekom.Cache
{
    class PaymentProviderCache : PerStoreCache<IPaymentProvider>
    {
        public override string NodeAlias { get; } = "netPaymentProvider";

        public PaymentProviderCache(
            Configuration config,
            ILogger logger,
            IFactory factory,
            IBaseCache<IStore> storeCache,
            IPerStoreFactory<IPaymentProvider> perStoreFactory
        ) : base(config, logger, factory, storeCache, perStoreFactory)
        {
        }
    }
}
