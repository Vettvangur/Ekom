using Examine;
using System.IO.Abstractions;
using System.Web;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Composing;
using Umbraco.Core.Logging;
using Umbraco.Core.Scoping;
using Umbraco.Core.Services;
using Ekom.Payments.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace Ekom.Payments.AspNetCore;

/// <summary>
/// Specifies the DI configuration.
/// </summary>
static class Registrations
{
    public static IServiceCollection AddEkomPayments(this IServiceCollection services, IConfiguration configuration)
    {
        var config = new PaymentsConfiguration();
        configuration.Bind("Ekom:Payments", config);
        services.AddSingleton(config);
        services.AddSingleton<IDatabaseFactory, DatabaseFactory>();

        services.AddTransient<IOrderService, OrderService>();
        services.AddTransient<EkomPayments>();
        services.Register<IUmbracoService, UmbracoServiceOld>();
        services.Register<IMailService, MailService>();
    }
}
