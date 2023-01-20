using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Owin;
using Owin;
using Umbraco.Core;
using Umbraco.Web.Security;
using Microsoft.Owin.Security.OpenIdConnect;

namespace Ekom.Site.Extensions
{
    public static class UmbracoADAuthExtensions
    {

        ///  <summary>
        ///  Configure ActiveDirectory sign-in
        ///  </summary>
        ///  <param name="app"></param>
        ///  <param name="tenant">
        ///  Your tenant ID i.e. YOURDIRECTORYNAME.onmicrosoft.com OR this could be the GUID of your tenant ID
        ///  </param>
        ///  <param name="clientId">
        ///  Also known as the Application Id in the azure portal
        ///  </param>
        ///  <param name="postLoginRedirectUri">
        ///  The URL that will be redirected to after login is successful, example: http://mydomain.com/umbraco/;
        ///  </param>
        ///  <param name="issuerId">
        /// 
        ///  This is the "Issuer Id" for you Azure AD application. This is a GUID value of your tenant ID.
        /// 
        ///  If this value is not set correctly then accounts won't be able to be detected 
        ///  for un-linking in the back office. 
        /// 
        ///  </param>
        /// <param name="caption"></param>
        /// <param name="style"></param>
        /// <param name="icon"></param>
        /// <remarks>
        /// 
        ///  ActiveDirectory account documentation for ASP.Net Identity can be found:
        ///  https://github.com/AzureADSamples/WebApp-WebAPI-OpenIDConnect-DotNet
        /// 
        ///  </remarks>
        public static void ConfigureBackOfficeAzureActiveDirectoryAuth(this IAppBuilder app, 
            string tenant, string clientId, string postLoginRedirectUri, Guid issuerId,
            string caption = "Active Directory", string style = "btn-microsoft", string icon = "fa-windows")
        {         
            var authority = string.Format(
                CultureInfo.InvariantCulture, 
                "https://login.windows.net/{0}", 
                tenant);

            var adOptions = new OpenIdConnectAuthenticationOptions
            {
                SignInAsAuthenticationType = Constants.Security.BackOfficeExternalAuthenticationType,
                ClientId = clientId,
                Authority = authority,
                RedirectUri = postLoginRedirectUri
            };

            adOptions.ForUmbracoBackOffice(style, icon);            
            adOptions.Caption = caption;
            //Need to set the auth tyep as the issuer path
            adOptions.AuthenticationType = string.Format(
                CultureInfo.InvariantCulture,
                "https://sts.windows.net/{0}/",
                issuerId);

            var autoLinkOptions = new ExternalSignInAutoLinkOptions(
                autoLinkExternalAccount: true,
                defaultUserGroups: new[] { "admin" }, // on older installs use defaultUserType: "admin", and add defaultAllowedSections, see Seatours
                defaultCulture: "en-US");


            adOptions.SetExternalSignInAutoLinkOptions(autoLinkOptions);


            adOptions.Notifications = new OpenIdConnectAuthenticationNotifications()
            {
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
                SecurityTokenValidated = async n =>
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
                {
                    try
                    {
                        var id = n.AuthenticationTicket.Identity;
                        id.AddClaim(
                          new Claim(
                            ClaimTypes.Email,
                            id.FindFirst(ClaimTypes.Upn).Value
                          )
                        );
                    }
                    catch (Exception ex) when (ex is NullReferenceException | ex is ArgumentNullException)
                    {
                        throw new Exception("Error linking logged in AAD account to umbraco user. Are you logged in with a Microsoft account instead of an AAD account?");
                    }
                }
            };

            app.UseOpenIdConnectAuthentication(adOptions);            
        }    

    }
    
}
