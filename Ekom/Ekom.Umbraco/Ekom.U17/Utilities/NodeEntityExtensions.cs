using Ekom.Models;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Ekom.Utilities;

public static class NodeEntityExtensions
{
    public static T? GetValue<T>(this IProduct node, string propertyAlias, string? alias = null)
        => GetValue<T>((INodeEntity)node, propertyAlias, alias);

    public static T? GetValue<T>(this ICategory node, string propertyAlias, string? alias = null)
        => GetValue<T>((INodeEntity)node, propertyAlias, alias);

    public static T? GetValue<T>(this IPerStoreNodeEntity node, string propertyAlias, string? alias = null)
        => GetValue<T>((INodeEntity)node, propertyAlias, alias);

    public static T? GetValue<T>(this INodeEntity node, string propertyAlias, string? alias = null)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (string.IsNullOrWhiteSpace(propertyAlias))
        {
            throw new ArgumentException("A property alias is required.", nameof(propertyAlias));
        }

        if (IsStringCollectionType<T>())
        {
            return ConvertRawValue<T>(node.GetValue(propertyAlias, alias));
        }

        if (string.IsNullOrWhiteSpace(alias))
        {
            var content = GetPublishedContent(node.Key);
            if (content != null)
            {
                return content.Value<T>(propertyAlias);
            }
        }

        return ConvertRawValue<T>(node.GetValue(propertyAlias, alias));
    }

    private static T? ConvertRawValue<T>(string value)
    {
        if (typeof(T) == typeof(string))
        {
            return (T)(object)value;
        }

        if (typeof(T) == typeof(IPublishedContent))
        {
            return (T?)(object?)GetPublishedContent(value);
        }

        if (typeof(T) == typeof(IEnumerable<IPublishedContent>))
        {
            return (T)(object)GetPublishedContents(value);
        }

        if (typeof(T) == typeof(Link))
        {
            return (T?)(object?)GetLinks(value).FirstOrDefault();
        }

        if (typeof(T) == typeof(IEnumerable<Link>))
        {
            return (T)(object)GetLinks(value);
        }

        if (IsStringCollectionType<T>())
        {
            return (T)(object)GetStringCollection(value, typeof(T));
        }

        return default;
    }

    private static bool IsStringCollectionType<T>()
    {
        var type = typeof(T);
        return type == typeof(IEnumerable<string>)
            || type == typeof(IReadOnlyCollection<string>)
            || type == typeof(IReadOnlyList<string>)
            || type == typeof(List<string>)
            || type == typeof(string[]);
    }

    private static object GetStringCollection(string value, Type targetType)
    {
        List<string> values;
        if (string.IsNullOrWhiteSpace(value))
        {
            values = [];
        }
        else if (value.TrimStart().StartsWith("[", StringComparison.Ordinal))
        {
            try
            {
                values = JsonConvert.DeserializeObject<List<string>>(value) ?? [];
            }
            catch (JsonException)
            {
                values = [];
            }
        }
        else
        {
            values = value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }

        return targetType == typeof(string[]) ? values.ToArray() : values;
    }

    private static IPublishedContent? GetPublishedContent(Guid key)
    {
        var umbracoContextFactory = Configuration.Resolver.GetService<IUmbracoContextFactory>();
        if (umbracoContextFactory == null)
        {
            return null;
        }

        using var contextReference = umbracoContextFactory.EnsureUmbracoContext();
        var context = contextReference.UmbracoContext;

        return context.Content?.GetById(false, key)
            ?? context.Media?.GetById(false, key);
    }

    private static IPublishedContent? GetPublishedContent(string value)
    {
        if (!UdiParser.TryParse(value, out var udi) || udi is not GuidUdi guidUdi)
        {
            return null;
        }

        return GetPublishedContent(guidUdi.Guid);
    }

    private static IEnumerable<IPublishedContent> GetPublishedContents(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<IPublishedContent>();
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(GetPublishedContent)
            .OfType<IPublishedContent>()
            .ToList();
    }

    private static IEnumerable<Link> GetLinks(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<Link>();
        }

        try
        {
            if (value.TrimStart().StartsWith("[", StringComparison.Ordinal))
            {
                return JsonConvert.DeserializeObject<List<Link>>(value) is { } links
                    ? links
                    : Array.Empty<Link>();
            }

            var link = JsonConvert.DeserializeObject<Link>(value);
            return link == null ? Array.Empty<Link>() : [link];
        }
        catch (JsonException)
        {
            return Array.Empty<Link>();
        }
    }
}
