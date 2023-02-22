using LinqToDB.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ekom.Payments.AspNetCore;

static class ApplicationBuilderExtensions
{
    public static IServiceCollection AddAspNetCoreEkomPayments(this IServiceCollection services, IConfiguration configuration)
    {
        var config = new PaymentsConfiguration();
        configuration.Bind("Ekom:Payments", config);
        
        services.AddSingleton(config);
        services.AddSingleton<IDatabaseFactory, DatabaseFactory>();

        services.AddTransient<EnsureTablesExist>();
        services.AddTransient<IOrderService, OrderService>();
        services.AddTransient<EkomPayments>();

        var cmsSection = configuration.GetSection("Umbraco:CMS");
        var smtpSection = cmsSection.GetSection("Global:Smtp");

        services.AddTransient<IMailService>(
            sp => new MailService(
                sp.GetRequiredService<ILogger<MailService>>(),
                smtpSection["Host"],
                smtpSection["Port"],
                smtpSection["Username"],
                smtpSection["Password"],
                smtpSection["From"],
                cmsSection["Content:Notifications:Email"])
        );

        return services;

    }

    public static IApplicationBuilder UseEkomPaymentsControllers(this IApplicationBuilder app)
    {
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });

        return app;
    }
}
