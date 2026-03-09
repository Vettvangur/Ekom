using Ekom.Algolia.Indexing;
using Ekom.Algolia.Mappers;
using Ekom.Algolia.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using Algolia.Search.Clients;

namespace Ekom.Algolia;

public static class AlgoliaServiceCollectionExtensions
{
    public static IServiceCollection AddAlgolia(
        this IServiceCollection services,
        Action<AlgoliaOptions>? configure = null)
    {
        var ob = services.AddOptions<AlgoliaOptions>()
            .BindConfiguration("Ekom:Algolia");

        if (configure is not null)
            ob.Configure(configure);

        services.AddHttpContextAccessor();

        services.AddSingleton<ISearchClient>(sp =>
        {
            var opt = sp.GetRequiredService<IOptions<AlgoliaOptions>>().Value;
            return new SearchClient(opt.ApplicationId, opt.AdminApiKey);
        });

        services.AddHttpClient("AlgoliaInsights", (sp, client) =>
        {
            var opt = sp.GetRequiredService<IOptions<AlgoliaOptions>>().Value;
            var key = string.IsNullOrWhiteSpace(opt.InsightsApiKey) ? opt.AdminApiKey : opt.InsightsApiKey;

            client.BaseAddress = new Uri("https://insights.algolia.io/1/");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("X-Algolia-Application-Id", opt.ApplicationId);
            client.DefaultRequestHeaders.Add("X-Algolia-API-Key", key);
        });

        services.AddSingleton<IAlgoliaInsightsClient>(sp =>
        {
            var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("AlgoliaInsights");
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AlgoliaInsightsClient>>();
            return new AlgoliaInsightsClient(http, logger);
        });

        services.AddSingleton<IndexNameBuilder>();
        services.AddSingleton<IAlgoliaProductIndexMapper, ProductIndexMapper>();

        services.AddSingleton<IAlgoliaProductIndexQueue, AlgoliaProductIndexQueue>();
        services.AddSingleton<AlgoliaProductIndexExecutor>();
        services.AddSingleton<IAlgoliaProductIndexService, AlgoliaProductIndexService>();
        services.AddSingleton<AlgoliaProductIndexWorker>();
        services.AddHostedService(sp => sp.GetRequiredService<AlgoliaProductIndexWorker>());

        services.AddSingleton<IAlgoliaUserTokenProvider, DefaultAlgoliaUserTokenProvider>();
        services.AddSingleton<IAlgoliaEventService, AlgoliaEventService>();

        return services;
    }
}
