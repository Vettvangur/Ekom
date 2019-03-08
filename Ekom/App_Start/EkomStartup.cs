using Ekom.App_Start;
using Ekom.Cache;
using Ekom.Helpers;
using Ekom.IoC;
using Ekom.Models.Data;
using Ekom.Services;
using Ekom.Utilities;
using Hangfire;
using log4net;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TinyIoC;
using Umbraco.Core;
using Umbraco.Core.Events;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Persistence;
using Umbraco.Core.Publishing;
using Umbraco.Core.Services;
using Umbraco.Web.Routing;

namespace Ekom
{
    /// <summary>
    /// Here we hook into the umbraco lifecycle methods to configure Ekom.
    /// We use ApplicationEventHandler so that these lifecycle methods are only run
    /// when umbraco is in a stable condition.
    /// </summary>
    public class EkomStartup : ApplicationEventHandler
    {
        Configuration _config;
        ILog _log;

        /// <summary>
        /// Event fired at start of ApplicationStarted
        /// </summary>
        public static event ExtensionRegistrations ApplicationStartedCalled;

        /// <summary>
        /// Methods that override unity type registrations
        /// </summary>
        /// <param name="typeMappings"></param>
        public delegate void ExtensionRegistrations(IList<IContainerRegistration> typeMappings);

        /// <summary>
        /// Umbraco startup lifecycle method
        /// </summary>
        /// <param name="umbracoApplication"></param>
        /// <param name="applicationContext"></param>
        protected override void ApplicationStarting(
            UmbracoApplicationBase umbracoApplication,
            ApplicationContext applicationContext
        )
        {
            LogHelper.Info(GetType(), "ApplicationStarting...");

            ContentFinderResolver.Current.InsertTypeBefore<ContentFinderByPageIdQuery, CatalogContentFinder>();
            UrlProviderResolver.Current.InsertTypeBefore<DefaultUrlProvider, CatalogUrlProvider>();
        }

        /// <summary>
        /// Umbraco startup lifecycle method
        /// </summary>
        /// <param name="umbracoApplication"></param>
        /// <param name="applicationContext"></param>
        protected override void ApplicationStarted(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            LogHelper.Info(GetType(), "ApplicationStarted...");
            var container = Configuration.container;

            TinyIoC();

            // Startup Dependencies
            var logFac = container.GetInstance<ILogFactory>();
            _log = logFac.GetLogger<EkomStartup>();
            _config = container.GetInstance<Configuration>();

            PrepareDatabase(applicationContext);

            // Controls which stock cache will be populated
            var stockCache = _config.PerStoreStock
                ? container.GetInstance<IPerStoreCache<StockData>>()
                : container.GetInstance<IBaseCache<StockData>>()
                as ICache;

            _config.CacheList.Value.Add(stockCache);

            var couponCache =
                 container.GetInstance<IBaseCache<CouponData>>()
                as ICache;

            _config.CacheList.Value.Add(couponCache);

            // Fill Caches
            foreach (var cacheEntry in _config.CacheList.Value)
            {
                cacheEntry.FillCache();
            }

            // VirtualContent=true allows for configuration of content nodes to use for matching all requests
            // Use case: Ekom populated by adapter, used as in memory cache with no backing umbraco nodes

            if (!_config.VirtualContent)
            {
                // Hook into Umbraco Events
                ContentService.Published += ContentService_Published;
                ContentService.UnPublished += ContentService_UnPublished;
                ContentService.Deleted += ContentService_Deleted;
                ContentService.Publishing += ContentService_Publishing;
                ContentService.Moved += ContentService_Moved;
            }

            // Hangfire
            GlobalConfiguration.Configuration.UseSqlServerStorage(applicationContext.DatabaseContext.ConnectionString);
            // ReSharper disable once ObjectCreationAsStatement
            new BackgroundJobServer();

            _log.Info("Ekom Started");
        }

        private void TinyIoC()
        {
            var tinyIoCContainer = TinyIoCContainer.Current;

            // Finish type registrations
            tinyIoCContainer.RegisterTypes();
            tinyIoCContainer.RegisterTypes(TinyIoCConfig.containerRegistrations);

            // Register extension types
            if (ApplicationStartedCalled != null)
            {
                var typeMappings = new List<IContainerRegistration>();
                ApplicationStartedCalled.Invoke(typeMappings);

                tinyIoCContainer.RegisterTypes(typeMappings);
            }
        }

        private void PrepareDatabase(ApplicationContext applicationContext)
        {
            var dbCtx = applicationContext.DatabaseContext;
            var dbHelper = new DatabaseSchemaHelper(dbCtx.Database, applicationContext.ProfilingLogger.Logger, dbCtx.SqlSyntax);

            //Check if the DB table does NOT exist
            if (!dbHelper.TableExist("EkomStock"))
            {
                //Create DB table - and set overwrite to false
                dbHelper.CreateTable<StockData>(false);
            }
            if (!dbHelper.TableExist("EkomOrdersActivityLog"))
            {
                //Create DB table - and set overwrite to false
                dbHelper.CreateTable<OrderActivityLog>(false);
            }
            if (!dbHelper.TableExist("EkomOrders"))
            {
                dbHelper.CreateTable<OrderData>(false);
                using (var db = dbCtx.Database)
                {
                    db.Execute("ALTER TABLE EkomOrders ALTER COLUMN OrderInfo NVARCHAR(MAX)");
                }
            }
            if (!dbHelper.TableExist("EkomCoupon"))
            {
                using (var db = dbCtx.Database)
                {
                    dbHelper.CreateTable<CouponData>(false);
                }
            }
            if (!dbHelper.TableExist(Configuration.DiscountStockTableName))
            {
                dbHelper.CreateTable<DiscountStockData>(false);
            }

            if (_config.StoreCustomerData
            && !dbHelper.TableExist("EkomCustomerData"))
            {
                dbHelper.CreateTable<CustomerData>(false);
            }
        }

        private void ContentService_Publishing(
            IPublishingStrategy strategy,
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
                    _log.Error("ContentService_Publishing Failed", ex);
                    throw;
                }
            }
        }

        private void ContentService_Published(
            IPublishingStrategy sender,
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
        private void ContentService_Moved(
            IContentService sender,
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

        private void ContentService_UnPublished(
            IPublishingStrategy sender,
            PublishEventArgs<IContent> args
        )
        {
            foreach (var node in args.PublishedEntities)
            {
                var cacheEntry = FindMatchingCache(node.ContentType.Alias);

                cacheEntry?.Remove(node.Key);
            }
        }

        private void ContentService_Deleted(IContentService sender, DeleteEventArgs<IContent> args)
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

        private void UpdateSlug(IContent content, string alias, PublishEventArgs<IContent> e)
        {
            var siblings = content.Parent().Children().Where(x => x.Published && x.Id != content.Id && !x.Trashed);

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

                    // Update Slug if Slug Exist on same Level and is Published
                    if (!string.IsNullOrEmpty(slug) && siblings.Any(x => NodeHelper.GetStoreProperty(x, "slug", store.Alias) == slug.ToLowerInvariant()))
                    {

                        // Random not a nice solution
                        Random rnd = new Random();

                        slug = slug + "-" + rnd.Next(1, 150);

                        _log.Warn("Duplicate slug found for product : " + content.Id + " store: " + store.Alias);

                        e.Messages.Add(new EventMessage("Duplicate Slug Found.", "Sorry but this slug is already in use, we updated it for you. Store: " + store.Alias, EventMessageType.Warning));
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
