using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Services;
using Ekom.Umb.Models;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Ekom.Umb.Services;

internal sealed class NodeService : INodeService
{
    private readonly IUmbracoContextFactory _context;
    private readonly IPublishedContentQuery _publishedContentQuery;
    private readonly ILogger<NodeService> _logger;

    public NodeService(
        IUmbracoContextFactory context,
        IPublishedContentQuery publishedContentQuery,
        ILogger<NodeService> logger)
    {
        _context = context;
        _publishedContentQuery = publishedContentQuery;
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

        return rootNode.DescendantsOfType(contentTypeAlias)
            .Select(x => new Umbraco17Content(x))
            .ToList();
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

        using var cref = _context.EnsureUmbracoContext();
        var node = GetNodeById(item.Id, true);

        ArgumentNullException.ThrowIfNull(node);

        var ancestors = node.AncestorsOrSelf()
            .Where(x => x.IsDocumentType("ekmCategory") || x.IsDocumentType("ekmProduct"))
            .ToList();
        ancestors.Reverse();

        return ancestors.Select(x => new Umbraco17Content(x)).ToList();
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
