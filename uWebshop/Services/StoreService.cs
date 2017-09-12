using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Models;
using Umbraco.Web;
using uWebshop.Cache;
using uWebshop.Interfaces;
using uWebshop.Models;

namespace uWebshop.Services
{
	class StoreService : IStoreService
	{
		ILog _log;
		ApplicationContext _appCtx;
		ICacheProvider _reqCache => _appCtx.ApplicationCache.RequestCache;
		IBaseCache<IDomain> _domainCache;
		IBaseCache<Store> _storeCache;

		/// <summary>
		/// ctor
		/// </summary>
		/// <param name="logFac"></param>
		/// <param name="domainCache"></param>
		/// <param name="storeCache"></param>
		/// <param name="appCtx"></param>
		public StoreService(
			ILogFactory logFac,
			IBaseCache<IDomain> domainCache,
			IBaseCache<Store> storeCache,
			ApplicationContext appCtx
		)
		{
			_log = logFac.GetLogger(typeof(StoreService));
			_appCtx = appCtx;
			_domainCache = domainCache;
			_storeCache = storeCache;
		}

		public Store GetStoreByDomain(string domain = "")
		{
			Store store = null;

			if (!string.IsNullOrEmpty(domain))
			{
				var storeDomain
					= _domainCache.Cache
									  .FirstOrDefault
										  (x => x.Value.DomainName.Equals(domain, StringComparison.InvariantCultureIgnoreCase))
									  .Value;

				if (storeDomain != null)
				{
					store = _storeCache.Cache
									  .FirstOrDefault
										(x => x.Value.StoreRootNode == storeDomain.RootContentId)
									  .Value;
				}
			}

			if (store == null)
			{
				store = _storeCache.Cache.FirstOrDefault().Value;
			}

			return store;
		}

		public Store GetStoreByAlias(string alias)
		{
			return _storeCache.Cache
							 .FirstOrDefault(x => x.Value.Alias.InvariantEquals(alias))
							 .Value;
		}

		public Store GetStoreFromCache()
		{
			var r = _reqCache.GetCacheItem("uwbsRequest") as ContentRequest;

			return r?.Store;
		}

		public IEnumerable<Store> GetAllStores()
		{
			return _storeCache.Cache.Select(x => x.Value).OrderBy(x => x.Level);
		}

		/// <summary>
		/// Gets the current store from available request data <para/>
		/// Saves store in cache and cookies
		/// </summary>
		/// <param name="umbracoDomain"></param>
		/// <param name="httpContext"></param>
		public Store GetStore(IDomain umbracoDomain, HttpContextBase httpContext)
		{
			HttpCookie storeInfo = httpContext.Request.Cookies["StoreInfo"];
			string storeAlias = storeInfo?.Values["StoreAlias"];

			Store store = null;

			// Attempt to retrieve Store from cookie data
			if (!string.IsNullOrEmpty(storeAlias))
			{
				store = GetStoreByAlias(storeAlias);
			}

			// Get by Domain
			if (store == null && umbracoDomain != null)
			{
				store = GetStoreByDomain(umbracoDomain.DomainName);
			}

			// Grab default store / First store from cache if no umbracoDomain present
			if (store == null)
			{
				store = GetStoreByDomain();
			}

			return store;
		}
	}
}
