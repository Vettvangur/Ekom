using Microsoft.AspNetCore.Authorization;

namespace Ekom.Authorization;

/// <summary>
/// In AspNetCore we mostly rely on the policy and 
/// it's associated IAuthorizationHandler to do the work
/// </summary>
public sealed class UmbracoUserAuthorizeAttribute : AuthorizeAttribute
{
    public UmbracoUserAuthorizeAttribute()
        : base("UmbracoUser")
    {
    }
}

