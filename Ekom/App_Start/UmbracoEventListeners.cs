using Ekom.Cache;
using Ekom.Exceptions;
using Ekom.Interfaces;
using Ekom.Utilities;
using Newtonsoft.Json;
using Our.Umbraco.Vorto.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Events;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Persistence;
using Umbraco.Core.Services;
using Umbraco.Web;

namespace Ekom.App_Start
{
    class UmbracoEventListeners
    {
        UmbracoHelper UmbHelper => Umbraco.Web.Composing.Current.UmbracoHelper;

        readonly ILogger _logger;
        readonly Configuration _config;
        readonly IBaseCache<IStore> _storeCache;
        readonly IStoreDomainCache _storeDomainCache;
        readonly IContentService _cs;

        /// <summary>
        /// Initializes a new instance of the <see cref="UmbracoEventListeners"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="config">The configuration.</param>
        /// <param name="storeCache">The store cache.</param>
        /// <param name="storeDomainCache">The store domain cache.</param>
        /// <param name="cs">The cs.</param>
        public UmbracoEventListeners(
            ILogger logger,
            Configuration config,
            IBaseCache<IStore> storeCache,
            IStoreDomainCache storeDomainCache,
            IContentService cs)
        {
            _logger = logger;
            _config = config;
            _storeCache = storeCache;
            _storeDomainCache = storeDomainCache;
            _cs = cs;
        }

        public void ContentService_Publishing(
            IContentService cs,
            PublishEventArgs<IContent> e)
        {
            foreach (var content in e.PublishedEntities)
            {
                var alias = content.ContentType.Alias;

                try
                {
                    if (alias == "ekmProduct" || alias == "ekmCategory" || alias == "ekmProductVariantGroup" || alias == "ekmProductVariant")
                    {
                        UpdateSlug(content, alias, e);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error<UmbracoEventListeners>("ContentService_Publishing Failed", ex);
                    throw;
                }
            }
        }

        public void ContentService_Published(
            IContentService cs,
            PublishEventArgs<IContent> args
        )
        {
            foreach (var node in args.PublishedEntities)
            {
                var cacheEntry = FindMatchingCache(node.ContentType.Alias);

                cacheEntry?.AddReplace(node);
            }
        }

        //TODO Needs testing
        public void ContentService_Moved(
            IContentService cs,
            MoveEventArgs<IContent> args
        )
        {
            foreach (var info in args.MoveInfoCollection)
            {
                var node = info.Entity;

                var cacheEntry = FindMatchingCache(node.ContentType.Alias);

                cacheEntry?.Remove(node.Key);
                cacheEntry?.AddReplace(node);
            }
        }

        public void ContentService_UnPublished(
            IContentService cs,
            PublishEventArgs<IContent> args
        )
        {
            foreach (var node in args.PublishedEntities)
            {
                var cacheEntry = FindMatchingCache(node.ContentType.Alias);

                cacheEntry?.Remove(node.Key);
            }
        }

        public void ContentService_Deleted(IContentService sender, DeleteEventArgs<IContent> args)
        {
            foreach (var node in args.DeletedEntities)
            {
                var cacheEntry = FindMatchingCache(node.ContentType.Alias);

                cacheEntry?.Remove(node.Key);
            }
        }

        private ICache FindMatchingCache(string contentTypeAlias)
        {
            return _config.CacheList.Value.FirstOrDefault(x
                => !string.IsNullOrEmpty(x.NodeAlias)
                && x.NodeAlias == contentTypeAlias
            );
        }

        private void UpdateSlug(
            IContent content,
            string alias,
            PublishEventArgs<IContent> e)
        {
            var parent = UmbHelper.Content(content.ParentId);
            var siblings = parent.Children().Where(x => x.Id != content.Id && !x.IsPublished());

            var stores = API.Store.Instance.GetAllStores();

            var slugItems = new Dictionary<string, object>();
            var titleItems = new Dictionary<string, object>();

            foreach (var store in stores.OrderBy(x => x.SortOrder))
            {
                var name = content.Name.Trim();

                var title = NodeHelper.GetStoreProperty(content, "title", store.Alias).Trim();

                if (string.IsNullOrEmpty(title))
                {
                    title = name;
                    titleItems.Add(store.Alias, title);
                }

                if (alias == "ekmProduct" || alias == "ekmCategory")
                {
                    var slug = NodeHelper.GetStoreProperty(content, "slug", store.Alias).Trim();

                    if (string.IsNullOrEmpty(slug) && !string.IsNullOrEmpty(title))
                    {
                        slug = title;
                    }

                    // Update Slug if Slug Exists on same Level and is Published
                    if (!string.IsNullOrEmpty(slug)
                    && siblings.Any(
                        x => x.GetVortoValue<string>("slug", store.Alias) == slug.ToLowerInvariant())
                    )
                    {
                        // Random not a nice solution
                        Random rnd = new Random();

                        slug = slug + "-" + rnd.Next(1, 150);

                        _logger.Warn<UmbracoEventListeners>(
                            "Duplicate slug found for product : " + content.Id + " store: " + store.Alias);

                        e.Messages.Add(
                            new EventMessage(
                                "Duplicate Slug Found.",
                                "Sorry but this slug is already in use, we updated it for you. Store: " + store.Alias,
                                EventMessageType.Warning
                            )
                        );
                    }

                    slugItems.Add(store.Alias, slug.ToUrlSegment().ToLowerInvariant());
                }
            }

            if (slugItems.Any())
            {
                content.SetVortoValue("slug", slugItems);
            }

            if (titleItems.Any())
            {
                content.SetVortoValue("title", titleItems);
            }
        }

        public void DomainSaved(IDomainService _ds, SaveEventArgs<IDomain> saveEventArgs)
        {
            foreach (var d in saveEventArgs.SavedEntities)
            {
                _storeDomainCache.AddReplace(d);
            }

            var domain = saveEventArgs.SavedEntities.FirstOrDefault();

            if (domain != null)
            {
                if (domain.RootContentId != null)
                {
                    var rootContent = _cs.GetById(domain.RootContentId.Value);
                    IContent ekmStoreContent;
                    if (int.TryParse(rootContent.GetValue<string>("ekmStorePicker"), out int storeId))
                    {
                        ekmStoreContent = _cs.GetById(storeId);
                    }
                    else
                    {
                        var srn = GuidUdi.Parse(rootContent.GetValue<string>("ekmStorePicker"));
                        ekmStoreContent = _cs.GetById(srn.Guid);
                    }
                    
                    if (ekmStoreContent?.ContentType?.Alias != "ekmStore")
                    {
                        throw new EventException(
                            "Error updating store! " +
                            $"Erronous ekom store picked for root content {domain.RootContentId.Value} domain {domain.DomainName}. " +
                            "Please ensure you have correctly selected a ekmStore node using the ekmStorePicker on this root content node."
                        );
                    }

                    // Update cached IStore
                    _storeCache.AddReplace(ekmStoreContent);
                }
            }
        }

        public void DomainDeleted(IDomainService _ds, DeleteEventArgs<IDomain> saveEventArgs)
        {
            foreach (var d in saveEventArgs.DeletedEntities)
            {
                _storeDomainCache.Remove(d.Key);
            }

            var domain = saveEventArgs.DeletedEntities.FirstOrDefault();

            if (domain != null)
            {
                if (domain.RootContentId != null)
                {
                    var rootContent = _cs.GetById(domain.RootContentId.Value);
                    IContent ekmStoreContent;
                    if (int.TryParse(rootContent.GetValue<string>("ekmStorePicker"), out int storeId))
                    {
                        ekmStoreContent = _cs.GetById(storeId);
                    }
                    else
                    {
                        var srn = GuidUdi.Parse(rootContent.GetValue<string>("ekmStorePicker"));
                        ekmStoreContent = _cs.GetById(srn.Guid);
                    }

                    if (ekmStoreContent?.ContentType?.Alias != "ekmStore")
                    {
                        throw new EventException(
                            "Error updating store! " +
                            $"Erronous ekom store picked for root content {domain.RootContentId.Value} domain {domain.DomainName}. " +
                            "Please ensure you have correctly selected a ekmStore node using the ekmStorePicker on this root content node."
                        );
                    }

                    // Update cached IStore
                    _storeCache.AddReplace(ekmStoreContent);
                }
            }
        }
    }
}
