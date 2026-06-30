using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Ekom.Umb.Models;

internal sealed class Umbraco17Member : Ekom.Models.UmbracoMember
{
    public Umbraco17Member(IMember member)
        : base(new Dictionary<string, string>
        {
            { "id", member.Id.ToString() },
            { "__Key", member.Key.ToString() },
            { "nodeName", member.Name ?? string.Empty },
            { "loginName", member.Username },
        },
        member.Properties.ToDictionary(
            x => x.Alias,
            x => x.GetValue()?.ToString() ?? string.Empty))
    {
    }

    public Umbraco17Member(IPublishedContent member, string userName)
        : base(new Dictionary<string, string>
        {
            { "id", member.Id.ToString() },
            { "__Key", member.Key.ToString() },
            { "nodeName", member.Name ?? string.Empty },
            { "loginName", userName },
        },
        member.Properties.ToDictionary(
            x => x.Alias,
            x => x.GetValue()?.ToString() ?? string.Empty))
    {
    }
}
