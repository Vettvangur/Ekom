using Ekom.App_Start;
using Ekom.Cache;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Umb.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.WebAssets;
using Umbraco.Cms.Web.BackOffice.Trees;

namespace Ekom.Umb;

class StartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.UseEkomMiddleware();

        next(app);
    };
}

/// <summary>
/// Hooks into the umbraco application startup lifecycle 
/// </summary>
// Public allows consumers to target type with ComposeAfter / ComposeBefore
public class EkomComposer : IComposer
{
    /// <summary>
    /// Umbraco lifecycle method
    /// </summary>
    public void Compose(IUmbracoBuilder builder)
    {

        builder.ContentFinders()
            .InsertBefore<ContentFinderByPageIdQuery, CatalogContentFinder>();
        builder.UrlProviders()
            .InsertBefore<DefaultUrlProvider, CatalogUrlProvider>();

        builder.Components()
            // Can't use umbraco npoco for this since we use linq2db in core
            .Append<EnsureTablesExist>()
            .Append<EnsureNodesExist>()
            .Append<EkomStartup>()
            ;


        // VirtualContent=true allows for configuration of content nodes to use for matching all requests
        // Use case: Ekom populated by adapter, used as in memory cache with no backing umbraco nodes

        var config = new Configuration(builder.Config);

        if (!config.VirtualContent)
        {
            builder
                .AddNotificationHandler<ContentPublishedNotification, UmbracoEventListeners>()
                .AddNotificationHandler<ContentUnpublishedNotification, UmbracoEventListeners>()
                .AddNotificationHandler<ContentSavingNotification, UmbracoEventListeners>()
                .AddNotificationHandler<ContentDeletedNotification, UmbracoEventListeners>()
                .AddNotificationHandler<ContentMovedToRecycleBinNotification, UmbracoEventListeners>()
                .AddNotificationHandler<ContentMovedNotification, UmbracoEventListeners>()
                .AddNotificationHandler<DomainSavedNotification, UmbracoEventListeners>()
                .AddNotificationHandler<ServerVariablesParsingNotification, UmbracoEventListeners>()
                .AddNotificationHandler<DomainDeletedNotification, UmbracoEventListeners>()
                .AddNotificationHandler<LanguageCacheRefresherNotification, UmbracoEventListeners>();
        }

        builder.Services.AddEkom();
    }
}
//[RuntimeLevelAttribute(MinLevel = RuntimeLevel.Run)]
public class RemoveCoreMemberSearchableTreeComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.SearchableTrees().Exclude<ContentTreeController>();
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
    readonly IServiceProvider _factory;
    readonly ExamineService _es;

    /// <summary>
    /// 
    /// </summary>
    public EkomStartup(
        Configuration config,
        ILogger<EkomStartup> logger,
        IServiceProvider factory,
        IUmbracoDatabaseFactory databaseFactory,
        ExamineService es,
        ServerVariablesParser svp)
    {
        _config = config;
        _logger = logger;
        _factory = factory;
        _es = es;
    }

    /// <summary>
    /// Umbraco startup lifecycle method
    /// </summary>
    public void Initialize()
    {
        try
        {
            _logger.LogInformation("Initializing...");

            Configuration.Resolver = _factory;

            if (_config.ExamineRebuild)
            {
                _es.Rebuild();
            }

            // Fill Caches
            foreach (var cacheEntry in _config.CacheList.Value)
            {
                cacheEntry.FillCache();
            }

            // FIX: To override the default stock cache register before EkomStartup

            // The following two caches are not closely related to the ones listed in _config.CacheList
            // They should not be added to the config list since that list is used by f.x. _config.Succeed in many caches

            // Controls which stock cache will be populated
            var stockCache = _config.PerStoreStock
                ? _factory.GetService<IPerStoreCache<StockData>>()
                : _factory.GetService<IBaseCache<StockData>>()
                as ICache;

            stockCache.FillCache();

            _factory.GetService<ICouponCache>()
                .FillCache(); ;

            _logger.LogInformation("Ekom Started");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ekom startup failed");
        }
    }

    public void Terminate()
    {
    }
}
