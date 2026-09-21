using Ekom.Mailchimp.Clients;
using Ekom.Mailchimp.Dispatching;
using Ekom.Mailchimp.Http;
using Ekom.Mailchimp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ekom.Mailchimp;

public static class MailchimpServiceCollectionExtensions
{
    public static IServiceCollection AddMailchimp(
        this IServiceCollection services,
        Action<MailchimpOptions>? configure = null)
    {
        OptionsBuilder<MailchimpOptions> options = services.AddOptions<MailchimpOptions>()
            .BindConfiguration("Ekom:Mailchimp");
        if (configure != null)
        {
            options.Configure(configure);
        }

        options.ValidateOnStart();
        services.AddSingleton<IValidateOptions<MailchimpOptions>, MailchimpOptionsValidator>();
        services.AddMemoryCache();
        services.AddHttpClient("Ekom.Mailchimp", client => client.Timeout = TimeSpan.FromSeconds(30));
        services.AddHttpClient("Ekom.Mailchimp.Transactional", client =>
        {
            client.BaseAddress = new Uri("https://mandrillapp.com/api/1.0/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddSingleton<MailchimpHttpClient>();

        services.AddSingleton<IMailchimpConfigurationResolver, MailchimpConfigurationResolver>();
        services.AddSingleton<IMailchimpTransactionalConfigurationResolver, MailchimpTransactionalConfigurationResolver>();
        services.AddHostedService<MailchimpConfigurationStartupService>();
        services.AddSingleton<IMailchimpAudienceClient, MailchimpAudienceClient>();
        services.AddSingleton<IMailchimpEcommerceClient, MailchimpEcommerceClient>();
        services.AddSingleton<IMailchimpTransactionalClient, MailchimpTransactionalClient>();

        services.AddSingleton<MailchimpDispatcher>();
        services.AddSingleton<IMailchimpDispatcher>(sp => sp.GetRequiredService<MailchimpDispatcher>());
        services.AddHostedService(sp => sp.GetRequiredService<MailchimpDispatcher>());
        services.AddSingleton<MailchimpTransactionalDispatcher>();
        services.AddSingleton<IMailchimpTransactionalDispatcher>(sp =>
            sp.GetRequiredService<MailchimpTransactionalDispatcher>());
        services.AddHostedService(sp => sp.GetRequiredService<MailchimpTransactionalDispatcher>());
        services.AddScoped<IMailchimpService, MailchimpService>();
        services.AddScoped<IMailchimpTransactionalService, MailchimpTransactionalService>();

        return services;
    }
}
