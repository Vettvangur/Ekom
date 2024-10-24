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
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Ekom.App_Start
{
    class UmbracoEventListeners :
        //INotificationHandler<ContentPublishingNotification>,
        INotificationAsyncHandler<ContentPublishedNotification>,
        INotificationAsyncHandler<ContentUnpublishedNotification>,
        INotificationHandler<ContentSavingNotification>,
        INotificationHandler<ContentDeletedNotification>,
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
            RevalidateService revalidateService)
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
        }

        public void Handle(ContentSavingNotification e)
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
                            UpdatePropertiesDefaultValues(content, alias, e);
                        }

                        if (alias == "ekmProduct" || alias == "ekmProductVariant")
                        {
                            var stockValue = content.GetValue<string>("stock");

                            if (!string.IsNullOrEmpty(stockValue))
                            {

                                var stockArray = JsonConvert.DeserializeObject<IEnumerable<StockRequest>>(stockValue);

                                if (stockArray != null)
                                {
                                    foreach (var stockItem in stockArray)
                                    {
                                        if (!string.IsNullOrEmpty(stockItem.StoreAlias))
                                        {
                                            var updated = Stock.Instance.SetStockAsync(content.Key, stockItem.StoreAlias, stockItem.Value).Result;
                                        }
                                        else
                                        {
                                            var updated = Stock.Instance.SetStockAsync(content.Key, stockItem.Value).Result;
                                        }
                                    }
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

                    cacheEntry?.AddReplace(new Umbraco10Content(node, parentNode.Key));
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

                    cacheEntry?.AddReplace(new Umbraco10Content(node, parentNode != null ? parentNode.Key : Guid.Empty));
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

        public void Handle(ContentDeletedNotification args)
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
                        _couponRepository.DeleteCouponsByDiscountAsync(node.Key).Wait();
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

        private void UpdatePropertiesDefaultValues(
            IContent content,
            string alias,
            ContentSavingNotification e)
        {

            if (content.HasProperty("updateSlug") && content.GetValue<bool>("updateSlug"))
            {
                var propertyType = PropertyEditorType.Language;
                var propertyTypes = new List<string>();
                var slugItems = new Dictionary<string, object>();

                content.SetValue("updateSlug", false);

                var titlePropertyValue = JsonConvert.DeserializeObject<PropertyValue>(content.GetValue<string>("title"));


                if (titlePropertyValue.Type == PropertyEditorType.Language)
                {
                    var languages = _umbracoService.GetLanguages();

                    propertyTypes.AddRange(languages.Select(x => x.IsoCode));
                }
                else if (titlePropertyValue.Type == PropertyEditorType.Store)
                {
                    propertyType = PropertyEditorType.Store;
                    var stores = API.Store.Instance.GetAllStores().OrderBy(x => x.SortOrder);

                    propertyTypes.AddRange(stores.Select(x => x.Alias));

                }

                foreach (var type in propertyTypes)
                {
                    if (alias != "ekmProduct" && alias != "ekmCategory") continue;
                    
                    var title = content.GetProperty("title", type);

                    var slug = string.Empty; // NodeHelper.GetStoreProperty(content, "slug", store.Alias).Trim();

                    if (string.IsNullOrEmpty(slug) && !string.IsNullOrEmpty(title))
                    {
                        slug = title;
                    }

                    slug = slug.ToLowerInvariant();

                    slugItems.Add(type, slug.ToUrlSegment(_shortStringHelper));


                }

                if (slugItems.Any())
                {
                    content.SetProperty("slug", slugItems, propertyType);
                }
            }

            //var propertyType = PropertyEditorType.Language;
            //var propertyTypes = new List<string>();
            //var slugItems = new Dictionary<string, object>();
            //var titleItems = new Dictionary<string, object>();

            //try
            //{
            //    var titlePropertyValue = JsonConvert.DeserializeObject<PropertyValue>(content.GetValue<string>("title"));

            //    if (titlePropertyValue.Type == "Language")
            //    {
            //        var languages = _umbracoService.GetLanguages();

            //        propertyTypes.AddRange(languages.Select(x => x.IsoCode));
            //    }
            //    else if (titlePropertyValue.Type == "Store")
            //    {
            //        propertyType = PropertyEditorType.Store;
            //        var stores = API.Store.Instance.GetAllStores().OrderBy(x => x.SortOrder);

            //        propertyTypes.AddRange(stores.Select(x => x.Alias));

            //    }

            //    var name = content.Name.Trim();
            //    var title = ""; //TODO  //NodeHelper.GetStoreProperty(content, "title", store.Alias).Trim();

            //    if (string.IsNullOrEmpty(title))
            //    {
            //        title = name;
            //    }

            //    foreach (var type in propertyTypes)
            //    {
            //        titleItems.Add(type, title);

            //        if (alias == "ekmProduct" || alias == "ekmCategory")
            //        {
            //            var slug = string.Empty; // NodeHelper.GetStoreProperty(content, "slug", store.Alias).Trim();

            //            if (string.IsNullOrEmpty(slug) && !string.IsNullOrEmpty(title))
            //            {
            //                slug = title;
            //            }

            //            slug = slug.ToLowerInvariant();

            //            slugItems.Add(type, slug.ToUrlSegment(_shortStringHelper));

            //        }

            //    }

            //    if (slugItems.Any())
            //    {
            //        content.SetProperty("slug", slugItems, propertyType);
            //    }

            //    if (titleItems.Any())
            //    {
            //        content.SetProperty("title", titleItems, propertyType);
            //    }

            //}
            //catch (Exception ex)
            //{

            //}


            //var stores = API.Store.Instance.GetAllStores();

            //var slugItems = new Dictionary<string, object>();
            //var titleItems = new Dictionary<string, object>();

            //foreach (var store in stores.OrderBy(x => x.SortOrder))
            //{
            //    var name = content.Name.Trim();

            //    var title = string.Empty; //NodeHelper.GetStoreProperty(content, "title", store.Alias).Trim();

            //    if (string.IsNullOrEmpty(title))
            //    {
            //        title = name;
            //    }

            //    titleItems.Add(store.Alias, title);

            //    if (alias == "ekmProduct" || alias == "ekmCategory")
            //    {

            //        var slug = string.Empty; // NodeHelper.GetStoreProperty(content, "slug", store.Alias).Trim();

            //        if (string.IsNullOrEmpty(slug) && !string.IsNullOrEmpty(title))
            //        {
            //            slug = title;
            //        }

            //        slug = slug.ToLowerInvariant();

            //        var parentCategory = API.Catalog.Instance.GetCategory(store.Alias, content.ParentId);

            //        if (parentCategory != null)
            //        {
            //            var products = parentCategory.Products.Where(x => x.Id != content.Id);
            //            var categories = parentCategory.SubCategories.Where(x => x.Id != content.Id);

            //            if (products.Any(x => x.Slug == slug) || categories.Any(x => x.Slug == slug))
            //            {
            //                Random rnd = new Random();

            //                slug = slug + "-" + rnd.Next(10, 500);

            //                _logger.LogWarning(
            //                    "Duplicate slug found for product : {Id} store: {Store}",
            //                    content.Id,
            //                    store.Alias);

            //                e.Messages.Add(
            //                    new EventMessage(
            //                        "Duplicate Slug Found.",
            //                        "Sorry but this slug is already in use, we updated it for you. Store: " + store.Alias,
            //                        EventMessageType.Warning
            //                    )
            //                );
            //            }

            //        }

            //        slugItems.Add(store.Alias, slug.ToUrlSegment(_shortStringHelper));
            //    }
            //}

            //if (slugItems.Any())
            //{
            //    //content.SetVortoValue("slug", slugItems);
            //}

            //if (titleItems.Any())
            //{
            //    //content.SetVortoValue("title", titleItems);
            //}
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
                _storeCache.AddReplace(new Umbraco10Content(ekmStoreContent, Guid.Empty));
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
                    cacheEntryForDesc?.AddReplace(new Umbraco10Content(descendant));
                }

            }

            foreach (var ancestor in ancestors.Where(x => x.ContentType.Alias is "ekmCategory" or "ekmProduct"))
            {
                var cacheEntryForDesc = FindMatchingCache(ancestor.ContentType.Alias);

                cacheEntryForDesc?.AddReplace(new Umbraco10Content(ancestor));
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
}
