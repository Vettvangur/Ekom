using Hangfire;
using log4net;
using Microsoft.Practices.Unity;
using System;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Events;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Persistence;
using Umbraco.Core.Publishing;
using Umbraco.Core.Services;
using Umbraco.Web.Routing;
using uWebshop.App_Start;
using uWebshop.Cache;
using uWebshop.Models.Data;
using uWebshop.Services;

namespace uWebshop
{
#pragma warning disable IDE1006 // Naming Styles
    /// <summary>
    /// Here we hook into the umbraco lifecycle methods to configure uWebshop.
    /// We use ApplicationEventHandler so that these lifecycle methods are only run
    /// when umbraco is in a stable condition.
    /// </summary>
    class uWebshopStartup : ApplicationEventHandler
    {
#pragma warning restore IDE1006 // Naming Styles
        Configuration _config;
        ILog _log;

        /// <summary>
        /// Event fired at start of ApplicationStarted
        /// </summary>
        public static event ExtensionRegistrations ApplicationStartedCalled;

        /// <summary>
        /// Methods that override unity type registrations
        /// </summary>
        /// <param name="c"></param>
        public delegate void ExtensionRegistrations(IUnityContainer c);

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

            // Register extension types
            var container = UnityConfig.GetConfiguredContainer();
            ApplicationStartedCalled?.Invoke(container);

            // Startup Dependencies
            var logFac = container.Resolve<ILogFactory>();
            _log = logFac.GetLogger(typeof(uWebshopStartup));
            _config = container.Resolve<Configuration>();

            // Controls which stock cache will be populated
            var stockCache = _config.PerStoreStock
                ? container.Resolve<IPerStoreCache<StockData>>()
                : container.Resolve<IBaseCache<StockData>>()
                as ICache;

            _config.CacheList.Value.Add(stockCache);

            // Fill Caches
            foreach (var cacheEntry in _config.CacheList.Value)
            {
                cacheEntry.FillCache();
            }

            // VirtualContent=true allows for configuration of content nodes to use for matching all requests
            // Use case: uWebshop populated by adapter, used as in memory cache with no backing umbraco nodes

            if (!_config.VirtualContent)
            {
                // Hook into Umbraco Events
                ContentService.Published += ContentService_Published;
                ContentService.UnPublished += ContentService_UnPublished;
                ContentService.Deleted += ContentService_Deleted;
                ContentService.Publishing += ContentService_Publishing;
            }

            var dbCtx = applicationContext.DatabaseContext;
            var dbHelper = new DatabaseSchemaHelper(dbCtx.Database, applicationContext.ProfilingLogger.Logger, dbCtx.SqlSyntax);

            //Check if the DB table does NOT exist
            if (!dbHelper.TableExist("uWebshopStock"))
            {
                //Create DB table - and set overwrite to false
                dbHelper.CreateTable<StockData>(false);
            }
            if (!dbHelper.TableExist("uWebshopOrders"))
            {
                //Create DB table - and set overwrite to false
                dbHelper.CreateTable<OrderData>(false);
                using (var db = dbCtx.Database)
                {
                    db.Execute("ALTER TABLE uWebshopOrders ALTER COLUMN OrderInfo NVARCHAR(MAX)");
                }
            }

            if (_config.StoreCustomerData
            && !dbHelper.TableExist("uwbsCustomerData"))
            {
                dbHelper.CreateTable<CustomerData>(false);
            }

            // Hangfire
            GlobalConfiguration.Configuration.UseSqlServerStorage(dbCtx.ConnectionString);
            new BackgroundJobServer();
        }

        private void ContentService_Publishing(IPublishingStrategy strategy, PublishEventArgs<IContent> e)
        {
            foreach (var content in e.PublishedEntities)
            {
                var alias = content.ContentType.Alias;

                if (alias == "uwbsProduct" || alias == "uwbsCategory")
                {
                    // Need to get this into function
                    var slug = content.GetValue<string>("slug");
                    var siblings = content.Parent().Children().Where(x => x.Published && x.Id != content.Id);

                    // Update Slug if Slug Exist on same Level and is Published
                    if (siblings.Any(x => x.GetValue<string>("slug").ToLowerInvariant() == slug.ToLowerInvariant()))
                    {

                        // Random not a nice solution
                        Random rnd = new Random();

                        slug = slug + "-" + rnd.Next(1, 150);

                        content.SetValue("slug", slug);

                        _log.Warn("Duplicate slug found for product : " + content.Id);

                        e.Messages.Add(new EventMessage("Duplicate Slug Found.", "Sorry but this slug is already in use, we updated it for you.", EventMessageType.Warning));
                    }

                    content.SetValue("slug", slug.ToUrlSegment());
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
    }
}
