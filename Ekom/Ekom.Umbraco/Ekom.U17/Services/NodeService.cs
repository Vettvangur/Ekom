using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Services;
using Ekom.Umb.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Ekom.Umb.Services;

internal sealed class NodeService : INodeService
{
    private readonly IUmbracoContextFactory _context;
    private readonly IPublishedContentQuery _publishedContentQuery;
    private readonly EkomCacheBuildContext _cacheBuildContext;
    private readonly Umbraco17ContentCache _contentCache;
    private readonly ILogger<NodeService> _logger;

    public NodeService(
        IUmbracoContextFactory context,
        IPublishedContentQuery publishedContentQuery,
        EkomCacheBuildContext cacheBuildContext,
        Umbraco17ContentCache contentCache,
        ILogger<NodeService> logger)
    {
        _context = context;
        _publishedContentQuery = publishedContentQuery;
        _cacheBuildContext = cacheBuildContext;
        _contentCache = contentCache;
        _logger = logger;
    }

    public IEnumerable<UmbracoContent> NodesByTypes(string contentTypeAlias)
    {
        var stopwatch = Stopwatch.StartNew();
        using var contextReference = _context.EnsureUmbracoContext();

        if (_cacheBuildContext.TryGetNodes(contentTypeAlias, out var cacheBuildNodes))
        {
            return NodesByTypesForCacheBuild(cacheBuildNodes, contentTypeAlias, stopwatch);
        }

        var rootNode = _publishedContentQuery.ContentAtRoot()
            .FirstOrDefault(x => x.IsDocumentType("ekom"));

        if (rootNode == null)
        {
            throw new EkomRootNodeException("Ekom root node not found.");
        }

        var keysById = _contentCache.Values
            .ToDictionary(x => x.Id, x => x.Key);
        keysById[rootNode.Id] = rootNode.Key;

        var nodes = rootNode.DescendantsOfType(contentTypeAlias).ToList();

        foreach (var node in nodes)
        {
            keysById[node.Id] = node.Key;
        }

        var results = nodes
            .Select(node =>
            {
                var parentId = GetParentId(node.Path);
                var parentKey = parentId.HasValue && keysById.TryGetValue(parentId.Value, out var parentKeyValue)
                    ? parentKeyValue
                    : (Guid?)null;

                return new Umbraco17Content(node, parentId, parentKey, node.Path);
            })
            .ToList();

        foreach (var result in results)
        {
            _contentCache.AddOrUpdate(result);
        }

        stopwatch.Stop();
        _logger.LogDebug(
            "Retrieved and mapped {Count} published {ContentTypeAlias} nodes in {Elapsed}.",
            results.Count,
            contentTypeAlias,
            stopwatch.Elapsed);

        return results;
    }

    private IEnumerable<UmbracoContent> NodesByTypesForCacheBuild(
        IReadOnlyList<IPublishedContent> nodes,
        string contentTypeAlias,
        Stopwatch stopwatch)
    {
        var results = nodes
            .Select(node =>
            {
                if (!_cacheBuildContext.TryGetNodeInfo(node, out var parentId, out var parentKey, out var path))
                {
                    throw new InvalidOperationException($"Cache build node {node.Key} was not found.");
                }

                return new Umbraco17Content(node, parentId, parentKey, path);
            })
            .ToList();

        foreach (var result in results)
        {
            _contentCache.AddOrUpdate(result);
        }

        stopwatch.Stop();
        _logger.LogDebug(
            "Retrieved and mapped {Count} cache-build {ContentTypeAlias} nodes in {Elapsed}.",
            results.Count,
            contentTypeAlias,
            stopwatch.Elapsed);

        return results;
    }

    private static int? GetParentId(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var lastSeparator = path.LastIndexOf(',');
        if (lastSeparator <= 0)
        {
            return null;
        }

        var parentSeparator = path.LastIndexOf(',', lastSeparator - 1);
        var parentIdValue = path[(parentSeparator + 1)..lastSeparator];
        return int.TryParse(parentIdValue, out var parentId) ? parentId : null;
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

}
