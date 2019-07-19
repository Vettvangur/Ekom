using Ekom.App_Start;
using Ekom.Cache;
using Ekom.Interfaces;
using Ekom.Models.Data;
using Ekom.Services;
using Ekom.Utilities;
using Hangfire;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TinyIoC;
using Umbraco.Core;
using Umbraco.Core.Composing;
using Umbraco.Core.Events;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Persistence;
using Umbraco.Core.Services;
using Umbraco.Web.Routing;
using Umbraco.Core;
using Umbraco.Web;
using Umbraco.Core.Composing;
using Umbraco.Core.Events;
using Umbraco.Core.Models;
using Umbraco.Core.Services;
using Umbraco.Core.Services.Implement;

namespace Ekom
{
    /// <summary>
    /// Hooks into the umbraco application startup lifecycle 
    /// </summary>
    class Composer : IUserComposer
    {
        /// <summary>
        /// Umbraco lifecycle method
        /// </summary>
        public void Compose(Composition composition)
        {
            composition.ContentFinders()
                .InsertBefore<ContentFinderByPageIdQuery, CatalogContentFinder>();
            composition.UrlProviders()
                .InsertBefore<DefaultUrlProvider, CatalogUrlProvider>();
            composition.Components()
                .Append<EnsureTablesExist>()
                .Append<EkomStartup>()
                ;
        }
    }

    /// <summary>
    /// Here we hook into the umbraco lifecycle methods to configure Ekom.
    /// We use ApplicationEventHandler so that these lifecycle methods are only run
    /// when umbraco is in a stable condition.
    /// </summary>
    class EkomStartup : IComponent
    {
        readonly Configuration _config;
        readonly ILogger _logger;
        readonly IFactory _factory;
        readonly IUmbracoDatabase _umbracoDb;

        /// <summary>
        /// Umbraco startup lifecycle method
        /// </summary>
        public void Initialize()
        {
            _logger.Info<EkomStartup>("Initializing...");

            // FIX: To override the default stock cache register before EkomStartup

            // The following two caches are not closely related to the ones listed in _config.CacheList
            // They should not be added to the config list since that list is used by f.x. _config.Succeed in many caches

            // Controls which stock cache will be populated
            var stockCache = _config.PerStoreStock
                ? _factory.CreateInstance<IPerStoreCache<StockData>>()
                : _factory.CreateInstance<IBaseCache<StockData>>()
                as ICache;

            stockCache.FillCache();

            _factory.CreateInstance<ICouponCache>()
                .FillCache();

            // Fill Caches
            foreach (var cacheEntry in _config.CacheList.Value)
            {
                cacheEntry.FillCache();
            }

            // VirtualContent=true allows for configuration of content nodes to use for matching all requests
            // Use case: Ekom populated by adapter, used as in memory cache with no backing umbraco nodes

            if (!_config.VirtualContent)
            {
                var umbEvListeners = _factory.CreateInstance<UmbracoEventListeners>();
                // Hook into Umbraco Events
                ContentService.Published += umbEvListeners.ContentService_Published;
                ContentService.Unpublished += umbEvListeners.ContentService_UnPublished;
                ContentService.Deleted += umbEvListeners.ContentService_Deleted;
                ContentService.Publishing += umbEvListeners.ContentService_Publishing;
                ContentService.Moved += umbEvListeners.ContentService_Moved;
            }

            // Hangfire
            GlobalConfiguration.Configuration.UseSqlServerStorage(_umbracoDb.ConnectionString);
            // ReSharper disable once ObjectCreationAsStatement
            new BackgroundJobServer();

            _logger.Info<EkomStartup>("Ekom Started");
        }

        public void Terminate() { }
    }
}
