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
        services.AddHttpClient("Ekom.Mailchimp", client => client.Timeout = TimeSpan.FromSeconds(30));
        services.AddSingleton<MailchimpHttpClient>();

        services.AddSingleton<IMailchimpConfigurationResolver, MailchimpConfigurationResolver>();
        services.AddSingleton<IMailchimpAudienceClient, MailchimpAudienceClient>();
        services.AddSingleton<IMailchimpEcommerceClient, MailchimpEcommerceClient>();

        services.AddSingleton<MailchimpDispatcher>();
        services.AddSingleton<IMailchimpDispatcher>(sp => sp.GetRequiredService<MailchimpDispatcher>());
        services.AddHostedService(sp => sp.GetRequiredService<MailchimpDispatcher>());
        services.AddScoped<IMailchimpService, MailchimpService>();

        return services;
    }
}
