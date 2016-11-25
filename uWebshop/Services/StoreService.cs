using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using umbraco.cms.businesslogic.web;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Web;
using uWebshop.Cache;
using uWebshop.Models;

namespace uWebshop.Services
{
    public static class StoreService
    {
        private static readonly ILog Log =
                LogManager.GetLogger(
                    MethodBase.GetCurrentMethod().DeclaringType
                );

        public static IEnumerable<IDomain> GetAllStoreDomains()
        {
            var ds = ApplicationContext.Current.Services.DomainService;

            var domains = ds.GetAll(true).Where(x => !x.DomainName.Contains("*"));

            return domains;

        }

        public static Store GetStoreByDomain(string domain = "")
        {
            try
            {
                Store store = null;

                var storeDomain = StoreCache._storeDomainCache.FirstOrDefault(x => x.Value.DomainName.ToLower() == domain.ToLower()).Value;

                if (storeDomain != null)
                {
                    //var storeNode = StoreCache._storeNodeCache.FirstOrDefault(x => x.Value.Id == storeDomain.RootContentId).Value;

                    //if (storeNode != null)
                    //{
                        store = StoreCache._storeCache.FirstOrDefault(x => x.Value.StoreRootNode == storeDomain.RootContentId).Value;
                    //}
                }

                if (store == null)
                {
                    Log.Info("GetStoreByDomain, Could not find store. Returning first store in the list.");

                    store = StoreCache._storeCache.FirstOrDefault().Value;
                }

                return store;

            } catch(Exception ex)
            {
                Log.Error("GetStoreByDomain, Could not find store. Returning first store in the list.", ex);

                var store = StoreCache._storeCache.FirstOrDefault().Value;

                return store;
            }
 
        }

        public static Store GetStore()
        {
            var r = (ContentRequest)HttpContext.Current.Cache["uwbsRequest"];

            if (r != null)
            {
                return r.Store;
            }

            if (UmbracoContext.Current != null && UmbracoContext.Current.PublishedContentRequest != null)
            {
                return GetStoreByDomain(UmbracoContext.Current.PublishedContentRequest.UmbracoDomain.DomainName);
            }

            return null;
        }

    }
}
