using Ekom.Klaviyo.Dispatching.Catalog;
using Ekom.Klaviyo.Dispatching.Events;
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
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Klaviyo-API-Key", opt.PrivateApiKey);

            client.DefaultRequestHeaders.Add("revision", opt.Revision);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });

        services.AddSingleton<KlaviyoHttpClient>(sp =>
        {
            var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("Klaviyo");
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<KlaviyoHttpClient>>();
            return new KlaviyoHttpClient(http, logger);
        });

        services.AddSingleton<IKlaviyoCatalogClient, KlaviyoCatalogClient>();
        services.AddSingleton<IKlaviyoEventsClient, KlaviyoEventsClient>();

        // Dispatchers (singleton hosted services)
        services.AddSingleton<KlaviyoCatalogDispatcher>();
        services.AddSingleton<IKlaviyoCatalogDispatcher>(sp => sp.GetRequiredService<KlaviyoCatalogDispatcher>());
        services.AddHostedService(sp => sp.GetRequiredService<KlaviyoCatalogDispatcher>());

        services.AddSingleton<KlaviyoEventsDispatcher>();
        services.AddSingleton<IKlaviyoEventsDispatcher>(sp => sp.GetRequiredService<KlaviyoEventsDispatcher>());
        services.AddHostedService(sp => sp.GetRequiredService<KlaviyoEventsDispatcher>());

        services.AddScoped<IKlaviyoEventService, KlaviyoEventService>();
        services.AddScoped<IKlaviyoOrderService, KlaviyoOrderService>();

        // Enrichers
        services.AddSingleton<KlaviyoProductEnrichmentPipeline>();

        return services;
    }
}
