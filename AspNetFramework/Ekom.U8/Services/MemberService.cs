using Ekom.Models;
using Ekom.Services;
using Ekom.U8.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using Umbraco.Web;
using Umbraco.Web.Security;

namespace Ekom.U8.Services
{
    class MemberService : Ekom.Services.IMemberService
    {
        private readonly Umbraco.Core.Services.IMemberService _ms;
        private readonly MembershipHelper _membershipHelper;
        public MemberService(Umbraco.Core.Services.IMemberService ms, MembershipHelper membershipHelper)
        {
            _ms = ms;
            _membershipHelper = membershipHelper;
        }

        public UmbracoMember GetByUsername(string userName)
        {
            var m = _ms.GetByUsername(userName);

            if (m == null)
            {
                return null;
            }

            return new Umbraco8Member(m);
        }

        public Task<UmbracoMember> GetCurrentMemberAsync()
        {
            var m = _membershipHelper.GetCurrentMember();
            var umbMember = new Umbraco8Member(m);
            return Task.FromResult(umbMember as UmbracoMember);
        }

        public void Save(Dictionary<string, object> data, UmbracoMember member)
        {
            Save(data,member);
        }

        public void Save(Dictionary<string, object> data, string userName)
        {
            var m = _ms.GetByUsername(userName);

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

            _ms.Save(m);
        }
    }
}
