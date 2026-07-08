using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Services;
using Ekom.Umb.Models;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Services.Navigation;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Ekom.Umb.Services;

internal sealed class NodeService : INodeService
{
    private readonly IUmbracoContextFactory _context;
    private readonly IPublishedContentQuery _publishedContentQuery;
    private readonly IDocumentCacheService _documentCacheService;
    private readonly IPublishedContentTypeCache _publishedContentTypeCache;
    private readonly ICoreScopeProvider _scopeProvider;
    private readonly IDocumentNavigationQueryService _documentNavigationQueryService;
    private readonly Umbraco17ContentCache _contentCache;
    private readonly ILogger<NodeService> _logger;

    public NodeService(
        IUmbracoContextFactory context,
        IPublishedContentQuery publishedContentQuery,
        IDocumentCacheService documentCacheService,
        IPublishedContentTypeCache publishedContentTypeCache,
        ICoreScopeProvider scopeProvider,
        IDocumentNavigationQueryService documentNavigationQueryService,
        Umbraco17ContentCache contentCache,
        ILogger<NodeService> logger)
    {
        _context = context;
        _publishedContentQuery = publishedContentQuery;
        _documentCacheService = documentCacheService;
        _publishedContentTypeCache = publishedContentTypeCache;
        _scopeProvider = scopeProvider;
        _documentNavigationQueryService = documentNavigationQueryService;
        _contentCache = contentCache;
        _logger = logger;
    }

    public IEnumerable<UmbracoContent> NodesByTypes(string contentTypeAlias)
    {
        var rootNode = _publishedContentQuery.ContentAtRoot()
            .FirstOrDefault(x => x.IsDocumentType("ekom"));

        if (rootNode == null)
        {
            throw new EkomRootNodeException("Ekom root node not found.");
        }

        using var scope = _scopeProvider.CreateCoreScope(autoComplete: true);

        var contentType = _publishedContentTypeCache.Get(PublishedItemType.Content, contentTypeAlias);
        if (contentType == null)
        {
            _logger.LogWarning("Content type {ContentTypeAlias} not found.", contentTypeAlias);
            return Array.Empty<UmbracoContent>();
        }

        var nodes = _documentCacheService.GetByContentType(contentType).ToList();

        var metadata = BuildContentMetadata(nodes, rootNode);
        var rootPathValue = rootNode.Id.ToString();
        nodes = nodes
            .Where(x => metadata.TryGetValue(x.Key, out var itemMetadata)
                && itemMetadata.Path.Split(',').Contains(rootPathValue))
            .ToList();

        var results = nodes
            .Select(x =>
            {
                metadata.TryGetValue(x.Key, out var itemMetadata);

                return new Umbraco17Content(
                    x,
                    itemMetadata?.ParentId,
                    itemMetadata?.ParentKey,
                    itemMetadata?.Path);
            })
            .ToList();

        foreach (var result in results)
        {
            _contentCache.AddOrUpdate(result);
        }

        return results;
    }

    private Dictionary<Guid, ContentMetadata> BuildContentMetadata(
        IReadOnlyCollection<IPublishedContent> nodes,
        IPublishedContent rootNode)
    {
        if (nodes.Count == 0)
        {
            return new Dictionary<Guid, ContentMetadata>();
        }

        var pathsByKey = new Dictionary<Guid, List<Guid>>();
        var pathCache = new Dictionary<Guid, List<Guid>>();

        foreach (var node in nodes)
        {
            pathsByKey[node.Key] = GetPathKeys(node.Key, pathCache);
        }

        var idsByKey = BuildIdsByKey(nodes, rootNode);

        var metadata = new Dictionary<Guid, ContentMetadata>();

        foreach (var node in nodes)
        {
            var pathKeys = pathsByKey[node.Key];
            var parentKey = pathKeys.Count > 1 ? pathKeys[^2] : (Guid?)null;
            var pathIds = new List<string> { "-1" };

            foreach (var key in pathKeys)
            {
                if (idsByKey.TryGetValue(key, out var id))
                {
                    pathIds.Add(id.ToString());
                }
            }

            metadata[node.Key] = new ContentMetadata(
                parentKey.HasValue && idsByKey.TryGetValue(parentKey.Value, out var parentId) ? parentId : null,
                parentKey,
                string.Join(',', pathIds));
        }

        return metadata;
    }

    private Dictionary<Guid, int> BuildIdsByKey(IReadOnlyCollection<IPublishedContent> nodes, IPublishedContent rootNode)
    {
        var idsByKey = new Dictionary<Guid, int>
        {
            [rootNode.Key] = rootNode.Id,
        };

        foreach (var item in _contentCache.Values)
        {
            idsByKey[item.Key] = item.Id;
        }

        foreach (var node in nodes)
        {
            idsByKey[node.Key] = node.Id;
        }

        return idsByKey;
    }

    private List<Guid> GetPathKeys(Guid key, Dictionary<Guid, List<Guid>> pathCache)
    {
        if (pathCache.TryGetValue(key, out var cachedPath))
        {
            return cachedPath;
        }

        var descendantKeys = new List<Guid>();
        List<Guid>? cachedPrefix = null;
        var currentKey = key;

        while (true)
        {
            if (pathCache.TryGetValue(currentKey, out var currentPath))
            {
                cachedPrefix = currentPath;
                break;
            }

            descendantKeys.Add(currentKey);

            if (!_documentNavigationQueryService.TryGetParentKey(currentKey, out var parentKey) || !parentKey.HasValue)
            {
                break;
            }

            currentKey = parentKey.Value;
        }

        descendantKeys.Reverse();
        var path = cachedPrefix == null
            ? descendantKeys
            : cachedPrefix.Concat(descendantKeys).ToList();

        foreach (var cacheKey in descendantKeys)
        {
            var index = path.IndexOf(cacheKey);
            if (index >= 0)
            {
                pathCache[cacheKey] = path.GetRange(0, index + 1);
            }
        }

        return path;
    }

    public IEnumerable<UmbracoContent> NodesByTypesFaster(string contentTypeAlias)
    {
        return NodesByTypes(contentTypeAlias);
    }

    public IEnumerable<UmbracoContent> NodeAncestors(string t)
    {
        using var cref = _context.EnsureUmbracoContext();
        var node = GetNodeById(t);

        if (node == null)
        {
            return Array.Empty<UmbracoContent>();
        }

        return node.Ancestors()
            .Select(x => new Umbraco17Content(x))
            .ToList();
    }

    public IEnumerable<UmbracoContent> NodeCatalogAncestors(string t)
    {
        using var cref = _context.EnsureUmbracoContext();
        var node = GetNodeById(t);

        ArgumentNullException.ThrowIfNull(node);

        var ancestors = node.AncestorsOrSelf()
            .Where(x => x.IsDocumentType("ekmCategory") || x.IsDocumentType("ekmProduct"))
            .ToList();
        ancestors.Reverse();

        return ancestors.Select(x => new Umbraco17Content(x)).ToList();
    }

    public IEnumerable<UmbracoContent> NodeChildren(string t)
    {
        using var cref = _context.EnsureUmbracoContext();
        var node = GetNodeById(t);

        ArgumentNullException.ThrowIfNull(node);

        return node.Children
            .Select(x => new Umbraco17Content(x))
            .ToList();
    }

    public bool IsItemUnpublished(UmbracoContent content)
    {
        foreach (var item in GetAllCatalogAncestors(content))
        {
            if (item == null)
            {
                return true;
            }
        }

        return false;
    }

    public IEnumerable<UmbracoContent> GetAllCatalogAncestors(UmbracoContent item)
    {
        if (item == null)
        {
            return Array.Empty<UmbracoContent>();
        }

        var cachedAncestors = GetCachedCatalogAncestors(item).ToList();
        if (cachedAncestors.Count > 0)
        {
            return cachedAncestors;
        }

        using var cref = _context.EnsureUmbracoContext();
        var node = GetNodeById(item.Id, true);

        ArgumentNullException.ThrowIfNull(node);

        var ancestors = node.AncestorsOrSelf()
            .Where(x => x.IsDocumentType("ekmCategory") || x.IsDocumentType("ekmProduct"))
            .ToList();
        ancestors.Reverse();

        return ancestors.Select(x => new Umbraco17Content(x)).ToList();
    }

    private IEnumerable<UmbracoContent> GetCachedCatalogAncestors(UmbracoContent item)
    {
        if (string.IsNullOrWhiteSpace(item.Path))
        {
            yield break;
        }

        foreach (var value in item.Path.Split(','))
        {
            if (!int.TryParse(value, out var id))
            {
                continue;
            }

            if (!_contentCache.TryGetById(id, out var content) || content == null)
            {
                continue;
            }

            if (content.IsDocumentType("ekmCategory") || content.IsDocumentType("ekmProduct"))
            {
                yield return content;
            }
        }
    }

    public IPublishedContent? GetNodeById(int id, bool preview = false)
    {
        using var cref = _context.EnsureUmbracoContext();
        return cref.UmbracoContext.Content?.GetById(preview, id);
    }

    public IPublishedContent? GetNodeById(Guid id, bool preview = false)
    {
        using var cref = _context.EnsureUmbracoContext();
        return cref.UmbracoContext.Content?.GetById(preview, id);
    }

    public IPublishedContent? GetNodeById(Udi id, bool preview = false)
    {
        using var cref = _context.EnsureUmbracoContext();
        return TryGetGuid(id, out var guid)
            ? cref.UmbracoContext.Content?.GetById(preview, guid)
            : null;
    }

    public IPublishedContent? GetNodeById(string id, bool preview = false)
    {
        using var cref = _context.EnsureUmbracoContext();
        var cache = cref.UmbracoContext.Content;

        if (int.TryParse(id, out var intId))
        {
            return cache?.GetById(preview, intId);
        }

        if (Guid.TryParse(id, out var guidId))
        {
            return cache?.GetById(preview, guidId);
        }

        if (UdiParser.TryParse(id, out var udiId) && TryGetGuid(udiId, out var udiGuid))
        {
            return cache?.GetById(preview, udiGuid);
        }

        return null;
    }

    public UmbracoContent? NodeById(int t, bool preview = false)
    {
        var node = GetNodeById(t, preview);

        return node == null ? null : new Umbraco17Content(node);
    }

    public UmbracoContent? NodeById(Guid t, bool preview = false)
    {
        var node = GetNodeById(t, preview);

        return node == null ? null : new Umbraco17Content(node);
    }

    public UmbracoContent? NodeById(Udi t, bool preview = false)
    {
        var node = GetNodeById(t, preview);

        return node == null ? null : new Umbraco17Content(node);
    }

    public UmbracoContent? NodeById(string t, bool preview = false)
    {
        if (int.TryParse(t, out var intId))
        {
            return NodeById(intId, preview);
        }

        if (Guid.TryParse(t, out var guidId))
        {
            return NodeById(guidId, preview);
        }

        if (UdiParser.TryParse(t, out var udiId) && TryGetGuid(udiId, out var udiGuid))
        {
            return NodeById(udiGuid, preview);
        }

        return null;
    }

    public UmbracoContent? MediaById(int t)
    {
        using var cref = _context.EnsureUmbracoContext();
        var node = cref.UmbracoContext.Media?.GetById(false, t);

        return node == null || !node.IsPublished() ? null : new Umbraco17Media(node);
    }

    public UmbracoContent? MediaById(Guid t)
    {
        using var cref = _context.EnsureUmbracoContext();
        var node = cref.UmbracoContext.Media?.GetById(false, t);

        return node == null ? null : new Umbraco17Media(node);
    }

    public UmbracoContent? MediaById(Udi t)
    {
        using var cref = _context.EnsureUmbracoContext();
        var node = TryGetGuid(t, out var guid)
            ? cref.UmbracoContext.Media?.GetById(false, guid)
            : null;

        return node == null ? null : new Umbraco17Media(node);
    }

    public UmbracoContent? MediaById(string t)
    {
        if (int.TryParse(t, out var intId))
        {
            return MediaById(intId);
        }

        if (Guid.TryParse(t, out var guidId))
        {
            return MediaById(guidId);
        }

        if (UdiParser.TryParse(t, out var udiId) && TryGetGuid(udiId, out var udiGuid))
        {
            return MediaById(udiGuid);
        }

        return null;
    }

    public IPublishedContent? GetMediaById(string id)
    {
        using var cref = _context.EnsureUmbracoContext();
        var cache = cref.UmbracoContext.Media;

        if (int.TryParse(id, out var intId))
        {
            return cache?.GetById(false, intId);
        }

        if (Guid.TryParse(id, out var guidId))
        {
            return cache?.GetById(false, guidId);
        }

        if (UdiParser.TryParse(id, out var udiId) && TryGetGuid(udiId, out var udiGuid))
        {
            return cache?.GetById(false, udiGuid);
        }

        return null;
    }

    public string GetUrl(string t, string url = null!)
    {
        using var cref = _context.EnsureUmbracoContext();
        var node = GetNodeById(t);

        if (node == null)
        {
            return "#";
        }

        try
        {
            return node.Url(url);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogDebug(ex, "Failed to resolve Umbraco 17 URL for node {NodeId}", t);
            return "#";
        }
    }

    private static bool TryGetGuid(Udi udi, out Guid guid)
    {
        if (udi is GuidUdi guidUdi)
        {
            guid = guidUdi.Guid;
            return true;
        }

        guid = Guid.Empty;
        return false;
    }

    private sealed record ContentMetadata(int? ParentId, Guid? ParentKey, string Path);
}
