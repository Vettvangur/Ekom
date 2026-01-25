using Ekom.Klaviyo.Clients;
using Ekom.Klaviyo.Dispatching.Catalog;
using Ekom.Klaviyo.Dispatching.Orders;
using Ekom.Klaviyo.Enrichers.OrderEnricher;
using Ekom.Klaviyo.Enrichers.ProductEnricher;
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
        services.AddSingleton<IKlaviyoOrdersClient, KlaviyoOrdersClient>();

        // Dispatchers (singleton hosted services)
        services.AddSingleton<KlaviyoCatalogDispatcher>();
        services.AddSingleton<IKlaviyoCatalogDispatcher>(sp => sp.GetRequiredService<KlaviyoCatalogDispatcher>());
        services.AddHostedService(sp => sp.GetRequiredService<KlaviyoCatalogDispatcher>());

        services.AddSingleton<KlaviyoOrdersDispatcher>();
        services.AddSingleton<IKlaviyoOrdersDispatcher>(sp => sp.GetRequiredService<KlaviyoOrdersDispatcher>());
        services.AddHostedService(sp => sp.GetRequiredService<KlaviyoOrdersDispatcher   >());

        services.AddScoped<IKlaviyoOrderService, KlaviyoOrderService>();

        // Enrichers
        services.AddSingleton<KlaviyoProductEnrichmentPipeline>();
        services.AddSingleton<KlaviyoPlacedOrderEnrichmentPipeline>();
        services.AddSingleton<IKlaviyoPlacedOrderEnricherRunner, KlaviyoPlacedOrderEnricherRunner>();

        return services;
    }
}
