using Ekom.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;

namespace Vettvangur.OrganisationManagement.AspNetCore;

class UmbracoUserAuthorization : IAuthorizationRequirement
{

}
/// <summary>
/// https://learn.microsoft.com/en-us/aspnet/core/security/authorization/policies?view=aspnetcore-6.0
/// </summary>
class UmbracoUserAuthorizationHandler : AuthorizationHandler<UmbracoUserAuthorization>
{
    readonly IHttpContextAccessor _httpContextAccessor;

    public UmbracoUserAuthorizationHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, UmbracoUserAuthorization requirement)
    {
        var usrMgmtAuthSvc = _httpContextAccessor.HttpContext?.RequestServices.GetService<ISecurityService>();
        var userGroups = usrMgmtAuthSvc?.GetUmbracoUserGroups();

        if (userGroups != null)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
