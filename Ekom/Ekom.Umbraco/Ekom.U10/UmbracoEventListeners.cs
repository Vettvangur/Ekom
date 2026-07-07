using Ekom.API;
using Ekom.Cache;
using Ekom.Models;
using Ekom.Repositories;
using Ekom.Services;
using Ekom.Umb.Models;
using Ekom.Umb.Services;
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
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Ekom.App_Start;

class UmbracoEventListeners :
    //INotificationHandler<ContentPublishingNotification>,
    INotificationAsyncHandler<ContentPublishedNotification>,
    INotificationAsyncHandler<ContentUnpublishedNotification>,
    INotificationAsyncHandler<ContentSavingNotification>,
    INotificationAsyncHandler<ContentDeletedNotification>,
    INotificationHandler<ContentMovedToRecycleBinNotification>,
    INotificationHandler<ContentMovedNotification>,
    INotificationHandler<DomainSavedNotification>,
    INotificationHandler<DomainDeletedNotification>,
    INotificationHandler<ServerVariablesParsingNotification>,
    INotificationHandler<LanguageCacheRefresherNotification>
{
    readonly ILogger _logger;
    readonly IShortStringHelper _shortStringHelper;
    readonly Configuration _config;
    readonly IBaseCache<IStore> _storeCache;
    readonly IStoreDomainCache _storeDomainCache;
    readonly IContentService _cs;
    readonly IUmbracoContextFactory _context;
    readonly IUmbracoService _umbracoService;
    readonly IAppPolicyCache _runtimeCache;
    private IMemoryCache _cache;
    private CouponRepository _couponRepository;
    private INodeService _nodeService;
    private RevalidateService _revalidateService;
    private IEkomRichTextResolver _richTextResolver;
    /// <summary>
    /// Initializes a new instance of the <see cref="UmbracoEventListeners"/> class.
    /// </summary>
    public UmbracoEventListeners(
        ILogger<UmbracoEventListeners> logger,
        Configuration config,
        IBaseCache<IStore> storeCache,
        IStoreDomainCache storeDomainCache,
        IContentService cs,
        IUmbracoContextFactory context,
        IShortStringHelper shortStringHelper,
        IUmbracoService umbracoService,
        IMemoryCache cache,
        AppCaches appCaches,
        CouponRepository couponRepository,
        INodeService nodeService,
        RevalidateService revalidateService,
        IEkomRichTextResolver richTextResolver)
    {
        _logger = logger;
        _config = config;
        _storeCache = storeCache;
        _storeDomainCache = storeDomainCache;
        _cs = cs;
        _context = context;
        _shortStringHelper = shortStringHelper;
        _umbracoService = umbracoService;
        _runtimeCache = appCaches.RuntimeCache;
        _cache = cache;
        _couponRepository = couponRepository;
        _nodeService = nodeService;
        _revalidateService = revalidateService;
        _richTextResolver = richTextResolver;
    }
    public async Task HandleAsync(ContentSavingNotification e, CancellationToken ct)
    {
        foreach (var content in e.SavedEntities)
        {
            if (content.ContentType.Alias.StartsWith("ekm"))
            {
                var alias = content.ContentType.Alias;

                ClearMemoryCache(content);

                try
                {

                    if (alias == "ekmProduct" || alias == "ekmCategory" || alias == "ekmProductVariantGroup" || alias == "ekmProductVariant" || alias == "ekmProductDiscount" || alias == "ekmOrderDiscount")
                    {
                        await UpdatePropertiesDefaultValuesAsync(content, alias, e, ct);
                    }

                    if (alias == "ekmProduct" || alias == "ekmProductVariant")
                    {
                        var stockValue = content.GetValue<string>("stock");

                        if (!string.IsNullOrEmpty(stockValue))
                        {
                            try
                            {
                                var stockArray = JsonConvert.DeserializeObject<IEnumerable<StockRequest>>(stockValue)?
                                    .Select(x => new StockRequest
                                    {
                                        StoreAlias = x.StoreAlias,
                                        Value = x.Value ?? 0
                                    })
                                    .ToList();

                                if (stockArray != null)
                                {
                                    foreach (var stockItem in stockArray)
                                    {
                                        if (!string.IsNullOrEmpty(stockItem.StoreAlias))
                                        {
                                            var updated = await Stock.Instance.SetStockAsync(content.Key, stockItem.StoreAlias, stockItem.Value ?? 0, ct);
                                        }
                                        else
                                        {
                                            var updated = await Stock.Instance.SetStockAsync(content.Key, stockItem.Value ?? 0, ct);
                                        }
                                    }
                                }
                            }
                            catch (JsonException ex)
                            {
                                _logger.LogError(ex, $"Could not map stock value to stock request on node {content.Id}. Value: {stockValue}");
                            }
                        }


                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"ContentService_Saving Failed for {content.Id}");
                    throw;
                }
            }
        }
    }

    public async Task HandleAsync(ContentPublishedNotification args, CancellationToken cancellationToken)
    {

        foreach (var node in args.PublishedEntities)
        {
            if (node.ContentType.Alias.StartsWith("ekm"))
            {
                var cacheEntry = FindMatchingCache(node.ContentType.Alias);

                var parentNode = _nodeService.NodeById(node.ParentId);

                if (parentNode == null) { continue; }

                cacheEntry?.AddReplace(new Umbraco10Content(node, parentNode.Key, _richTextResolver));
                //Fire and forget
                RevalidateAsync(node, cancellationToken).ConfigureAwait(false);

                // If slug changes on category then we need to update the cache for all descending products.
                if (node.ContentType.Alias != "ekmCategory") continue;

                var dirty = (IRememberBeingDirty)node;
                var slugHasChanged = dirty.WasPropertyDirty("slug");
                var disabledChanged = dirty.WasPropertyDirty("disable");

                if (slugHasChanged || disabledChanged)
                {
                    RefreshCacheForRelatedNodes(node.Id);
                }
            }


        }
    }

    //TODO Needs testing
    public void Handle(ContentMovedNotification args)
    {
        foreach (var info in args.MoveInfoCollection)
        {
            var node = info.Entity;

            if (node.ContentType.Alias.StartsWith("ekm"))
            {
                var cacheEntry = FindMatchingCache(node.ContentType.Alias);

                cacheEntry?.Remove(node.Key);

                var parentNode = _nodeService.NodeById(node.Id);

                cacheEntry?.AddReplace(new Umbraco10Content(node, parentNode != null ? parentNode.Key : Guid.Empty, _richTextResolver));

                if (node.ContentType.Alias == "ekmCategory")
                {
                    RefreshCacheForRelatedNodes(node.Id);
                }
            }


        }
    }

    public async Task HandleAsync(ContentUnpublishedNotification args, CancellationToken cancellationToken)
    {
        foreach (var node in args.UnpublishedEntities)
        {
            if (node.ContentType.Alias.StartsWith("ekm"))
            {
                ClearMemoryCache(node);

                var cacheEntry = FindMatchingCache(node.ContentType.Alias);

                cacheEntry?.Remove(node.Key);

                RefreshCacheForRelatedNodes(node.Id, true);

                //Fire and forget
                RevalidateAsync(node, cancellationToken).ConfigureAwait(false);

#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            }
        }
    }

    public async Task HandleAsync(ContentDeletedNotification args, CancellationToken cancellationToken)
    {
        foreach (var node in args.DeletedEntities)
        {
            if (node.ContentType.Alias.StartsWith("ekm"))
            {
                ClearMemoryCache(node);

                var cacheEntry = FindMatchingCache(node.ContentType.Alias);

                cacheEntry?.Remove(node.Key);

                if (node.ContentType.Alias == "ekmOrderDiscount")
                {
                    await _couponRepository.DeleteCouponsByDiscountAsync(node.Key, cancellationToken);
                }
            }
        }
    }

    public void Handle(ContentMovedToRecycleBinNotification args)
    {
        foreach (var node in args.MoveInfoCollection)
        {
            if (node.Entity.ContentType.Alias.StartsWith("ekm"))
            {
                var cacheEntry = FindMatchingCache(node.Entity.ContentType.Alias);

                cacheEntry?.Remove(node.Entity.Key);
            }


        }
    }

    private ICache? FindMatchingCache(string contentTypeAlias)
    {
        if (contentTypeAlias.Contains("ekmpaymentprovider", StringComparison.InvariantCulture))
        {
            return _config.CacheList.Value.FirstOrDefault(x
                => !string.IsNullOrEmpty(x.NodeAlias)
                && contentTypeAlias.StartsWith(x.NodeAlias, StringComparison.InvariantCulture)
            );
        }

        return _config.CacheList.Value.FirstOrDefault(x
            => !string.IsNullOrEmpty(x.NodeAlias)
               && contentTypeAlias.Equals(x.NodeAlias, StringComparison.InvariantCulture)
        );
    }

    private async Task UpdatePropertiesDefaultValuesAsync(
        IContent content,
        string alias,
        ContentSavingNotification e, 
        CancellationToken ct)
    {
        if (!content.HasProperty("updateSlug") || !content.GetValue<bool>("updateSlug"))
            return;

        content.SetValue("updateSlug", false);

        var titleValue = content.GetValue<string>("title") ?? "";

        var propertyValue = JsonConvert.DeserializeObject<PropertyValue>(titleValue);

        if (propertyValue == null)
        {
            return;
        }

        var propertyType = propertyValue.Type;
        var propertyTypes = GetPropertyTypes(propertyType);

        var slugItems = new Dictionary<string, object>();
        var parentCategory = alias == "ekmProduct" ? await Catalog.Instance.GetCategoryAsync(content.ParentId, global: true, raiseEvent: false, ct: ct) : null;
        var siblingProducts =
            parentCategory == null
                ? new List<IProduct>()
                : (await parentCategory.ProductsAsync(ct: ct))
                    .Products
                    .Where(p => p.Id != content.Id)
                    .ToList();

        if (alias != "ekmProduct" && alias != "ekmCategory")
        {
            return;
        }

        // Set slug only for Product and Category
        foreach (var (type, key) in propertyTypes)
        {
            var title = content.GetProperty("title", key);
            if (string.IsNullOrWhiteSpace(title))
                continue;

            var slug = content.GetProperty("slug", key);
            slug = string.IsNullOrWhiteSpace(slug) ? title : slug;
            slug = slug.ToUrlSegment(_shortStringHelper);

            if (IsSlugDuplicate(siblingProducts, slug, key))
            {
                slug = GenerateUniqueSlug(slug);
                _logger.LogWarning("Duplicate slug found for product: {Id}", content.Id);

                e.Messages.Add(new EventMessage(
                    "Duplicate Slug Found",
                    $"Slug was updated due to a conflict. Language/Store: {key}",
                    EventMessageType.Warning
                ));
            }

            slugItems.TryAdd(key, slug);
        }

        if (slugItems.Any())
        {
            content.SetProperty("slug", slugItems, propertyType);
        }
    }

    private List<KeyValuePair<string, string>> GetPropertyTypes(PropertyEditorType propertyType)
    {
        return propertyType switch
        {
            PropertyEditorType.Language => _umbracoService.GetLanguages()
                .Select(lang => new KeyValuePair<string, string>("Language", lang.IsoCode))
                .ToList(),

            PropertyEditorType.Store => API.Store.Instance.GetAllStores()
                .OrderBy(store => store.SortOrder)
                .Select(store => new KeyValuePair<string, string>("Store", store.Alias))
                .ToList(),

            _ => new List<KeyValuePair<string, string>>()
        };
    }

    private bool IsSlugDuplicate(List<IProduct> products, string slug, string typeKey)
    {
        return products.Any(p => p.GetValue("slug", typeKey) == slug);
    }

    private string GenerateUniqueSlug(string baseSlug)
    {
        var rnd = new Random();
        return $"{baseSlug}-{rnd.Next(10, 1000)}";
    }


    public void Handle(DomainSavedNotification saveEventArgs)
    {
        foreach (var d in saveEventArgs.SavedEntities)
        {
            _storeDomainCache.AddReplace(new Umbraco10Domain(d));
        }

        var domain = saveEventArgs.SavedEntities.FirstOrDefault();

        if (domain?.RootContentId == null) return;
        
        var rootContent = _cs.GetById(domain.RootContentId.Value);

        if (rootContent == null) return;

        var store = _storeCache.Cache.Values.FirstOrDefault(x => x.StoreRootNodeId == rootContent.Id);

        if (store == null) return;
        
        var ekmStoreContent = _cs.GetById(store.Id);

        if (ekmStoreContent != null)
        {
            // Update cached IStore
            _storeCache.AddReplace(new Umbraco10Content(ekmStoreContent, Guid.Empty, _richTextResolver));
        }
    }

    public void Handle(DomainDeletedNotification n)
    {
        foreach (var d in n.DeletedEntities)
        {
            _storeDomainCache.Remove(d.Key);
        }

        var domain = n.DeletedEntities.FirstOrDefault();

        if (domain != null)
        {
            // FIX
            //if (domain.RootContentId != null)
            //{
            //    var rootContent = _cs.GetById(domain.RootContentId.Value);
            //    IContent ekmStoreContent;
            //    if (int.TryParse(rootContent.GetValue<string>("ekmStorePicker"), out int storeId))
            //    {
            //        ekmStoreContent = _cs.GetById(storeId);
            //    }
            //    else
            //    {
            //        var srn = GuidUdi.TryParse(rootContent.GetValue<string>("ekmStorePicker"), out var _udi);

            //        ekmStoreContent = _cs.GetById(_udi.Guid);
            //    }

            //    if (ekmStoreContent?.ContentType?.Alias != "ekmStore")
            //    {
            //        throw new EventException(
            //            "Error updating store! " +
            //            $"Erronous ekom store picked for root content {domain.RootContentId.Value} domain {domain.DomainName}. " +
            //            "Please ensure you have correctly selected a ekmStore node using the ekmStorePicker on this root content node."
            //        );
            //    }

            //    // Update cached IStore
            //    _storeCache.AddReplace(ekmStoreContent);

        }
    }

    public void Handle(ServerVariablesParsingNotification notification)
    {
        notification.ServerVariables.Add("ekom", new
        { 
            backofficeApiEndpoint = "/ekom/backoffice/",
            apiEndpoint = "/ekom/api/",
            managerEndpoint = "/ekom/manager/",
            charCollections = _config.CharCollections
        });
    }
    public void Handle(LanguageCacheRefresherNotification notification)
    {
        _runtimeCache.Clear("ekmLanguages");
        _runtimeCache.Clear("ekmDefaultLanguage");
    }

    private void RefreshCacheForRelatedNodes(int Id, bool remove = false)
    {
        using var cref = _context.EnsureUmbracoContext();
        
        var cache = cref.UmbracoContext.Content;

        var currentNode = cache.GetById(Id);

        if (currentNode == null) return;
        
        var descendants = currentNode.Descendants();
        var ancestors = currentNode.Ancestors().Where(x => x.ContentType.Alias.StartsWith("ekm"));

        foreach (var descendant in descendants)
        {
            var cacheEntryForDesc = FindMatchingCache(descendant.ContentType.Alias);

            if (remove)
            {
                cacheEntryForDesc?.Remove(descendant.Key);
            }
            else
            {
                cacheEntryForDesc?.AddReplace(new Umbraco10Content(descendant, richTextResolver: _richTextResolver));
            }

        }

        foreach (var ancestor in ancestors.Where(x => x.ContentType.Alias is "ekmCategory" or "ekmProduct"))
        {
            var cacheEntryForDesc = FindMatchingCache(ancestor.ContentType.Alias);

            cacheEntryForDesc?.AddReplace(new Umbraco10Content(ancestor, richTextResolver: _richTextResolver));
        }
    }
    
    private void ClearMemoryCache(IContent content)
    {

        if (content.ContentType.Alias == "ekmProduct")
        {
            _cache.Remove($"{content.Id}_SerializeMetafields");
        }
        if (content.ContentType.Alias == "ekmMetaField")
        {
            _cache.Remove($"GetMetafields");
        }

        if (content.ContentType.Alias == "ekmStore" || content.ContentType.Alias == "ekmProductDiscount" || content.ContentType.Alias == "ekmOrderDiscount")
        {
            PriceCache.InvalidateAll();
        }

        if (content.ContentType.Alias == "ekmProduct" || content.ContentType.Alias == "ekmProductVariant")
        {
            PriceCache.InvalidateItem(content.Path);
        }
    }

    private async Task RevalidateAsync(IContent content, CancellationToken cancellationToken)
    {

        var headlessConfig = _config.HeadlessConfig();

        if (headlessConfig == null)
        {
            return;
        }

        await _revalidateService.RevalidateAsync(headlessConfig, content.Key, content.ContentType.Alias);

    }
}
