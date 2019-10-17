using System.Web.Mvc;
using System.Web.Routing;
using Umbraco.Core.Composing;
using Umbraco.Core.Configuration;

namespace Umbraco.Extensions.Events
{
    class RegisterCustomBackofficeMvcRouteComponent : IComponent
    {
        private readonly IGlobalSettings _globalSettings;
        public RegisterCustomBackofficeMvcRouteComponent(IGlobalSettings globalSettings)
        {
            _globalSettings = globalSettings;
        }
        public void Initialize()
        {
            RouteTable.Routes.MapRoute(
                name: "EkomManager",
                url: _globalSettings.GetUmbracoMvcArea() + "/backoffice/ekom/manager/{id}/{orderId}",
                defaults: new { controller = "Ekom", id = UrlParameter.Optional, orderId = "" }
            );

            // Is this really needed?
            //GlobalConfiguration.Configuration.Formatters.JsonFormatter.MediaTypeMappings
            //    .Add(new System.Net.Http.Formatting.RequestHeaderMapping("Accept",
            //      "text/html",
            //      StringComparison.InvariantCultureIgnoreCase,
            //      true,
            //      "application/json"));

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

        public void Terminate()
        {
        }
    }
}
