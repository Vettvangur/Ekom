using Ekom.Payments.ValitorPay;
using LinqToDB.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vettvangur.ValitorPay;

namespace Ekom.Payments.ValitorPay;

public static class ApplicationBuilderExtensions
{
    public static IServiceCollection AddEkomValitorPay(this IServiceCollection services, IConfiguration configuration)
    {
        var config = new PaymentsConfiguration();
        configuration.Bind("Ekom:Payments", config);
        
        services.AddSingleton(config);
        services.AddSingleton<IDatabaseFactory, DatabaseFactory>();

        services.AddTransient<EnsureValitorPayTablesExist>();
        services.AddTransient<VirtualCardService>();

        var apiUrl = new Uri(configuration["Ekom:Payments:ValitorPay:ApiUrl"]);
        var apiKey = configuration["Ekom:Payments:ValitorPay:ApiKey"];

        services.AddValitorPay(apiUrl, apiKey);

        return services;
    }

    public static IApplicationBuilder UseEkomValitorPay(this IApplicationBuilder app, IConfiguration configuration)
    {
        var ensureTablesExist
            = app.ApplicationServices.GetRequiredService<EnsureValitorPayTablesExist>();

        ensureTablesExist.Create();

        var virtualCardService
            = app.ApplicationServices.GetRequiredService<VirtualCardService>();

        Ekom.Payments.ValitorPay.Events.InitialPaymentSuccess
            += (sender, e) => Events_Success(virtualCardService, sender, e);

        return app;
    }

    private static void Events_Success(
        VirtualCardService virtualCardService,
        object? sender,
        SuccessEventArgs e)
    {
        virtualCardService.SaveVirtualCardAsync(e.OrderStatus).Wait();
    }
}
