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

        public Umbraco10Content(PublishedSearchResult content)
            : base(new Dictionary<string, string>
            {
                { "id", content.Content.Id.ToString() },
                { "parentID", content.Content.Parent?.Id.ToString() ?? "" },
                { "__Key", content.Content.Key.ToString() },
                { "nodeName", content.Content.Name ?? "" },
                { "__NodeTypeAlias", content.Content.ContentType.Alias },
                { "sortOrder", content.Content.SortOrder.ToString() },
                { "level", content.Content.Level.ToString() },
                { "__Path", content.Content.Path },
                { "createDate", content.Content.CreateDate.ToString("yyyy-MM-dd HH:mm:ss:fff") },
                { "updateDate", content.Content.UpdateDate.ToString("yyyy-MM-dd HH:mm:ss:fff") },
                { "__VariesByCulture", content.Content.Cultures.Count > 1 ? "y" : "n" },
                { "url", content.Content.Url() }
            },
            content.Content.Properties.ToDictionary(
                x => x.Alias,
                x => x.GetSourceValue()?.ToString()))
        { }

        private static new Dictionary<string, string> GetProperties(IEnumerable<IPublishedProperty> properties, IReadOnlyDictionary<string, PublishedCultureInfo> cultures, IPublishedContent content)
        {
            
            var dict = new Dictionary<string, string>();

            if (properties == null) { return dict; }

            foreach (var prop in properties)
            {

                //if (prop.PropertyType.VariesByCulture())
                //{
                //    foreach (var culture in cultures)
                //    {
                //        var value = prop.GetSourceValue(culture.Value.Culture);

                //        dict.Add(prop.Alias, value?.ToString());
                //    }
                //} else
                //{

                //}

                var value = prop.GetSourceValue();

                dict.Add(prop.Alias, value != null ? value.ToString() : "");
            }

            return dict;
        }
    }
}
