using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web.Routing;
using umbraco;
using Umbraco.Core;

namespace Ekom.App_Start
{
    class IAppEventHandler : IApplicationEventHandler
    {
        /// <summary> 
        /// Umbraco lifecycle method 
        /// </summary>  
        /// <param name="httpApplication"></param> 
        /// <param name="applicationContext"></param> 
        public void OnApplicationStarted(UmbracoApplicationBase httpApplication, ApplicationContext applicationContext)
        {

            RouteTable.Routes.MapRoute(
                name: "EkomManager",
                url: GlobalSettings.UmbracoMvcArea + "/backoffice/ekom/{action}/{id}",
                defaults: new { controller = "Ekom", action = "Manager", id = UrlParameter.Optional }
            );

            //RouteTable.Routes.RouteExistingFiles = true;
            //RouteTable.Routes.MapPageRoute("ekomManager",
            //    GlobalSettings.UmbracoMvcArea + "/backoffice/ekom/{*pathInfo}", 
            //    "~/views/ekomManager/index.cshtml"
            //);



            //RouteTable.Routes.IgnoreRoute("dist/{*pathInfo}");

            //RouteTable.Routes.MapRoute(
            //    name: "ekomManager",
            //    url: GlobalSettings.UmbracoMvcArea + "/backoffice/ekomManager/{action}/{id}",
            //    defaults: new {
            //        controller = "EkomManager",
            //        action = "Dashboard",
            //        id = UrlParameter.Optional
            //    }
            //);

            //RouteTable.Routes.MapRoute(
            //    name: "ekomManager",
            //    url: GlobalSettings.UmbracoMvcArea + "/backoffice/ekomManager/{action}/{id}",
            //    defaults: new
            //    {
            //        controller = "EkomManager",
            //        action = "Index",
            //        id = UrlParameter.Optional
            //    });


        }

        public void OnApplicationInitialized(UmbracoApplicationBase httpApplication, ApplicationContext applicationContext)
        {
        }

        public void OnApplicationStarting(UmbracoApplicationBase httpApplication, ApplicationContext applicationContext)
        {
        }
    }
}
