using Ekom.Models;
using Ekom.Umb.Services;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System.Text.Json;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PropertyEditors.ValueConverters;
using Umbraco.Extensions;

namespace Ekom.Utilities;

public static class NodeEntityExtensions
{

    public static T GetValue<T>(this IProduct node, string propAlias, string? alias = null)
    {
        string val = node.GetValue(propAlias, alias);

        return GetValue<T>(val, propAlias, node.Key);
    }
    public static T GetValue<T>(this ICategory node, string propAlias, string? alias = null)
    {
        string val = node.GetValue(propAlias, alias);

        return GetValue<T>(val, propAlias, node.Key);
    }
    public static T GetValue<T>(this INodeEntity node, string propAlias, string? alias = null)
    {
        string val = node.GetValue(propAlias, alias);

        return GetValue<T>(val, propAlias, node.Key);
    }
    public static T GetValue<T>(this IPerStoreNodeEntity node, string propAlias, string? alias = null)
    {
        string val = node.GetValue(propAlias, alias);

        return GetValue<T>(val, propAlias,node.Key);
    }
    private static T? GetValue<T>(string val, string propAlias, Guid nodeId)
    {
        if (typeof(T) == typeof(string))
        {
            return (T)(object)val;
        }
        if (typeof(T) == typeof(int))
        {
            return (T)(object)Convert.ToInt32(val);
        }
        if (typeof(T) == typeof(bool))
        {
            return (T)(object)val.IsBoolean();
        }
        if (typeof(T) == typeof(MediaWithCrops))
        {
            return (T)(object)GetMediaWithCrop(val);
        }
        if (typeof(T) == typeof(IPublishedContent))
        {
            return (T)(object)GetContent(val);
        }
        if (typeof(T) == typeof(IEnumerable<IPublishedContent>))
        {
            return (T)(object)GetContents(val);
        }
        if (typeof(T) == typeof(IProduct))
        {
            return (T)(object)ProductHelper.GetProduct(val);
        }
        if (typeof(T) == typeof(Link))
        {
            return (T?)(object)GetLink(val);
        }
        if (typeof(T) == typeof(IEnumerable<BlockListItem>))
        {
            return (T?)(object)GetBlockListItems(val, propAlias, nodeId);
        }
        if (typeof(T) == typeof(IEnumerable<IProduct>))
        {
            return (T)(object)ProductHelper.GetProducts(val);
        }
        if (IsStringCollectionType<T>())
        {
            return (T)(object)GetStringCollection(val, typeof(T));
        }

        return (T)(object)val;
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
            catch (Newtonsoft.Json.JsonException)
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


    internal static object? GetMediaWithCrop(string value)
    {
        var image = GetContent(value);

        if (image != null)
        {
            var document = JsonDocument.Parse(value);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
                return null;

            var item = root[0]; // Take the first object

            // Build ImageCropperValue
            var imageCropperValue = new ImageCropperValue
            {
                FocalPoint = item.TryGetProperty("focalPoint", out var focalPointElement) && focalPointElement.ValueKind == JsonValueKind.Object
                    ? new ImageCropperValue.ImageCropperFocalPoint
                    {
                        Left = focalPointElement.GetProperty("left").GetDecimal(),
                        Top = focalPointElement.GetProperty("top").GetDecimal()
                    }
                    : null,

                Crops = item.TryGetProperty("crops", out var cropsElement) && cropsElement.ValueKind == JsonValueKind.Array
                    ? cropsElement.EnumerateArray().Select(crop => new ImageCropperValue.ImageCropperCrop
                    {
                        Alias = crop.TryGetProperty("alias", out var aliasElement) ? aliasElement.GetString() : null,
                        Width = crop.TryGetProperty("width", out var widthElement) && widthElement.ValueKind == JsonValueKind.Number
                            ? widthElement.GetInt32()
                            : 0,
                        Height = crop.TryGetProperty("height", out var heightElement) && heightElement.ValueKind == JsonValueKind.Number
                            ? heightElement.GetInt32()
                            : 0,
                        Coordinates = crop.TryGetProperty("coordinates", out var coordElement) && coordElement.ValueKind == JsonValueKind.Object
                            ? new ImageCropperValue.ImageCropperCropCoordinates
                            {
                                X1 = coordElement.GetProperty("x1").GetDecimal(),
                                Y1 = coordElement.GetProperty("y1").GetDecimal(),
                                X2 = coordElement.GetProperty("x2").GetDecimal(),
                                Y2 = coordElement.GetProperty("y2").GetDecimal()
                            }
                            : null
                    }).ToList()
                    : new List<ImageCropperValue.ImageCropperCrop>()
            };

            var mediaWithCrops = new MediaWithCrops(image, null, imageCropperValue);

            return mediaWithCrops;
        }

        return null;
    }

    internal static IPublishedContent? GetContent(string value)
    {

        if (!string.IsNullOrEmpty(value) && value.InvariantStartsWith("umb"))
        {
            using var scope = Configuration.Resolver.GetRequiredService<IServiceScopeFactory>().CreateScope();
            var r = scope.ServiceProvider.GetRequiredService<NodeService>();

            if (value.InvariantContains("document"))
            {
                var node = r.GetNodeById(value);

                if (node != null)
                {
                    return node;
                }
            }
            else if (value.InvariantContains("media"))
            {
                var node = r.GetMediaById(value);

                if (node != null)
                {
                    return node;
                }
            }
        }

        try
        {
            var medias = JsonConvert.DeserializeObject<List<MediaItem>>(value);

            if (medias != null && medias.Any())
            {
                using var scope = Configuration.Resolver.GetRequiredService<IServiceScopeFactory>().CreateScope();
                var r = scope.ServiceProvider.GetRequiredService<NodeService>();

                var media = r.GetMediaById(medias.FirstOrDefault().MediaKey.ToString());

                if (media != null)
                {
                    return media;
                }
            }

        }
        catch
        {

        }

        return null;

    }
    internal static IEnumerable<IPublishedContent> GetContents(string value)
    {
        if (!string.IsNullOrEmpty(value) && value.InvariantStartsWith("umb"))
        {
            using var scope = Configuration.Resolver.GetRequiredService<IServiceScopeFactory>().CreateScope();
            var r = scope.ServiceProvider.GetRequiredService<NodeService>();

            var result = new List<IPublishedContent>();

            foreach (var udi in value.Split(','))
            {
                if (udi.InvariantContains("document"))
                {
                    var node = r.GetNodeById(udi);

                    if (node != null)
                    {
                        result.Add(node);
                    }
                }
                else if (udi.InvariantContains("media"))
                {
                    var node = r.GetMediaById(udi);

                    if (node != null)
                    {
                        result.Add(node);
                    }
                }

            }

            return result;
        }

        try
        {
            var medias = JsonConvert.DeserializeObject<List<MediaItem>>(value);

            if (medias != null && medias.Any())
            {
                using var scope = Configuration.Resolver.GetRequiredService<IServiceScopeFactory>().CreateScope();
                var nodeService = scope.ServiceProvider.GetRequiredService<NodeService>();

                return medias.Select(x => x.MediaKey).Select(x => nodeService.GetMediaById(x.ToString())).ToList();
            }

        }
        catch
        {

        }

        return Enumerable.Empty<IPublishedContent>();

    }
    internal static Link? GetLink(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (value.StartsWith("[", StringComparison.InvariantCultureIgnoreCase))
        {
            return JsonConvert.DeserializeObject<Link[]>(value)?.FirstOrDefault();
        }

        return JsonConvert.DeserializeObject<Link>(value);
    }

    internal static IEnumerable<BlockListItem>? GetBlockListItems(string value, string propAlias, Guid nodeId)
    {
        if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(propAlias))
        {
            return null;
        }

        using var scope = Configuration.Resolver.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var nodeService = scope.ServiceProvider.GetRequiredService<NodeService>();

        var node = nodeService.GetNodeById(nodeId.ToString(), false);

        if (node != null)
        {
            return node.Value<IEnumerable<BlockListItem>>(propAlias);
        }
        return Enumerable.Empty<BlockListItem>();
    }

    internal class MediaItem
    {
        public Guid Key { get; set; }
        public Guid MediaKey { get; set; }
    }
}
