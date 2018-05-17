using Ekom.Interfaces;
using Ekom.IoC;
using Ekom.Repository;
using System.Collections.Generic;
using System.Web.Mvc;
using System.Web.Routing;
using umbraco;
using Umbraco.Core;

namespace Umbraco.Extensions.Events
{
    class EkomEvents : ApplicationEventHandler
    {
        protected override void ApplicationStarted(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            Ekom.EkomStartup.ApplicationStartedCalled += EkomStartup_ApplicationStarted;

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

        private void EkomStartup_ApplicationStarted(IList<IContainerRegistration> typeMappings)
        {
            typeMappings.Add(
                new ContainerRegistration<IManagerRepository>(
                    Lifetime.Transient, c => c.GetInstance<ManagerRepository>()
                )
            );
        }
    }
}
