using Ekom.Klaviyo.Enrichers.ProductEnricher;
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
			.BindConfiguration("Klaviyo");

		if (configure is not null) ob.Configure(configure);

        services.AddSingleton<IPostConfigureOptions<KlaviyoOptions>, KlaviyoOptionsPostConfigure>();

        services.AddHttpClient<IKlaviyoClient, KlaviyoClient>((sp, client) =>
        {
            var opt = sp.GetRequiredService<IOptions<KlaviyoOptions>>().Value;

            client.BaseAddress = new Uri(opt.ApiBaseUrl);
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Klaviyo-API-Key", opt.PrivateApiKey);

            client.DefaultRequestHeaders.Add("revision", opt.Revision);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });


        services.AddSingleton<IKlaviyoProductDispatcher, KlaviyoProductBatchingDispatcher>();
        services.AddHostedService(sp => (KlaviyoProductBatchingDispatcher)sp.GetRequiredService<IKlaviyoProductDispatcher>());

        // Enrichers
        services.AddSingleton<KlaviyoProductEnrichmentPipeline>();

        return services;
	}
}
