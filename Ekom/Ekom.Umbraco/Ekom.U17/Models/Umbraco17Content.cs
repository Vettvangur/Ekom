using Ekom.Models;
using System.Globalization;
using System.Text.Json;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;

namespace Ekom.Umb.Models;

internal class Umbraco17Content : UmbracoContent
{
    public Umbraco17Content(
        IPublishedContent content,
        int? parentId = null,
        Guid? parentKey = null,
        string? path = null,
        string? urlOverride = null)
        : base(
            GetDefaultProperties(content, parentId, parentKey, path, urlOverride),
            GetContentProperties(content))
    {
    }

    public Umbraco17Content(IContent content, Guid parentKey)
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
                ["url"] = "#",
            },
            content.Properties.ToDictionary(
                x => x.Alias,
                x => TransformPropertyValue(content, x.Alias)))
    {
    }

    private static Dictionary<string, string> GetDefaultProperties(
        IPublishedContent content,
        int? parentId,
        Guid? parentKey,
        string? path,
        string? urlOverride)
    {
        var id = content.Id.ToString();
        var parent = parentId.HasValue || parentKey.HasValue ? null : GetParent(content);
        var resolvedParentId = parentId ?? parent?.Id;
        var resolvedParentKey = parentKey ?? parent?.Key;
        var resolvedPath = path ?? content.Path ?? string.Empty;
        var key = content.Key.ToString();
        var name = content.Name ?? string.Empty;
        var contentTypeAlias = content.ContentType?.Alias ?? string.Empty;
        var sortOrder = content.SortOrder.ToString();
        var level = content.Level.ToString();
        var createDate = content.CreateDate.ToString("O");
        var updateDate = content.UpdateDate.ToString("O");
        var variesByCulture = content.Cultures.Count > 1 ? "y" : "n";

        return new Dictionary<string, string>
        {
            ["id"] = id,
            ["parentID"] = resolvedParentId?.ToString() ?? string.Empty,
            ["parentKey"] = resolvedParentKey?.ToString() ?? Guid.Empty.ToString(),
            ["__Key"] = key,
            ["nodeName"] = name,
            ["__NodeTypeAlias"] = contentTypeAlias,
            ["sortOrder"] = sortOrder,
            ["level"] = level,
            ["__Path"] = resolvedPath,
            ["createDate"] = createDate,
            ["updateDate"] = updateDate,
            ["__VariesByCulture"] = variesByCulture,
            ["url"] = urlOverride ?? "#",
        };
    }

    private static IPublishedContent? GetParent(IPublishedContent content)
    {
#if UMBRACO_18
        return content.Parent();
#else
        return content.Parent;
#endif
    }

    private static Dictionary<string, string> GetContentProperties(IPublishedContent content)
    {
        var firstCulture = content.Cultures.FirstOrDefault().Value?.Culture;

        return content.Properties
            .Where(x => !string.IsNullOrEmpty(x.Alias))
            .ToDictionary(
                prop => prop.Alias,
                prop => GetContentPropertyValue(content, prop, firstCulture));
    }

    private static string GetContentPropertyValue(
        IPublishedContent content,
        IPublishedProperty prop,
        string? firstCulture)
    {
        try
        {
            if (prop.PropertyType.EditorAlias == "Umbraco.RichText")
            {
                return prop.PropertyType.VariesByCulture()
                    ? content.Value<string>(prop.Alias, firstCulture) ?? string.Empty
                    : content.Value<string>(prop.Alias) ?? string.Empty;
            }

            var culture = prop.PropertyType.VariesByCulture() ? firstCulture : null;
            var sourceValue = prop.GetSourceValue(culture);
            var value = sourceValue;
            var isDate = IsDateEditor(prop.PropertyType.EditorAlias);
            if (prop.PropertyType.EditorAlias == "Umbraco.DateTimeWithTimeZone" && sourceValue is string storedDate
                && !TryFormatStoredDate(storedDate, prop.PropertyType.EditorAlias, out _))
                return storedDate;

            if (isDate && sourceValue is not DateTime and not DateTimeOffset
                && !string.IsNullOrWhiteSpace(sourceValue?.ToString()) && prop.HasValue(culture))
            {
                var converted = prop.GetValue(culture);
                // The legacy converter uses MinValue for invalid or missing dates.
                if (converted is DateTime date && (prop.PropertyType.EditorAlias != "Umbraco.DateTime" || date != DateTime.MinValue)
                    || converted is DateTimeOffset)
                    value = converted;
            }

            return isDate ? FormatPropertyValue(value) : value?.ToString() ?? string.Empty;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to GetSourceValue for: {prop.Alias} (Node ID: {content.Id})", ex);
        }
    }

    private static string TransformPropertyValue(IContent content, string alias)
    {
        var prop = content.Properties.FirstOrDefault(x => x.Alias == alias);

        if (prop != null && prop.PropertyType.PropertyEditorAlias == "Umbraco.RichText")
        {
            var rteValue = content.GetValue<string>(alias) ?? string.Empty;

            if (rteValue.InvariantStartsWith("{"))
            {
                using var doc = JsonDocument.Parse(rteValue);

                return doc.RootElement.GetProperty("markup").GetString() ?? string.Empty;
            }

            return rteValue;
        }

        var editorAlias = prop?.PropertyType.PropertyEditorAlias;
        if (IsDateEditor(editorAlias))
        {
            var value = content.GetValue(alias);
            if (value is DateTime or DateTimeOffset)
                return FormatPropertyValue(value);

            // Modern Date/Time editors store a date/offset in JSON, not a database DateTime.
            if (editorAlias != "Umbraco.DateTime" && value is string text
                && TryFormatStoredDate(text, editorAlias, out var formatted))
                return formatted;
        }

        return content.GetValue<string>(alias) ?? string.Empty;
    }

    private static bool IsDateEditor(string? editorAlias) => editorAlias is
        "Umbraco.DateTime" or "Umbraco.DateTimeUnspecified" or "Umbraco.DateTimeWithTimeZone";

    private static bool TryFormatStoredDate(string value, string? editorAlias, out string formatted)
    {
        formatted = string.Empty;
        try
        {
            using var document = JsonDocument.Parse(value);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("date", out var date)
                || date.ValueKind != JsonValueKind.String)
                return false;

            var dateText = date.GetString() ?? string.Empty;
            if (editorAlias == "Umbraco.DateTimeWithTimeZone"
                && !dateText.EndsWith('Z')
                && !(dateText.Length >= 6 && dateText[^3] == ':' && dateText[^6] is '+' or '-'))
                return false;

            if (!date.TryGetDateTimeOffset(out var dateTimeOffset))
                return false;

            formatted = editorAlias == "Umbraco.DateTimeUnspecified"
                ? dateTimeOffset.DateTime.ToString("O", CultureInfo.InvariantCulture)
                : dateTimeOffset.ToString("O", CultureInfo.InvariantCulture);
            return true;
        }
        catch (JsonException)
        {
            // Preserve malformed source values rather than guessing a date or timezone.
            return false;
        }
    }

    private static string FormatPropertyValue(object? value) => value switch
    {
        DateTime date => date.ToString("O", CultureInfo.InvariantCulture),
        DateTimeOffset date => date.ToString("O", CultureInfo.InvariantCulture),
        _ => value?.ToString() ?? string.Empty,
    };
}
