using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Utilities;
using Microsoft.Extensions.Logging;

namespace Ekom.Cache
{
    class ProductDiscountCache : PerStoreCache<IProductDiscount>
    {
        public override string NodeAlias { get; } = "ekmProductDiscount";


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

            foreach (var store in _storeCache.Cache)
            {
                try
                {
                    var ancestors = nodeService.NodeAncestors(node.Id.ToString());
                    
                    if (node.IsItemDisabled(store.Value, ancestors)) continue;

                    var item = _objFac?.Create(node, store.Value)
                               ?? (ProductDiscount)Activator.CreateInstance(typeof(ProductDiscount), node, store.Value);

                    if (item == null) continue;

                    Cache[store.Value.Alias][node.Key] = item;
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
