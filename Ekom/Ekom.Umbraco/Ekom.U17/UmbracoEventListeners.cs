using Ekom.API;
using Ekom.Cache;
using Ekom.Models;
using Ekom.Repositories;
using Ekom.Services;
using Ekom.Umb.Models;
using Ekom.Utilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Ekom.Umb;

internal sealed class UmbracoEventListeners :
    INotificationAsyncHandler<ContentPublishedNotification>,
    INotificationAsyncHandler<ContentUnpublishedNotification>,
    INotificationAsyncHandler<ContentSavingNotification>,
    INotificationAsyncHandler<ContentDeletedNotification>,
    INotificationHandler<ContentMovedToRecycleBinNotification>,
    INotificationHandler<ContentMovedNotification>,
    INotificationHandler<DomainSavedNotification>,
    INotificationHandler<DomainDeletedNotification>,
    INotificationHandler<ServerVariablesParsingNotification>,
    INotificationHandler<LanguageSavedNotification>,
    INotificationHandler<LanguageDeletedNotification>
{
    private readonly ILogger<UmbracoEventListeners> _logger;
    private readonly Configuration _config;
    private readonly IBaseCache<IStore> _storeCache;
    private readonly IStoreDomainCache _storeDomainCache;
    private readonly IContentService _contentService;
    private readonly IUmbracoContextFactory _context;
    private readonly IAppPolicyCache _runtimeCache;
    private readonly IMemoryCache _cache;
    private readonly CouponRepository _couponRepository;
    private readonly Ekom.Services.INodeService _nodeService;
    private readonly RevalidateService _revalidateService;

    public UmbracoEventListeners(
        ILogger<UmbracoEventListeners> logger,
        Configuration config,
        IBaseCache<IStore> storeCache,
        IStoreDomainCache storeDomainCache,
        IContentService contentService,
        IUmbracoContextFactory context,
        IMemoryCache cache,
        AppCaches appCaches,
        CouponRepository couponRepository,
        Ekom.Services.INodeService nodeService,
        RevalidateService revalidateService)
    {
        _logger = logger;
        _config = config;
        _storeCache = storeCache;
        _storeDomainCache = storeDomainCache;
        _contentService = contentService;
        _context = context;
        _runtimeCache = appCaches.RuntimeCache;
        _cache = cache;
        _couponRepository = couponRepository;
        _nodeService = nodeService;
        _revalidateService = revalidateService;
    }

    public async Task HandleAsync(ContentSavingNotification notification, CancellationToken cancellationToken)
    {
        foreach (var content in notification.SavedEntities)
        {
            if (!content.ContentType.Alias.StartsWith("ekm", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            ClearMemoryCache(content);

            if (content.ContentType.Alias is not ("ekmProduct" or "ekmProductVariant"))
            {
                continue;
            }

            await UpdateStockAsync(content, cancellationToken).ConfigureAwait(false);
        }
    }

    public Task HandleAsync(ContentPublishedNotification notification, CancellationToken cancellationToken)
    {
        foreach (var node in notification.PublishedEntities)
        {
            if (!node.ContentType.Alias.StartsWith("ekm", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            ClearMemoryCache(node);

            var cacheEntry = FindMatchingCache(node.ContentType.Alias);
            var parentNode = _nodeService.NodeById(node.ParentId);

            if (parentNode == null)
            {
                continue;
            }

            cacheEntry?.AddReplace(new Umbraco17Content(node, parentNode.Key));
            _ = RevalidateAsync(node, cancellationToken);

            if (node.ContentType.Alias != "ekmCategory")
            {
                continue;
            }

            var dirty = (IRememberBeingDirty)node;
            if (dirty.WasPropertyDirty("slug") || dirty.WasPropertyDirty("disable"))
            {
                RefreshCacheForRelatedNodes(node.Id);
            }
        }

        return Task.CompletedTask;
    }

    public Task HandleAsync(ContentUnpublishedNotification notification, CancellationToken cancellationToken)
    {
        foreach (var node in notification.UnpublishedEntities)
        {
            RemoveDescendantsFromCaches(node.Id);

            if (!node.ContentType.Alias.StartsWith("ekm", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            ClearMemoryCache(node);

            var cacheEntry = FindMatchingCache(node.ContentType.Alias);
            cacheEntry?.Remove(node.Key);

            _ = RevalidateAsync(node, cancellationToken);
        }

        return Task.CompletedTask;
    }

    public async Task HandleAsync(ContentDeletedNotification notification, CancellationToken cancellationToken)
    {
        foreach (var node in notification.DeletedEntities)
        {
            if (!node.ContentType.Alias.StartsWith("ekm", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            ClearMemoryCache(node);

            var cacheEntry = FindMatchingCache(node.ContentType.Alias);
            cacheEntry?.Remove(node.Key);

            if (node.ContentType.Alias == "ekmOrderDiscount")
            {
                await _couponRepository.DeleteCouponsByDiscountAsync(node.Key, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public void Handle(ContentMovedNotification notification)
    {
        foreach (var info in notification.MoveInfoCollection)
        {
            var node = info.Entity;

            if (!node.ContentType.Alias.StartsWith("ekm", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var cacheEntry = FindMatchingCache(node.ContentType.Alias);
            cacheEntry?.Remove(node.Key);

            var parentNode = _nodeService.NodeById(node.ParentId);
            cacheEntry?.AddReplace(new Umbraco17Content(node, parentNode?.Key ?? Guid.Empty));

            if (node.ContentType.Alias == "ekmCategory")
            {
                RefreshCacheForRelatedNodes(node.Id);
            }
        }
    }

    public void Handle(ContentMovedToRecycleBinNotification notification)
    {
        foreach (var node in notification.MoveInfoCollection)
        {
            if (!node.Entity.ContentType.Alias.StartsWith("ekm", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var cacheEntry = FindMatchingCache(node.Entity.ContentType.Alias);
            cacheEntry?.Remove(node.Entity.Key);
        }
    }

    public void Handle(DomainSavedNotification notification)
    {
        foreach (var domain in notification.SavedEntities)
        {
            _storeDomainCache.AddReplace(new Umbraco17Domain(domain));
            RefreshStoreForDomainRoot(domain.RootContentId);
        }
    }

    public void Handle(DomainDeletedNotification notification)
    {
        foreach (var domain in notification.DeletedEntities)
        {
            _storeDomainCache.Remove(domain.Key);
            RefreshStoreForDomainRoot(domain.RootContentId);
        }
    }

    public void Handle(ServerVariablesParsingNotification notification)
    {
        notification.ServerVariables.Add("ekom", new
        {
            backofficeApiEndpoint = "/ekom/backoffice/",
            apiEndpoint = "/ekom/api/",
            managerEndpoint = "/ekom/manager/",
            charCollections = _config.CharCollections,
        });
    }

    public void Handle(LanguageSavedNotification notification) => ClearLanguageCache();

    public void Handle(LanguageDeletedNotification notification) => ClearLanguageCache();

    private ICache? FindMatchingCache(string contentTypeAlias)
    {
        if (contentTypeAlias.Contains("ekmpaymentprovider", StringComparison.OrdinalIgnoreCase))
        {
            return _config.CacheList.Value.FirstOrDefault(x =>
                !string.IsNullOrEmpty(x.NodeAlias)
                && contentTypeAlias.StartsWith(x.NodeAlias, StringComparison.OrdinalIgnoreCase));
        }

        return _config.CacheList.Value.FirstOrDefault(x =>
            !string.IsNullOrEmpty(x.NodeAlias)
            && contentTypeAlias.Equals(x.NodeAlias, StringComparison.OrdinalIgnoreCase));
    }

    private void RefreshStoreForDomainRoot(int? rootContentId)
    {
        if (rootContentId == null)
        {
            return;
        }

        var rootContent = _contentService.GetById(rootContentId.Value);
        if (rootContent == null)
        {
            return;
        }

        var store = _storeCache.Cache.Values.FirstOrDefault(x => x.StoreRootNodeId == rootContent.Id);
        if (store == null)
        {
            return;
        }

        var storeContent = _contentService.GetById(store.Id);
        if (storeContent != null)
        {
            _storeCache.AddReplace(new Umbraco17Content(storeContent, Guid.Empty));
        }
    }

    private async Task UpdateStockAsync(IContent content, CancellationToken cancellationToken)
    {
        var stockValue = content.GetValue<string>("stock");

        if (string.IsNullOrEmpty(stockValue))
        {
            return;
        }

        try
        {
            var stockArray = JsonConvert.DeserializeObject<IEnumerable<StockRequest>>(stockValue)?
                .Select(x => new StockRequest
                {
                    StoreAlias = x.StoreAlias,
                    Value = x.Value ?? 0,
                })
                .ToList();

            if (stockArray == null)
            {
                return;
            }

            foreach (var stockItem in stockArray)
            {
                if (!string.IsNullOrEmpty(stockItem.StoreAlias))
                {
                    await Stock.Instance.SetStockAsync(content.Key, stockItem.StoreAlias, stockItem.Value ?? 0, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await Stock.Instance.SetStockAsync(content.Key, stockItem.Value ?? 0, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Could not map stock value to stock request on node {NodeId}. Value: {StockValue}", content.Id, stockValue);
        }
    }

    private void RefreshCacheForRelatedNodes(int id, bool remove = false)
    {
        using var cref = _context.EnsureUmbracoContext();
        var currentNode = cref.UmbracoContext.Content?.GetById(id);

        if (currentNode == null)
        {
            return;
        }

        var descendants = currentNode.Descendants();
        var ancestors = currentNode.Ancestors().Where(x => x.ContentType.Alias.StartsWith("ekm", StringComparison.OrdinalIgnoreCase));

        foreach (var descendant in descendants)
        {
            var cacheEntry = FindMatchingCache(descendant.ContentType.Alias);

            if (remove)
            {
                cacheEntry?.Remove(descendant.Key);
            }
            else
            {
                cacheEntry?.AddReplace(new Umbraco17Content(descendant));
            }
        }

        foreach (var ancestor in ancestors.Where(x => x.ContentType.Alias is "ekmCategory" or "ekmProduct"))
        {
            var cacheEntry = FindMatchingCache(ancestor.ContentType.Alias);
            cacheEntry?.AddReplace(new Umbraco17Content(ancestor));
        }
    }

    private void RemoveDescendantsFromCaches(int parentId)
    {
        const int pageSize = 2_000;
        long pageIndex = 0;
        long descendantCount = 0;

        while (true)
        {
            var batch = _contentService.GetPagedDescendants(parentId, pageIndex, pageSize, out var totalRecords);
            var descendants = batch as IList<IContent> ?? batch.ToList();

            if (descendants.Count == 0)
            {
                return;
            }

            foreach (var descendant in descendants)
            {
                if (!descendant.ContentType.Alias.StartsWith("ekm", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                FindMatchingCache(descendant.ContentType.Alias)?.Remove(descendant.Key);
            }

            descendantCount += descendants.Count;
            if (descendantCount >= totalRecords)
            {
                return;
            }

            pageIndex++;
        }
    }

    private void ClearMemoryCache(IContent content)
    {
        if (content.ContentType.Alias == "ekmProduct")
        {
            _cache.Remove($"{content.Id}_SerializeMetafields");
        }

        if (content.ContentType.Alias.Equals("ekmMetafield", StringComparison.OrdinalIgnoreCase))
        {
            _cache.Remove("GetMetafields");
        }

        if (content.ContentType.Alias is "ekmStore" or "ekmProductDiscount" or "ekmOrderDiscount")
        {
            PriceCache.InvalidateAll();
        }

        if (content.ContentType.Alias is "ekmProduct" or "ekmProductVariant")
        {
            PriceCache.InvalidateItem(content.Path);
        }
    }

    private void ClearLanguageCache()
    {
        _runtimeCache.Clear("ekmLanguages");
        _runtimeCache.Clear("ekmDefaultLanguage");
    }

    private async Task RevalidateAsync(IContent content, CancellationToken cancellationToken)
    {
        var headlessConfig = _config.HeadlessConfig();

        if (headlessConfig == null)
        {
            return;
        }

        await _revalidateService.RevalidateAsync(headlessConfig, content.Key, content.ContentType.Alias).ConfigureAwait(false);
    }
}
