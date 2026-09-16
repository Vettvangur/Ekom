using Ekom.Algolia.Indexing;
using Ekom.Algolia.Mappers;
using Ekom.Algolia.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        services.AddMemoryCache();
        services.AddHttpClient<IAlgoliaQuerySuggestionsConfigurator, AlgoliaQuerySuggestionsConfigurator>();

        services.AddSingleton<ISearchClient>(sp =>
        {
            var opt = sp.GetRequiredService<IOptions<AlgoliaOptions>>().Value;
            if (!opt.Enabled || !opt.Stores.Any(store => store.Collections.Enabled))
                return new SearchClient(opt.ApplicationId, opt.AdminApiKey);

            var region = ResolveTransformationRegion(opt.TransformationRegion);

            return SearchClient.WithTransformation(
                opt.ApplicationId,
                opt.AdminApiKey,
                new TransformationOptions(region),
                sp.GetRequiredService<ILoggerFactory>());
        });

        services.AddSingleton<IAlgoliaQueryClient, AlgoliaQueryClient>();

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
        services.AddSingleton<ContentIndexNameResolver>();
        services.AddSingleton<AlgoliaStoreResolver>();
        services.AddSingleton<AlgoliaIndexReplacementService>();
        services.AddSingleton<AlgoliaSearchCacheVersionProvider>();
        services.AddSingleton<AlgoliaSearchCacheKeyBuilder>();
        services.AddSingleton<IAlgoliaProductIndexMapper, ProductIndexMapper>();
        services.AddSingleton<IAlgoliaCategoryIndexMapper, CategoryIndexMapper>();
        services.AddSingleton<AlgoliaAvailabilityUpdateService>();

        services.AddSingleton<IAlgoliaProductIndexQueue, AlgoliaProductIndexQueue>();
        services.AddSingleton<AlgoliaProductIndexExecutor>();
        services.AddSingleton<IAlgoliaProductIndexService, AlgoliaProductIndexService>();
        services.AddSingleton<IAlgoliaFullIndexRebuildCoordinator, AlgoliaFullIndexRebuildCoordinator>();
        services.AddSingleton<AlgoliaProductIndexWorker>();
        services.AddHostedService(sp => sp.GetRequiredService<AlgoliaProductIndexWorker>());

        services.AddSingleton<IAlgoliaCategoryIndexQueue, AlgoliaCategoryIndexQueue>();
        services.AddSingleton<AlgoliaCategoryIndexExecutor>();
        services.AddSingleton<IAlgoliaCategoryIndexService, AlgoliaCategoryIndexService>();
        services.AddSingleton<AlgoliaCategoryIndexWorker>();
        services.AddHostedService(sp => sp.GetRequiredService<AlgoliaCategoryIndexWorker>());

        services.AddSingleton<IAlgoliaContentIndexQueue, AlgoliaContentIndexQueue>();
        services.AddSingleton<AlgoliaContentIndexExecutor>();
        services.AddSingleton<IAlgoliaContentIndexService, AlgoliaContentIndexService>();
        services.AddSingleton<AlgoliaContentIndexWorker>();
        services.AddHostedService(sp => sp.GetRequiredService<AlgoliaContentIndexWorker>());

        services.AddSingleton<IAlgoliaUserTokenProvider, DefaultAlgoliaUserTokenProvider>();
        services.AddSingleton<IAlgoliaEventService, AlgoliaEventService>();
        services.AddSingleton<IAlgoliaSearchService, AlgoliaSearchService>();

        return services;
    }

    internal static string ResolveTransformationRegion(string? region)
    {
        var normalized = string.IsNullOrWhiteSpace(region) ? "eu" : region.Trim();

        if (normalized.Equals("eu", StringComparison.OrdinalIgnoreCase))
            return "eu";
        if (normalized.Equals("us", StringComparison.OrdinalIgnoreCase))
            return "us";

        throw new InvalidOperationException("Algolia TransformationRegion must be 'eu' or 'us' when Collections are enabled.");
    }
}
