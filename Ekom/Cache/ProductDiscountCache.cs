using Ekom.Exceptions;
using Ekom.Helpers;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Models.Discounts;
using Ekom.Services;
using Examine;
using Examine.Providers;
using Examine.SearchCriteria;
using System;
using System.Collections.Concurrent;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Models;


namespace Ekom.Cache
{
    class ProductDiscountCache : PerStoreCache<IProductDiscount>
    {
        public override string NodeAlias { get; } = "ekmProductDiscount";

        public ProductDiscountCache(
            ILogFactory logFac,
            Configuration config,
            IBaseCache<IStore> storeCache,
            IPerStoreFactory<IProductDiscount> perStoreFactory
        ) : base(config, storeCache, perStoreFactory)
        {
            _log = logFac.GetLogger(typeof(ProductDiscountCache));
        }

        public override void AddReplace(IContent node)
        {
            // We use tempItem to only run the refresh on the products items once. and not for every store.
            IProductDiscount tempItem = null;

            foreach (var store in _storeCache.Cache)
            {
                try
                {
   
                    if (!node.IsItemDisabled(store.Value))
                    {
                        var item = _objFac?.Create(node, store.Value)
                            ?? (ProductDiscount)Activator.CreateInstance(typeof(ProductDiscount), node, store.Value);

                        if (item != null) {

                            tempItem = item;

                            Cache[store.Value.Alias][node.Key] = item;
                        } 
                    }
                }
                catch (Exception ex)
                {
                    _log.Error("Error on Add/Replacing item with id: " + node.Id + " in store: " + store.Value.Alias, ex);
                }
            }

            RefreshProdutCache(tempItem);


        }


        /// <summary>
        /// <see cref="ICache"/> implementation,
        /// handles removal of nodes when umbraco events fire
        /// </summary>
        public override void Remove(Guid key)
        {
            _log.Debug($"Attempting to remove product discount with key {key}");
            IProductDiscount i = null;

            foreach (var store in _storeCache.Cache)
            {
                Cache[store.Value.Alias].TryRemove(key, out i);
            }

            RefreshProdutCache(i);
        }

        private void RefreshProdutCache(IProductDiscount discountItem)
        {
            if (discountItem != null)
            {
                // Refresh Product items cache
                var productCache = _config.CacheList.Value.FirstOrDefault(x => !string.IsNullOrEmpty(x.NodeAlias) && x.NodeAlias == "ekmProduct");
                var cs = ApplicationContext.Current.Services.ContentService;

                foreach (var productId in discountItem.DiscountItems)
                {
                    //TODO We need to use something else then the IContent here. This will be very slow with many products
                    var productNode = cs.GetById(productId);

                    if (productNode != null)
                    {
                        productCache.AddReplace(productNode);
                    }

                }
            }

        }

    }
}
