using Ekom.Models;
using Ekom.Umb.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Ekom.Umb.Services
{
    class MemberService : Ekom.Services.IMemberService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        public MemberService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public UmbracoMember GetByUsername(string userName)
        {
            var serviceContext = _httpContextAccessor.HttpContext?.RequestServices.GetService<Umbraco.Cms.Core.Services.IMemberService>();
            var m = serviceContext?.GetByUsername(userName);

            if (m == null)
            {
                return null;
            }

            return new Umbraco10Member(m);
        }

        public UmbracoMember GetCurrentMember()
        {
            var serviceContext = _httpContextAccessor.HttpContext?.RequestServices.GetService<Umbraco.Cms.Core.Services.IMemberService>();
            var managerContext = _httpContextAccessor.HttpContext?.RequestServices.GetService<IMemberManager>();
            var m = managerContext?.GetCurrentMemberAsync();
            IMember? member = serviceContext?.GetById(Convert.ToInt32(m?.Id));
            if (member == null)
            {
                return null;
            }
            return new Umbraco10Member(member);
        }

        public void Save(Dictionary<string, object> data, UmbracoMember member)
        {
            Save(data,member);
        }

        public void Save(Dictionary<string, object> data, string userName)
        {
            var serviceContext = _httpContextAccessor.HttpContext?.RequestServices.GetService<Umbraco.Cms.Core.Services.IMemberService>();
            var m = serviceContext?.GetByUsername(userName);

            if (m == null)
            {
                return;
            }

            foreach (var d in data)
            {
                if (m.HasProperty(d.Key))
                {
                    m.SetValue(d.Key, d.Value);
                }
                
            }

            serviceContext?.Save(m);
           
        }
    }
}
