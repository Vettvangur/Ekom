using Ekom.Printer.Configuration;
using Ekom.Printer.Migrations;
using Ekom.Printer.Rendering;
using Ekom.Printer.Services;
using Ekom.Printer.Storage;
using Ekom.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Ekom.Printer;

public static class PrinterServiceCollectionExtensions
{
    public static IServiceCollection AddEkomPrinter(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PrinterOptions>().Bind(configuration.GetSection(PrinterOptions.SectionName)).ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<PrinterOptions>, PrinterOptionsValidator>());
        services.TryAddScoped<IPickingSlipHtmlRenderer, DefaultPickingSlipHtmlRenderer>();
        services.TryAddScoped<IPickingSlipContextFactory, PickingSlipContextFactory>();
        services.TryAddSingleton<PrinterMigration>();
        services.TryAddSingleton<PrinterJobStore>();
        services.TryAddSingleton<PrinterDeliveryStore>();
        services.TryAddSingleton<CapturedStatusStore>();
        services.TryAddScoped<IPickingSlipCapture, PickingSlipCapture>();
        services.AddAntiforgery();
        services.TryAddScoped<PrinterManagerService>();
        if (configuration.GetValue<bool>(PrinterOptions.SectionName + ":Enabled"))
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IOrderStatusPersistenceHandler, PrinterStatusCapture>());
        services.TryAddSingleton(TimeProvider.System);
        services.AddHostedService<PrinterWorker>();
        return services;
    }
}
