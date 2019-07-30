using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core;
using Umbraco.Core.Composing;
using Umbraco.Core.Events;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Persistence;
using Umbraco.Core.Services;
using Umbraco.Web.Routing;
using Umbraco.Web;
using Umbraco.Core.Services.Implement;
using Ekom.Cache;
using Ekom.Utilities;

namespace Ekom.App_Start
{
    class UmbracoEventListeners
    {
        readonly ILogger _logger;
        readonly Configuration _config;
        readonly UmbracoHelper _umbHelper;

        /// <summary>
        /// Initializes a new instance of the <see cref="UmbracoEventListeners"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="config">The configuration.</param>
        /// <param name="umbHelper">The umb helper.</param>
        public UmbracoEventListeners(ILogger logger, Configuration config, UmbracoHelper umbHelper)
        {
            _logger = logger;
            _config = config;
            _umbHelper = umbHelper;
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
                        UpdateSlug(content, alias, e, cs);
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
            PublishEventArgs<IContent> e,
            IContentService cs)
        {
            var parent = _umbHelper.Content(content.ParentId);
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
                        x => NodeHelper.GetStoreProperty(x, "slug", store.Alias) 
                        == slug.ToLowerInvariant())
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
    }
}
