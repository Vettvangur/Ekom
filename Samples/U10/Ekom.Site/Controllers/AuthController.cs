using Ekom.Site.Models;
using Ekom.Utilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Win32;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Logging;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Web.Common.Filters;
using Umbraco.Cms.Web.Common.Security;
using Umbraco.Cms.Web.Website.Controllers;
using Umbraco.Cms.Web.Website.Models;

namespace Ekom.Site.Controllers
{
    public class AuthController : SurfaceController
    {
        private readonly ILogger<AuthController> _log;
        private readonly IMemberService _ms;
        private readonly IMemberSignInManager _memberSignInManager;
        private readonly IMemberManager _memberManager;
        private readonly ICoreScopeProvider _coreScopeProvider;
        public AuthController(IUmbracoContextAccessor umbracoContextAccessor,
                              IUmbracoDatabaseFactory databaseFactory,
                              ServiceContext services,
                              AppCaches appCaches,
                              IProfilingLogger profilingLogger,
        IPublishedUrlProvider publishedUrlProvider,
                              ILogger<AuthController> log,
                              IMemberService ms,
                              IMemberSignInManager memberSignInManager,
                              IMemberManager memberManager, 
                              ICoreScopeProvider coreScopeProvider)
    : base(umbracoContextAccessor, databaseFactory, services, appCaches, profilingLogger, publishedUrlProvider)
        {
            _log = log;
            _ms = ms;
            _memberSignInManager = memberSignInManager;
            _memberManager = memberManager;
            _coreScopeProvider = coreScopeProvider;
        }

        [HttpPost]
        [ValidateUmbracoFormRouteString]
        public async Task<IActionResult> Login(Login model)
        {

            try
            {
                if (!ModelState.IsValid)
                {
                    return RedirectToCurrentUmbracoPage(QueryString.Create("error","invalidData"));
                }

                var result = await _memberSignInManager.PasswordSignInAsync(model.Username, model.Password, true, false);

                return result.Succeeded ? RedirectToCurrentUmbracoPage(QueryString.Create("success", "true")) : RedirectToCurrentUmbracoPage(QueryString.Create("error", "incorrectPassword"));
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Login server error");
                return RedirectToCurrentUmbracoPage(QueryString.Create("error", "servererror"));
            }
        }

        [HttpGet]
        [IgnoreAntiforgeryToken]
        public async Task<string> UpdatePrice()
        {

            var content = Services.ContentService.GetById(1167);

            content.SetPrice2("Store", "en-US", 585);

            Services.ContentService.SaveAndPublish(content);
            return "Success";

        }

        [HttpGet]
        [IgnoreAntiforgeryToken]
        public async Task<ActionResult> Logout()
        {
            try
            {
                await _memberSignInManager.SignOutAsync();
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error - Logout");
            }

            return Redirect("/");
        }

        [HttpPost]
        [ValidateUmbracoFormRouteString]
        public async Task<IActionResult> Register(Register model)
        {

            try
            {
                if (!ModelState.IsValid)
                {
                    return RedirectToCurrentUmbracoPage(QueryString.Create("error", "invalidData"));
                }

                if (model.Password.Length < 8)
                {
                    return RedirectToCurrentUmbracoPage(QueryString.Create("error", "passwordLength"));
                }

                var member = await _memberManager.FindByNameAsync(model.Username);

                if (member != null)
                {
                    return RedirectToCurrentUmbracoPage(QueryString.Create("error", "userExist"));
                }
                
                var result = await RegisterMemberAsync(new RegisterModel()
                {
                    Name = model.Username,
                    Username = model.Username,
                    Password = model.Password,
                    Email = model.Username,
                    UsernameIsEmail = false,
                    MemberTypeAlias = "Member",
                    ConfirmPassword = model.Password
                });

                return result.Succeeded ? RedirectToCurrentUmbracoPage(QueryString.Create("success", "true")) : RedirectToCurrentUmbracoPage(QueryString.Create("error", "registerFailed"));
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Register server error");

                return RedirectToCurrentUmbracoPage(QueryString.Create("error", "serverError"));
            }
        }

        /// <summary>
        /// <param name="model">Register member model.</param>
        /// <param name="logMemberIn">Flag for whether to log the member in upon successful registration.</param>
        /// <returns>Result of registration operation.</returns>
        /// </summary>
        private async Task<IdentityResult> RegisterMemberAsync(RegisterModel model, bool logMemberIn = true)
        {
            using ICoreScope scope = _coreScopeProvider.CreateCoreScope(autoComplete: true);

            if (string.IsNullOrEmpty(model.Name) && string.IsNullOrEmpty(model.Email) == false)
            {
                model.Name = model.Email;
            }

            model.Username = model.UsernameIsEmail || model.Username == null ? model.Email : model.Username;

            var identityUser =
                MemberIdentityUser.CreateNew(model.Username, model.Email, model.MemberTypeAlias, true, model.Name);
            IdentityResult identityResult = await _memberManager.CreateAsync(
            identityUser,
            model.Password);

            if (identityResult.Succeeded)
            {

                IMember? member = _ms.GetByKey(identityUser.Key);
                if (member == null)
                {

                    throw new InvalidOperationException($"Could not find a member with key: {member?.Key}.");
                }

                foreach (MemberPropertyModel property in model.MemberProperties.Where(p => p.Value != null).Where(property => member.Properties.Contains(property.Alias)))
                {
                    member.Properties[property.Alias]?.SetValue(property.Value);
                }

                string memberGroup = "Members";
                AssignMemberGroup(model.Email, memberGroup);

                _ms.Save(member);

                if (logMemberIn)
                {
                    await _memberSignInManager.SignInAsync(identityUser, false);
                }
            }

            return identityResult;
        }

        private void AssignMemberGroup(string email, string group)
        {
            try
            {
                _ms.AssignRole(email, group);
            }
            catch (Exception ex)
            {
                //handle the exception
            }

        }
    }
}
