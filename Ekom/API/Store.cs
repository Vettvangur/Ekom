using Ekom.Models;
using Ekom.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;


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
        public static Store Instance => Configuration.Resolver.GetService<Store>();

        readonly ILogger<Store> _logger;
        readonly IStoreService _storeSvc;
        readonly Configuration _config;
        readonly INodeService _nodeService;
        /// <summary>
        /// ctor
        /// </summary>
        internal Store(
            ILogger<Store> logger,
            IStoreService storeService,
            Configuration config,
            INodeService nodeService
        )
        {
            _storeSvc = storeService;
            _logger = logger;
            _config = config;
            _nodeService = nodeService;
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
        /// Get store by domain
        /// </summary>
        /// <returns></returns>
        public IStore GetStoreByDomain(string domain, string culture)
        {
            return _storeSvc.GetStoreByDomain(domain, culture);
        }

        /// <summary>
        /// Get all stores
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IStore> GetAllStores()
        {
            return _storeSvc.GetAllStores();
        }


        /// <summary>
        /// Get domains
        /// </summary>
        /// <returns></returns>
        public IEnumerable<UmbracoDomain> GetDomains()
        {
            return _storeSvc.GetDomains();
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
