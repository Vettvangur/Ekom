using Microsoft.Web.Infrastructure.DynamicModuleHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

[assembly: PreApplicationStartMethod(typeof(uWebshop.App_Start.RegisterHttpModule), "RegisterModules")]
namespace uWebshop.App_Start
{
    /// <summary>
    /// Registers the uWebshop HttpModule into the request pipeline.
    /// This eliminates the explicit web.config/system.webserver/modules configuration 
    /// </summary>
    public class RegisterHttpModule
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
