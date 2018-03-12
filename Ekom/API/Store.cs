using Ekom.Interfaces;
using Ekom.Services;
using log4net;
using System.Collections.Generic;
using Umbraco.Core;
using Umbraco.Core.Cache;

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
        public static Store Instance => Configuration.container.GetInstance<Store>();

        ILog _log;
        ApplicationContext _appCtx;
        IStoreService _storeSvc;
        ICacheProvider _reqCache => _appCtx.ApplicationCache.RequestCache;

        /// <summary>
        /// ctor
        /// </summary>
        internal Store(
            ApplicationContext appCtx,
            ILogFactory logFac,
            IStoreService storeService
        )
        {
            _appCtx = appCtx;
            _storeSvc = storeService;
            _log = logFac.GetLogger(typeof(Store));
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
