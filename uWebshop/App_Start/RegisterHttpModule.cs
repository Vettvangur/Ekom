using Microsoft.Web.Infrastructure.DynamicModuleHelper;

[assembly: WebActivatorEx.PreApplicationStartMethod(typeof(uWebshop.App_Start.RegisterHttpModule), "RegisterModules")]
namespace uWebshop.App_Start
{
    /// <summary>
    /// Registers the uWebshop HttpModule into the request pipeline.
    /// This eliminates the explicit web.config/system.webserver/modules configuration 
    /// </summary>
    static class RegisterHttpModule
    {
        /// <summary>
        /// Registers the uWebshop HttpModule into the request pipeline.
        /// This eliminates the explicit web.config/system.webserver/modules configuration 
        /// </summary>
        public static void RegisterModules()
        {
            DynamicModuleUtility.RegisterModule(typeof(HttpModule));
        }
    }
}
