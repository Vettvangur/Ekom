using Ekom.App_Start;
using Ekom.Cache;
using Ekom.Interfaces;
using Ekom.Models.Data;
using Hangfire;
using Umbraco.Core;
using Umbraco.Core.Composing;
using Umbraco.Core.Logging;
using Umbraco.Core.Persistence;
using Umbraco.Core.Services.Implement;
using Umbraco.Web;
using Umbraco.Web.Routing;

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
            Registrations.Register(composition);

            composition.ContentFinders()
                .InsertBefore<ContentFinderByPageIdQuery, CatalogContentFinder>();
            composition.UrlProviders()
                .InsertBefore<DefaultUrlProvider, CatalogUrlProvider>();
            composition.Components()
                .Append<EnsureTablesExist>()
                .Append<EnsureNodesExist>()
                .Append<EkomStartup>()
                ;
        }
    }

#pragma warning disable CA1001 // Types that own disposable fields should be disposable
    /// <summary>
    /// Here we hook into the umbraco lifecycle methods to configure Ekom.
    /// We use ApplicationEventHandler so that these lifecycle methods are only run
    /// when umbraco is in a stable condition.
    /// </summary>
    class EkomStartup : IComponent
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
    {
        readonly Configuration _config;
        readonly ILogger _logger;
        readonly IFactory _factory;
        readonly IUmbracoDatabaseFactory _databaseFactory;

        BackgroundJobServer _hangfireServer;

        /// <summary>
        /// 
        /// </summary>
        public EkomStartup(
            Configuration config,
            ILogger logger,
            IFactory factory,
            IUmbracoDatabaseFactory databaseFactory)
        {
            _config = config;
            _logger = logger;
            _factory = factory;
            _databaseFactory = databaseFactory;
        }

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
                ? _factory.GetInstance<IPerStoreCache<StockData>>()
                : _factory.GetInstance<IBaseCache<StockData>>()
                as ICache;

            stockCache.FillCache();

            _factory.GetInstance<ICouponCache>()
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
                ContentService.Saving += umbEvListeners.ContentService_Saving;
                ContentService.Moved += umbEvListeners.ContentService_Moved;
                DomainService.Saved += umbEvListeners.DomainSaved;
                DomainService.Deleted += umbEvListeners.DomainDeleted;
            }

            // Hangfire
            GlobalConfiguration.Configuration.UseSqlServerStorage(_databaseFactory.ConnectionString);
            // ReSharper disable once ObjectCreationAsStatement
            _hangfireServer = new BackgroundJobServer();

            _logger.Info<EkomStartup>("Ekom Started");
        }

        public void Terminate()
        {
            _hangfireServer.Dispose();
        }
    }
}
