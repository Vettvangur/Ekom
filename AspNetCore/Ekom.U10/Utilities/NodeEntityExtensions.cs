using Ekom.Models;
using Ekom.Umb.Services;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;

namespace Ekom.Utilities
{
    public static class NodeEntityExtensions
    {

        public static T GetValue<T>(this IProduct node, string propAlias, string alias = null)
        {
            string val = node.GetValue(propAlias, alias);

            return GetValue<T>(val);
        }
        public static T GetValue<T>(this ICategory node, string propAlias, string alias = null)
        {
            string val = node.GetValue(propAlias, alias);

            return GetValue<T>(val);
        }
        public static T GetValue<T>(this INodeEntity node, string propAlias, string alias = null)
        {
            string val = node.GetValue(propAlias, alias);

            return GetValue<T>(val);
        }
        public static T GetValue<T>(this IPerStoreNodeEntity node, string propAlias, string alias = null)
        {
            string val = node.GetValue(propAlias, alias);

            return GetValue<T>(val);
        }
        private static T? GetValue<T>(string val)
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
                return (T)(object)GetContent(val);
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
            if (typeof(T) == typeof(IEnumerable<IProduct>))
            {
                return (T)(object)ProductHelper.GetProducts(val);
            }
            return (T)(object)val;
        }
        internal static IPublishedContent? GetContent(string value)
        {

            if (!string.IsNullOrEmpty(value) && value.InvariantStartsWith("umb"))
            {
                var r = Configuration.Resolver.GetService<NodeService>();

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
                    var r = Configuration.Resolver.GetService<NodeService>();

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
                var r = Configuration.Resolver.GetService<NodeService>();

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
                    return medias.Select(x => x.MediaKey).Select(x => Configuration.Resolver.GetService<NodeService>()?.GetMediaById(x.ToString()));
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

            return JsonConvert.DeserializeObject<Link>(value);
        }

        internal class MediaItem
        {
            public Guid Key { get; set; }
            public Guid MediaKey { get; set; }
        }
    }
}
