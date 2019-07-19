using Ekom.Interfaces;
using Ekom.Services;
using System.Collections.Generic;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Composing;
using Umbraco.Core.Logging;

namespace Ekom.API
{
    /// <summary>
    /// The Ekom API, get current or all stores.
    /// </summary>
    public class Store
    {
        /// <summary>
        /// Store Instance
        /// </summary>
        public static Store Instance => Current.Factory.GetInstance<Store>();

        readonly ILogger _logger;
        readonly IStoreService _storeSvc;
        readonly IAppCache _reqCache;

        /// <summary>
        /// ctor
        /// </summary>
        internal Store(
            AppCaches appCaches,
            ILogger logger,
            IStoreService storeService
        )
        {
            _reqCache = appCaches.RequestCache;
            _storeSvc = storeService;
            _logger = logger;
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
    }
}
