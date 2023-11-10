using Ekom.Models;
using Ekom.Umb.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Security;

namespace Ekom.Umb.Services
{
    class MemberService : Ekom.Services.IMemberService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<MemberService> _logger;
        public MemberService(IHttpContextAccessor httpContextAccessor, ILogger<MemberService> logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public UmbracoMember GetByUsername(string userName)
        {
            try
            {
                var serviceContext = _httpContextAccessor.HttpContext?.RequestServices.GetService<Umbraco.Cms.Core.Services.IMemberService>();
                var m = serviceContext?.GetByUsername(userName);

                return m == null ? null : new Umbraco10Member(m);
            } catch(Exception ex)
            {
                _logger.LogError(ex, "Failed to Get user by username: " + userName);

                return null;
            }

        }

        public async Task<UmbracoMember> GetCurrentMember()
        {
            var managerContext = _httpContextAccessor.HttpContext?.RequestServices.GetService<IMemberManager>();

            if (managerContext == null)
            {
                return null;
            }

            var m = await managerContext.GetCurrentMemberAsync();
            var publishedMember = managerContext.AsPublishedMember(m);
            
            return publishedMember == null ? null : new Umbraco10Member(publishedMember, m.UserName);
        }

        public void Save(Dictionary<string, object> data, UmbracoMember member)
        {
            Save(data,member);
        }

        public void Save(Dictionary<string, object> data, string userName)
        {
            if (string.IsNullOrEmpty(userName))
            {
                return;
            }

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
