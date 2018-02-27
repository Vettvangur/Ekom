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
        private static Store _current;
        /// <summary>
        /// Store Singleton
        /// </summary>
        public static Store Current
        {
            get
            {
                return _current ?? (_current = Configuration.container.GetInstance<Store>());
            }
        }

        ILog _log;
        ApplicationContext _appCtx;
        ICacheProvider _reqCache => _appCtx.ApplicationCache.RequestCache;
        IStoreService _storeSvc => Configuration.container.GetInstance<IStoreService>();

        /// <summary>
        /// ctor
        /// </summary>
        public Store(ApplicationContext appCtx, ILogFactory logFac)
        {
            _appCtx = appCtx;

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
