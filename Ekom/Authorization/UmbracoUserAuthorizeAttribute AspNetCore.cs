#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Ekom.Services;
using Microsoft.AspNetCore.Http;

namespace Ekom.Authorization
{
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
}
#endif
