using Microsoft.Owin;
using Owin;
using Umbraco.Web;
using Ekom.Site.Extensions;
using System.Configuration;
using System;

//To use this startup class, change the appSetting value in the web.config called 
// "owin:appStartup" to be "UmbracoStandardOwinStartup"

[assembly: OwinStartup("UmbracoStandardOwinStartup", typeof(UmbracoStandardOwinStartup))]

namespace Ekom.Site.Extensions
{
    /// <summary>
    /// A standard way to configure OWIN for Umbraco
    /// </summary>
    /// <remarks>
    /// The startup type is specified in appSettings under owin:appStartup - change it to "UmbracoStandardOwinStartup" to use this class.
    /// </remarks>
    public class UmbracoStandardOwinStartup : UmbracoDefaultOwinStartup
    {
        /// <summary>
        /// Configures the back office authentication for Umbraco
        /// </summary>
        /// <param name="app"></param>
        protected override void ConfigureUmbracoAuthentication(IAppBuilder app)
        {
            base.ConfigureUmbracoAuthentication(app);

            app.ConfigureBackOfficeAzureActiveDirectoryAuth(
              tenant: ConfigurationManager.AppSettings["AzureAd.TenantId"],
              clientId: ConfigurationManager.AppSettings["AzureAd.ClientId"],
              postLoginRedirectUri: ConfigurationManager.AppSettings["AzureAd.RedirectUrl"],
              issuerId: new Guid(ConfigurationManager.AppSettings["AzureAd.TenantId"]));
        }
    }
}
