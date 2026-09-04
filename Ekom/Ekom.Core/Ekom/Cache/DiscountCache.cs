using Ekom.Interfaces;
using Ekom.Models;
using Microsoft.Extensions.Logging;

namespace Ekom.Cache;

class DiscountCache : PerStoreCache<IDiscount>
{
    public override string NodeAlias { get; } = "ekmOrderDiscount";
    protected override string? StoreDisableFolderAlias => "ekmOrderDiscountsFolder";

    /// <summary>
    /// ctor
    /// </summary>
    public DiscountCache(
        Configuration config,
        ILogger<IPerStoreCache<IDiscount>> logger,
        IBaseCache<IStore> storeCache,
        IPerStoreFactory<IDiscount> perStoreFactory,
        IServiceProvider serviceProvider)
        : base(config, logger, storeCache, perStoreFactory, serviceProvider)
    {
    }

}
