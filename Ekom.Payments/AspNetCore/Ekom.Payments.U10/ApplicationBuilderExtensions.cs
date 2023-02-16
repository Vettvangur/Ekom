using Ekom.Payments.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Serialization;

namespace Ekom.Payments.Umb;

static class ApplicationBuilderExtensions
{
    public static IServiceCollection AddEkomPayments(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IStartupFilter, EkomPaymentsStartupFilter>();

        services.AddAspNetCoreEkomPayments(configuration);

        services.AddTransient<IUmbracoService, UmbracoService>();

        return services;
    }
}
