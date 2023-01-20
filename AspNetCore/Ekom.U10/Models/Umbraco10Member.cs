using System.Collections.Generic;
using System.Linq;
using Umbraco.Cms.Core.Models;

namespace Ekom.Umb.Models
{
    class Umbraco10Member : Ekom.Models.UmbracoMember
    {
        public Umbraco10Member(IMember member)
            : base(new Dictionary<string, string>
            {
                { "id", member.Id.ToString() },
                { "__Key", member.Key.ToString() },
                { "nodeName", member.Name },
                { "loginName", member.Username }
            },
            member.Properties.ToDictionary(
                x => x.Alias, 
                x => x.GetValue()?.ToString()))
        { }
    }
}
