using Ekom.Klaviyo.Clients;
using Ekom.Klaviyo.Dispatching.Catalog;
using Ekom.Klaviyo.Dispatching.Orders;
using Ekom.Klaviyo.Dispatching.Subscriptions;
using Ekom.Klaviyo.Dispatching.Tracking;
using Ekom.Klaviyo.Enrichers.TrackingEnricher;
using Ekom.Klaviyo.Enrichers.OrderEnricher;
using Ekom.Klaviyo.Enrichers.ProductEnricher;
using Ekom.Klaviyo.Enrichers.SubscriptionsEnricher;
using Ekom.Klaviyo.Http;
using Ekom.Klaviyo.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace Ekom.Klaviyo;

public static class KlaviyoServiceCollectionExtensions
{
    public static IServiceCollection AddKlaviyo(
        this IServiceCollection services,
        Action<KlaviyoOptions>? configure = null)
    {
        var ob = services.AddOptions<KlaviyoOptions>()
            .BindConfiguration("Ekom:Klaviyo");

        if (configure is not null)
            ob.Configure(configure);

        services.AddSingleton<IPostConfigureOptions<KlaviyoOptions>, KlaviyoOptionsPostConfigure>();

        services.AddHttpClient("Klaviyo", (sp, client) =>
        {
            var opt = sp.GetRequiredService<IOptions<KlaviyoOptions>>().Value;

            client.BaseAddress = new Uri(opt.ApiBaseUrl);
            client.DefaultRequestHeaders.Add("revision", opt.Revision);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });

        services.AddSingleton<KlaviyoHttpClient>(sp =>
        {
            var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("Klaviyo");
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<KlaviyoHttpClient>>();
            var resolver = sp.GetRequiredService<IKlaviyoApiKeyResolver>();
            return new KlaviyoHttpClient(http, resolver, logger);
        });

        services.AddSingleton<IKlaviyoApiKeyResolver, KlaviyoApiKeyResolver>();

        services.AddSingleton<IKlaviyoCatalogClient, KlaviyoCatalogClient>();
        services.AddSingleton<IKlaviyoListsClient, KlaviyoListsClient>();
        services.AddSingleton<IKlaviyoOrdersClient, KlaviyoOrdersClient>();
        services.AddSingleton<IKlaviyoProfilesClient, KlaviyoProfilesClient>();
        services.AddSingleton<IKlaviyoSubscriptionsClient, KlaviyoSubscriptionsClient>();
        services.AddSingleton<IKlaviyoTrackingClient, KlaviyoTrackingClient>();

        // Dispatchers (singleton hosted services)
        services.AddSingleton<KlaviyoCatalogDispatcher>();
        services.AddSingleton<IKlaviyoCatalogDispatcher>(sp => sp.GetRequiredService<KlaviyoCatalogDispatcher>());
        services.AddHostedService(sp => sp.GetRequiredService<KlaviyoCatalogDispatcher>());

        services.AddSingleton<KlaviyoOrdersDispatcher>();
        services.AddSingleton<IKlaviyoOrdersDispatcher>(sp => sp.GetRequiredService<KlaviyoOrdersDispatcher>());
        services.AddHostedService(sp => sp.GetRequiredService<KlaviyoOrdersDispatcher>());

        services.AddSingleton<KlaviyoSubscriptionsDispatcher>();
        services.AddSingleton<IKlaviyoSubscriptionsDispatcher>(sp => sp.GetRequiredService<KlaviyoSubscriptionsDispatcher>());
        services.AddHostedService(sp => sp.GetRequiredService<KlaviyoSubscriptionsDispatcher>());

        services.AddSingleton<KlaviyoTrackingDispatcher>();
        services.AddSingleton<IKlaviyoTrackingDispatcher>(sp => sp.GetRequiredService<KlaviyoTrackingDispatcher>());
        services.AddHostedService(sp => sp.GetRequiredService<KlaviyoTrackingDispatcher>());

        services.AddScoped<IKlaviyoOrderService, KlaviyoOrderService>();
        services.AddScoped<IKlaviyoProfilesService, KlaviyoProfilesService>();
        services.AddScoped<IKlaviyoSubscriptionsService, KlaviyoSubscriptionsService>();
        services.AddScoped<IKlaviyoTrackingService, KlaviyoTrackingService>();

        // Enrichers
        services.AddSingleton<KlaviyoProductEnrichmentPipeline>();
        services.AddSingleton<KlaviyoSubscriptionsEnrichmentPipeline>();
        services.AddSingleton<KlaviyoPlacedOrderEnrichmentPipeline>();

        services.AddSingleton<IKlaviyoPlacedOrderEnricherRunner, KlaviyoPlacedOrderEnricherRunner>();
        services.AddSingleton<IKlaviyoSubscriptionsEnricherRunner, KlaviyoSubscriptionsEnricherRunner>();

        services.AddSingleton<KlaviyoTrackingEnrichmentPipeline>();
        services.AddSingleton<IKlaviyoTrackingEnricherRunner, KlaviyoTrackingEnricherRunner>();

        return services;
    }
}
