using Ekom.Models;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;

namespace Ekom.Umb.Models
{
    class Umbraco10Content : UmbracoContent
    {
        public Umbraco10Content(IPublishedContent content)
            : base(new Dictionary<string, string>
            {
                { "id", content.Id.ToString() },
                { "parentID", content.Parent?.Id.ToString() ?? "" },
                { "__Key", content.Key.ToString() },
                { "nodeName", content.Name ?? "" },
                { "__NodeTypeAlias", content.ContentType.Alias },
                { "sortOrder", content.SortOrder.ToString() },
                { "level", content.Level.ToString() },
                { "__Path", content.Path },
                { "createDate", content.CreateDate.ToString("yyyy-MM-dd HH:mm:ss:fff") },
                { "updateDate", content.UpdateDate.ToString("yyyy-MM-dd HH:mm:ss:fff") },
                { "__VariesByCulture", content.Cultures.Count > 1 ? "y" : "n" },
                { "url", content.Url() }
            },
            content.Properties.ToDictionary(
                x => x.Alias,
                x => x.GetSourceValue()?.ToString()))
        {
        
        }

        public Umbraco10Content(IContent content)
            : base(new Dictionary<string, string>
            {
                { "id", content.Id.ToString() },
                { "parentID", content.ParentId.ToString() },
                { "__Key", content.Key.ToString() },
                { "nodeName", content.Name ?? ""},
                { "__NodeTypeAlias", content.ContentType.Alias },
                { "sortOrder", content.SortOrder.ToString() },
                { "level", content.Level.ToString() },
                { "__Path", content.Path },
                { "createDate", content.CreateDate.ToString("yyyy-MM-dd HH:mm:ss:fff") },
                { "updateDate", content.UpdateDate.ToString("yyyy-MM-dd HH:mm:ss:fff") },
                { "__VariesByCulture", content.AvailableCultures.Count() > 1 ? "y" : "n" },
                { "url", "#" }
            },
            content.Properties.ToDictionary(
                x => x.Alias,
                x => content.GetValue<string>(x.Alias)))
        { }
    }
}
