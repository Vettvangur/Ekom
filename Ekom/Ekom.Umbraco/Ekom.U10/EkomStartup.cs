using Ekom.App_Start;
using Ekom.Cache;
using Ekom.Exceptions;
using Ekom.Payments;
using Ekom.Repositories;
using Ekom.Services;
using Ekom.Umb.Sections;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Web.BackOffice.Trees;

namespace Ekom.Umb;

class StartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, next) =>
        {
            context.Request.EnableBuffering();
            await next();
        });

        app.UseEkomMalformedFormGuard();
        app.UseSession();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseEkomMiddleware();
        app.UseEkomTrackingMiddleware();
        next(app);
    };
}

/// <summary>
/// Hooks into the umbraco application startup lifecycle 
/// </summary>
// Public allows consumers to target type with ComposeAfter / ComposeBefore

public class EkomComposer : IComposer
{
    public EkomComposer()
    {

    }
    /// <summary>
    /// Umbraco lifecycle method
    /// </summary>
    public void Compose(IUmbracoBuilder builder)
    {

        builder.ContentFinders()
            .InsertBefore<ContentFinderByPageIdQuery, CatalogContentFinder>();
        builder.UrlProviders()
            .Insert<CatalogUrlProvider>();

        builder.Components()
            // Can't use umbraco npoco for this since we use linq2db in core
            .Append<EnsureTablesExist>()
            .Append<EnsureNodesExist>()
            .Append<Ekom.Umb.Services.ExamineSearchIndexComponent>()
            .Append<EkomStartup>()
            ;

        builder.Sections().Append<ManagerSection>();

        // VirtualContent=true allows for configuration of content nodes to use for matching all requests
        // Use case: Ekom populated by adapter, used as in memory cache with no backing umbraco nodes

        var config = new Configuration(builder.Config);


        builder
            .AddNotificationAsyncHandler<ContentPublishedNotification, UmbracoEventListeners>()
            .AddNotificationAsyncHandler<ContentUnpublishedNotification, UmbracoEventListeners>()
            .AddNotificationAsyncHandler<ContentSavingNotification, UmbracoEventListeners>()
            .AddNotificationAsyncHandler<ContentDeletedNotification, UmbracoEventListeners>()
            .AddNotificationHandler<ContentMovedToRecycleBinNotification, UmbracoEventListeners>()
            .AddNotificationHandler<ContentMovedNotification, UmbracoEventListeners>()
            .AddNotificationHandler<DomainSavedNotification, UmbracoEventListeners>()
            .AddNotificationHandler<ServerVariablesParsingNotification, UmbracoEventListeners>()
            .AddNotificationHandler<DomainDeletedNotification, UmbracoEventListeners>()
            .AddNotificationHandler<LanguageCacheRefresherNotification, UmbracoEventListeners>();

        BackwardCompatabilityDeserialization();

        builder.Services.AddEkom(builder.Config);
    }

    private void BackwardCompatabilityDeserialization()
    {
        //Backward compatibility for deserialization
        EkomJsonDotNet.AddTypeMap(
            "Ekom.Models.OrderedObjects.OrderedDiscount, Ekom",
            typeof(Ekom.Models.OrderedDiscount));


        EkomJsonDotNet.AddTypeMapByName(
            "Ekom.Models.Behaviors.Constraints",
            typeof(Ekom.Models.Constraints));
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
    readonly ILogger _logger;
    readonly IServiceProvider _factory;
    readonly IMemoryCache _cache;
    readonly IRuntimeState _runtimeState;
    private readonly IOptions<RequestLocalizationOptions> _requestLocalizationOptions;

    /// <summary>
    /// 
    /// </summary>
    public EkomStartup(
        ILogger<EkomStartup> logger,
        IServiceProvider factory,
        IMemoryCache cache,
        IRuntimeState runtimeState,
        IOptions<RequestLocalizationOptions> requestLocalizationOptions)
    {
        _logger = logger;
        _factory = factory;
        _cache = cache;
        _runtimeState = runtimeState;
        _requestLocalizationOptions = requestLocalizationOptions;
    }

    /// <summary>
    /// Umbraco startup lifecycle method
    /// </summary>
    public void Initialize()
    {
        try
        {
            if (_runtimeState.Level < RuntimeLevel.Run)
            {
                // If Installing or Upgrading, we don't want to run this
                return;
            }

            _logger.LogInformation("Initializing...");

            Configuration.Resolver = _factory;
            PriceCache.SetCache(_cache);
            EkomCultureRequestLocalizationOptions.ConfigureCultures(
                _requestLocalizationOptions.Value,
                _factory.GetRequiredService<Ekom.Services.IUmbracoService>());

            var orderRepo = _factory.GetService<OrderRepository>();

            orderRepo?.MigrateOrderTableAsync();
            orderRepo?.MigrateStockToDecimalAsync();
            _factory.GetRequiredService<EkomCacheInitializer>().Initialize(false);

            Payments.Events.SuccessAsync += CompleteCheckoutAsync;

            _logger.LogInformation("Ekom Started");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ekom startup failed");
        }
    }

    private async Task CompleteCheckoutAsync(object sender, SuccessEventArgs args)
    {
        var o = args.OrderStatus;

        if (o.EkomPaymentSettings.OrderCustomData.TryGetValue("ekomOrderUniqueId", out var value))
        {
            var checkoutSvc = _factory.GetRequiredService<CheckoutService>();

            if (Guid.TryParse(value, out var orderId))
            {
                await checkoutSvc.CompleteAsync(orderId);
            }
        }
    }

    public void Terminate()
    {
        Payments.Events.SuccessAsync -= CompleteCheckoutAsync;
    }
}
