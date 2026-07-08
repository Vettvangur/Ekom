using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;

namespace Ekom.Umb.Models;

internal sealed class Umbraco17Media : Umbraco17Content
{
    public Umbraco17Media(IPublishedContent content)
        : base(content, urlOverride: content.Url())
    {
    }
}
