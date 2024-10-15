using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;

namespace Ekom.Umb.Models;

class Umbraco10Media : Umbraco10Content
{
    public Umbraco10Media(IPublishedContent content)
        : base(content, content.Url()) // or custom media-specific URL logic if needed
    {
    }
}
