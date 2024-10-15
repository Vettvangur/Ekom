using Ekom.Models;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;

namespace Ekom.Umb.Models;

class Umbraco10Content : UmbracoContent
{
    public Umbraco10Content(
        IPublishedContent content,
        string? urlOverride = null)
        : base(new Dictionary<string, string>
        {
            { "id", content.Id.ToString() },
            { "parentID", content.Parent?.Id.ToString() ?? "" },
            { "parentKey", content.Parent?.Key.ToString() ?? Guid.Empty.ToString() },
            { "__Key", content.Key.ToString() },
            { "nodeName", content.Name ?? "" },
            { "__NodeTypeAlias", content.ContentType.Alias },
            { "sortOrder", content.SortOrder.ToString() },
            { "level", content.Level.ToString() },
            { "__Path", content.Path },
            { "createDate", content.CreateDate.ToString("yyyy-MM-dd HH:mm:ss:fff") },
            { "updateDate", content.UpdateDate.ToString("yyyy-MM-dd HH:mm:ss:fff") },
            { "__VariesByCulture", content.Cultures.Count > 1 ? "y" : "n" },
            // Only used for media
            { "url", urlOverride ?? "#" }
        },
        content.Properties
            .Where(x => !string.IsNullOrEmpty(x.Alias))
            .ToDictionary(
                pair => pair.Alias,
                pair =>
                {
                    try
                    {
                        return pair.PropertyType.VariesByCulture()
                            ? pair.GetSourceValue(content.Cultures.FirstOrDefault().Value?.Culture)?.ToString() ?? ""
                            : pair.GetSourceValue()?.ToString() ?? "";
                    }
                    catch
                    {
                        throw new Exception($"Failed to GetSourceValue for: {pair.Alias} Node: {content.Id}");
                    }
                }))
    {
    }


    public Umbraco10Content(IContent content, Guid ParentKey)
        : base(new Dictionary<string, string>
        {
            { "id", content.Id.ToString() },
            { "parentID", content.ParentId.ToString() },
            { "parentKey",ParentKey.ToString() },
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
