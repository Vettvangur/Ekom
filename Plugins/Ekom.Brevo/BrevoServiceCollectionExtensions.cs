using Ekom.Brevo.Dispatching;
using Ekom.Brevo.Http;
using Ekom.Brevo.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using System.Net.Http;

namespace Ekom.Brevo;

public static class BrevoServiceCollectionExtensions
{
    public static IServiceCollection AddBrevo(this IServiceCollection services, Action<BrevoOptions>? configure = null)
    {
        var options = services.AddOptions<BrevoOptions>().BindConfiguration("Ekom:Brevo").ValidateOnStart();
        if (configure != null) options.Configure(configure);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<BrevoOptions>, BrevoOptionsValidator>());
        services.AddHttpClient("Ekom.Brevo", (sp, client) =>
        {
            client.BaseAddress = sp.GetRequiredService<IOptions<BrevoOptions>>().Value.ApiBaseUrl;
            client.Timeout = TimeSpan.FromSeconds(30);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
            .RemoveAllLoggers();
        services.TryAddSingleton<BrevoConfiguration>();
        services.TryAddSingleton<BrevoHttpClient>();
        services.TryAddSingleton<BrevoMarketingDispatcher>();
        services.TryAddSingleton<BrevoTransactionalDispatcher>();
        services.AddHostedService(sp => sp.GetRequiredService<BrevoMarketingDispatcher>());
        services.AddHostedService(sp => sp.GetRequiredService<BrevoTransactionalDispatcher>());
        services.TryAddSingleton<IBrevoContactsService, BrevoContactsService>();
        services.TryAddSingleton<IBrevoOrderService, BrevoOrderService>();
        services.TryAddSingleton<IBrevoTransactionalService, BrevoTransactionalService>();
        return services;
    }
}
