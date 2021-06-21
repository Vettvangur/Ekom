using Ekom.Interfaces;
using System.Collections.Generic;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Composing;
using Umbraco.Core.Logging;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Web;

namespace Ekom.API
{
    /// <summary>
    /// The Ekom API, get current or all stores.
    /// </summary>
    public class Store
    {
        private UmbracoHelper umbHelper => Current.Factory.GetInstance<UmbracoHelper>();
        /// <summary>
        /// Store Instance
        /// </summary>
        public static Store Instance => Current.Factory.GetInstance<Store>();

        readonly ILogger _logger;
        readonly IStoreService _storeSvc;
        readonly IAppCache _reqCache;
        readonly Configuration _config;
        /// <summary>
        /// ctor
        /// </summary>
        internal Store(
            AppCaches appCaches,
            ILogger logger,
            IStoreService storeService,
            Configuration config
        )
        {
            _reqCache = appCaches.RequestCache;
            _storeSvc = storeService;
            _logger = logger;
            _config = config;
        }

        /// <summary>
        /// Get store from <see cref="Ekom.Models.ContentRequest"/> or first store available
        /// </summary>
        /// <returns></returns>
        public IStore GetStore()
        {
            return _storeSvc.GetStoreFromCache();
        }

        /// <summary>
        /// Get store by alias
        /// </summary>
        /// <param name="storeAlias"></param>
        /// <returns></returns>
        public IStore GetStore(string storeAlias)
        {
            return _storeSvc.GetStoreByAlias(storeAlias);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IStore> GetAllStores()
        {
            return _storeSvc.GetAllStores();
        }

        public IPublishedContent GetRootNode(IPublishedContent currentNode)
        {
            // Add Cache

            var root = currentNode.AncestorOrSelf(1);

            if (root.ContentType.Alias == "ekom")
            {
                var local = GetStore();

                if (local != null)
                {
                    var storeNode = umbHelper.Content(local.StoreRootNode);

                    if (storeNode != null)
                    {
                        return storeNode;
                    }
                }
            }

            return root;
        }

        public void RefreshCache()
        {
            foreach (var cacheEntry in _config.CacheList.Value)
            {
                cacheEntry.FillCache();
            }
        }

    }
}
