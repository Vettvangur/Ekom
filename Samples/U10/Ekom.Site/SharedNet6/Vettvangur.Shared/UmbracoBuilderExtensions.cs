using Microsoft.Identity.Web;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Web.BackOffice.Security;
using Umbraco.Extensions;

namespace Vettvangur.Shared
{
    public static class UmbracoBuilderExtensions
    {
        const string _vettvangurTenantId = "641805e6-66ba-4840-96c5-1942bc87687f";
        const string _vettvangurClientId = "7d1d0244-45e2-4f7d-a899-6ef6676bbb6e";
        const string _vettvangurGlobalUmbracoAdmins = "15f93c9c-c870-48cf-83de-bb97dde7bc92";

        public static IUmbracoBuilder AddBackofficeAzureAd(this IUmbracoBuilder umbracoBuilder, string configSectionName = "Vettvangur:AzureAd")
        {
            var azAdConfig = umbracoBuilder.Config.GetSection(configSectionName);

            var groupConstraint = azAdConfig["GroupConstraint"] ?? _vettvangurGlobalUmbracoAdmins;
            var extLoginOpts = new ExternalSignInAutoLinkOptions(
                autoLinkExternalAccount: true,
                defaultUserGroups: new[] { "admin" },
                defaultCulture: "en-US",
                allowManualLinking: true)
            {
                OnAutoLinking = (user, loginInfo) =>
                {
                    user.IsApproved = true;
                },
                OnExternalLogin = (user, loginInfo) =>
                {
                    var groups = loginInfo.Principal.Claims.Where(x => x.Type == "groups");
                    return groups?.Any(x => x.Value == groupConstraint) == true;
                },
            };

            return umbracoBuilder.AddBackOfficeExternalLogins(extLoginBuilder =>
                extLoginBuilder.AddBackOfficeLogin(
                auth =>
                {
                    auth
                        // https://github.com/umbraco/Umbraco-CMS/pull/9470
                        .AddMicrosoftIdentityWebApp(options =>
                        {
                            options.CallbackPath = "/umbraco-signin-oidc/";
                            options.Instance = "https://login.microsoftonline.com/";
                            options.TenantId = azAdConfig["TenantId"] ?? _vettvangurTenantId;
                            options.ClientId = azAdConfig["ClientId"] ?? _vettvangurClientId;
                            options.SignedOutRedirectUri = "/umbraco";

                            // https://github.com/AzureAD/microsoft-identity-web/issues/749
                            //options.ClaimActions.MapJsonKey(ClaimTypes.Email, ClaimConstants.PreferredUserName);

                            // Preferred over IClaimsTransformation which runs for every AuthenticateAsync
                            options.Events.OnTokenValidated = ctx =>
                            {
                                var username = ctx.Principal?.Claims.FirstOrDefault(c => c.Type == ClaimConstants.PreferredUserName);
                                if (username != null && ctx.Principal?.Identity is ClaimsIdentity claimsIdentity)
                                {
                                    claimsIdentity.AddClaim(
                                        new Claim(
                                            ClaimTypes.Email,
                                            username.Value
                                        )
                                    );
                                }

                                return Task.CompletedTask;
                            };
                        },
                        openIdConnectScheme: auth.SchemeForBackOffice(Constants.AzureAd),
                        cookieScheme: "Fake"
                        );
                },
                loginProviderOptions =>
                {
                    loginProviderOptions.ButtonStyle = "btn-microsoft";
                    loginProviderOptions.Icon = "fa-windows";
                    loginProviderOptions.AutoLinkOptions = extLoginOpts;
                    loginProviderOptions.AutoRedirectLoginToExternalProvider = false;
                })
            );
        }
    }
}
