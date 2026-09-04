using Ekom.Interfaces;
using Ekom.Models;
using Microsoft.Extensions.Logging;

namespace Ekom.Cache;

class ProductDiscountCache : PerStoreCache<IProductDiscount>
{
    public override string NodeAlias { get; } = "ekmProductDiscount";
    protected override string? StoreDisableFolderAlias => "ekmProductDiscountsFolder";

    public ProductDiscountCache(
        Configuration config,
        ILogger<IPerStoreCache<IProductDiscount>> logger,
        IBaseCache<IStore> storeCache,
        IPerStoreFactory<IProductDiscount> perStoreFactory,
        IServiceProvider serviceProvider
    ) : base(config, logger, storeCache, perStoreFactory, serviceProvider)
    {
    }

    public override void AddReplace(UmbracoContent node)
    {
        AddOrReplaceFromAllCaches(node);
    }

    public override void Remove(Guid key)
    {
        _logger.LogDebug("Attempting to remove product discount with key {Key}", key);

        RemoveItemFromAllCaches(key);
    }
}
