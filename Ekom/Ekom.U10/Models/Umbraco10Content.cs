using Ekom.Models;
using Ekom.Umb.Services;
using System.Text.Json;
using System.Text.Json.Nodes;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;

namespace Ekom.Umb.Models;

class Umbraco10Content : UmbracoContent
{
    public Umbraco10Content(IPublishedContent content, string? urlOverride = null, IEkomRichTextResolver? richTextResolver = null)
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
            GetContentProperties(content, richTextResolver)
        )
    { }

    private static Dictionary<string, string> GetContentProperties(IPublishedContent content, IEkomRichTextResolver? richTextResolver)
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
                            
                            return ResolveLocalLinks(value, richTextResolver);
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Failed to GetSourceValue for: {prop.Alias} (Node ID: {content.Id})", ex);
                    }
                }
            );
    }



    public Umbraco10Content(IContent content, Guid parentKey, IEkomRichTextResolver? richTextResolver = null)
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
                x => TransformPropertyValue(content, x.Alias, richTextResolver)
            )
        )
    { }

    private static string TransformPropertyValue(IContent content, string alias, IEkomRichTextResolver? richTextResolver)
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

                return ResolveLocalLinks(markup, richTextResolver);
            } else
            {
                return ResolveLocalLinks(rteValue, richTextResolver);
            }

        }

        return ResolveLocalLinks(content.GetValue<string>(alias) ?? string.Empty, richTextResolver);

    }

    private static string ResolveLocalLinks(string value, IEkomRichTextResolver? richTextResolver)
    {
        if (richTextResolver == null || string.IsNullOrEmpty(value) || !value.Contains("{localLink:", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        var trimmed = value.TrimStart();

        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
        {
            return ResolveJsonLocalLinks(value, richTextResolver);
        }

        return richTextResolver.ResolveLocalLinks(value);
    }

    private static string ResolveJsonLocalLinks(string value, IEkomRichTextResolver richTextResolver)
    {
        try
        {
            var node = JsonNode.Parse(value);

            if (node == null)
            {
                return value;
            }

            ResolveJsonNodeLocalLinks(node, richTextResolver);

            return node.ToJsonString();
        }
        catch (JsonException)
        {
            return value;
        }
    }

    private static void ResolveJsonNodeLocalLinks(JsonNode node, IEkomRichTextResolver richTextResolver)
    {
        if (node is JsonObject jsonObject)
        {
            foreach (var property in jsonObject.ToList())
            {
                if (property.Value is JsonValue jsonValue
                    && jsonValue.TryGetValue<string>(out var stringValue)
                    && stringValue.Contains("{localLink:", StringComparison.OrdinalIgnoreCase))
                {
                    jsonObject[property.Key] = richTextResolver.ResolveLocalLinks(stringValue);
                    continue;
                }

                if (property.Value != null)
                {
                    ResolveJsonNodeLocalLinks(property.Value, richTextResolver);
                }
            }
        }
        else if (node is JsonArray jsonArray)
        {
            for (var i = 0; i < jsonArray.Count; i++)
            {
                var item = jsonArray[i];

                if (item is JsonValue jsonValue
                    && jsonValue.TryGetValue<string>(out var stringValue)
                    && stringValue.Contains("{localLink:", StringComparison.OrdinalIgnoreCase))
                {
                    jsonArray[i] = richTextResolver.ResolveLocalLinks(stringValue);
                    continue;
                }

                if (item != null)
                {
                    ResolveJsonNodeLocalLinks(item, richTextResolver);
                }
            }
        }
    }
}
