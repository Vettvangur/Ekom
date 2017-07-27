using log4net;
using Microsoft.Practices.Unity;
using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Cache;
using uWebshop.App_Start;
using uWebshop.Cache;
using uWebshop.Models;
using uWebshop.Services;

namespace uWebshop.API
{
    public class Store
    {
        private static Store _current;
        public static Store Current
        {
            get
            {
                return _current ?? (_current = UnityConfig.GetConfiguredContainer().Resolve<Store>());
            }
        }

        ILog _log;
        ApplicationContext _appCtx;
        ICacheProvider _reqCache => _appCtx.ApplicationCache.RequestCache;

        StoreService _storeSvc;

        /// <summary>
        /// ctor
        /// </summary>
        public Store(ApplicationContext appCtx, StoreService storeSvc, ILogFactory logFac)
        {
            _appCtx = appCtx;
            _storeSvc = storeSvc;

            _log = logFac.GetLogger(typeof(Store));
        }

        public Models.Store GetStore()
        {
            return _storeSvc.GetStoreFromCache();
        }

        public Models.Store GetStore(string storeAlias)
        {
            return _storeSvc.GetStoreByAlias(storeAlias);
        }

        public IEnumerable<Models.Store> GetAllStores()
        {
            return _storeSvc.GetAllStores();
        }
    }
}
