using Ekom.Models;
using System.Text.Json;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;

namespace Ekom.Umb.Models;

class Umbraco10Content : UmbracoContent
{
    public Umbraco10Content(IPublishedContent content, string? urlOverride = null)
        : base(
            new Dictionary<string, string>
            {
                ["id"] = content.Id.ToString(),
                ["parentID"] = content.Parent?.Id.ToString() ?? string.Empty,
                ["parentKey"] = content.Parent?.Key.ToString() ?? Guid.Empty.ToString(),
                ["__Key"] = content.Key.ToString(),
                ["nodeName"] = content.Name ?? string.Empty,
                ["__NodeTypeAlias"] = content.ContentType?.Alias ?? string.Empty,
                ["sortOrder"] = content.SortOrder.ToString(),
                ["level"] = content.Level.ToString(),
                ["__Path"] = content.Path ?? string.Empty,
                ["createDate"] = content.CreateDate.ToString("O"),  // ISO 8601 format
                ["updateDate"] = content.UpdateDate.ToString("O"),
                ["__VariesByCulture"] = content.Cultures.Count > 1 ? "y" : "n",
                ["url"] = urlOverride ?? "#"
            },
            GetContentProperties(content)
        )
    { }

    private static Dictionary<string, string> GetContentProperties(IPublishedContent content)
    {
        string? firstCulture = content.Cultures.FirstOrDefault().Value?.Culture;

        return content.Properties
            .Where(x => !string.IsNullOrEmpty(x.Alias))
            .ToDictionary(
                prop => prop.Alias,
                prop =>
                {
                    try
                    {
                        if (prop.PropertyType.EditorAlias == "Umbraco.TinyMCE")
                        {
                            var rtevalue = prop.PropertyType.VariesByCulture()
                                ? content.Value<string>(prop.Alias, firstCulture) ?? string.Empty
                                : content.Value<string>(prop.Alias) ?? string.Empty;

                            return rtevalue;

                        } else
                        {
                            var value = prop.PropertyType.VariesByCulture()
                                ? prop.GetSourceValue(firstCulture)?.ToString() ?? string.Empty
                                : prop.GetSourceValue()?.ToString() ?? string.Empty;
                            
                            return value;
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Failed to GetSourceValue for: {prop.Alias} (Node ID: {content.Id})", ex);
                    }
                }
            );
    }



    public Umbraco10Content(IContent content, Guid parentKey)
        : base(
            new Dictionary<string, string>
            {
                ["id"] = content.Id.ToString(),
                ["parentID"] = content.ParentId.ToString(),
                ["parentKey"] = parentKey.ToString(),
                ["__Key"] = content.Key.ToString(),
                ["nodeName"] = content.Name ?? string.Empty,
                ["__NodeTypeAlias"] = content.ContentType?.Alias ?? string.Empty,
                ["sortOrder"] = content.SortOrder.ToString(),
                ["level"] = content.Level.ToString(),
                ["__Path"] = content.Path ?? string.Empty,
                ["createDate"] = content.CreateDate.ToString("O"),
                ["updateDate"] = content.UpdateDate.ToString("O"),
                ["__VariesByCulture"] = content.AvailableCultures.Any() ? "y" : "n",
                ["url"] = "#"
            },
            content.Properties.ToDictionary(
                x => x.Alias,
                x => TransformPropertyValue(content, x.Alias)
            )
        )
    { }

    private static string TransformPropertyValue(IContent content, string alias)
    {
        var prop = content.Properties.FirstOrDefault(x => x.Alias == alias);

        if (prop != null && prop.PropertyType.PropertyEditorAlias == "Umbraco.TinyMCE")
        {
            var rteValue = content.GetValue<string>(alias) ?? string.Empty;

            if (rteValue.InvariantStartsWith("{"))
            {
                using JsonDocument doc = JsonDocument.Parse(rteValue);

                // Extract the "markup" value
                string markup = doc.RootElement.GetProperty("markup").GetString() ?? "";

                return markup;
            } else
            {
                return rteValue;
            }

        }

        return content.GetValue<string>(alias) ?? string.Empty;

    }
}
