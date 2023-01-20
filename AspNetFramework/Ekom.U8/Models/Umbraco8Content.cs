using Ekom.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Models.PublishedContent;

namespace Ekom.U8.Models
{
    class Umbraco8Content : UmbracoContent
    {
        public Umbraco8Content(IPublishedContent content)
            : base(new Dictionary<string, string>
            {
                { "id", content.Id.ToString() },
                { "__Key", content.Key.ToString() },
                { "nodeName", content.Name },
                { "ContentTypeAlias", content.ContentType.Alias }
            },
            content.Properties.ToDictionary(
                x => x.Alias, 
                x => x.GetValue().ToString()))
        { }

        public Umbraco8Content(PublishedSearchResult content)
            : base(new Dictionary<string, string>
            {
                { "id", content.Content.Id.ToString() },
                { "__Key", content.Content.Key.ToString() },
                { "nodeName", content.Content.Name },
                { "ContentTypeAlias", content.Content.ContentType.Alias }
            },
            content.Content.Properties.ToDictionary(
                x => x.Alias,
                x => x.GetValue().ToString()))
        { }
    }
}
