using Ekom.Interfaces;
using Ekom.Models;
using Microsoft.Extensions.Logging;

namespace Ekom.Cache;

class PaymentProviderCache : PerStoreCache<IPaymentProvider>
{
    public override string NodeAlias { get; } = "ekmPaymentProvider";
    protected override string? StoreDisableFolderAlias => "ekmPaymentProvidersFolder";

    public PaymentProviderCache(
        Configuration config,
        ILogger<IPerStoreCache<IPaymentProvider>> logger,
        IBaseCache<IStore> storeCache,
        IPerStoreFactory<IPaymentProvider> perStoreFactory,
        IServiceProvider serviceProvider
    ) : base(config, logger, storeCache, perStoreFactory, serviceProvider)
    {
    }
}
