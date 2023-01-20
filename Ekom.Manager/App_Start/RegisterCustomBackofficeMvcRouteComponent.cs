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
                url: _globalSettings.GetUmbracoMvcArea() + "/backoffice/ekom/Manager/{action}/{id}/{b}/{c}",
                defaults: new { controller = "Ekom", action = "Index", id = UrlParameter.Optional, b = UrlParameter.Optional, c = UrlParameter.Optional }
            );

        }

        public void Terminate()
        {
        }
    }
}
