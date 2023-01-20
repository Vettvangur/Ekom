using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Utilities;
using Ekom.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;

namespace Ekom.Cache
{
    class ProductDiscountCache : PerStoreCache<IProductDiscount>
    {
        public override string NodeAlias { get; } = "ekmProductDiscount";

        readonly IPerStoreCache<IProduct> _perStoreProductCache;
        public ProductDiscountCache(
            Configuration config,
            ILogger<IPerStoreCache<IProductDiscount>> logger,
            IBaseCache<IStore> storeCache,
            IPerStoreFactory<IProductDiscount> perStoreFactory,
            IPerStoreCache<IProduct> perStoreProductCache,
            IServiceProvider serviceProvider
        ) : base(config, logger, storeCache, perStoreFactory, serviceProvider)
        {
            _perStoreProductCache = perStoreProductCache;
        }

        public override void AddReplace(UmbracoContent node)
        {
            // We use tempItem to only run the refresh on the products items once. and not for every store.
            IProductDiscount tempItem = null;

            foreach (var store in _storeCache.Cache)
            {
                try
                {
                    var ancestors = nodeService.NodeAncestors(node.Id.ToString());
                    if (!node.IsItemDisabled(store.Value, ancestors))
                    {
                        var item = _objFac?.Create(node, store.Value)
                            ?? (ProductDiscount)Activator.CreateInstance(typeof(ProductDiscount), node, store.Value);

                        if (item != null)
                        {
                            tempItem = item;

                            Cache[store.Value.Alias][node.Key] = item;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        $"Error on Add/Replacing item with id: {node.Id} in store: {store.Value.Alias}"
                    );
                }
            }

            //RefreshProductCache(tempItem);
        }


        /// <summary>
        /// <see cref="ICache"/> implementation,
        /// handles removal of nodes when umbraco events fire
        /// </summary>
        public override void Remove(Guid key)
        {
            _logger.LogDebug("Attempting to remove product discount with key {Key}", key);

            foreach (var store in _storeCache.Cache)
            {
                Cache[store.Value.Alias].TryRemove(key, out IProductDiscount i);
            }
        }

    }
}
