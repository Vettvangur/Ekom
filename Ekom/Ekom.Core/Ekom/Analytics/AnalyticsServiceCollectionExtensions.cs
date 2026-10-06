using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Ekom.Analytics;

public static class AnalyticsServiceCollectionExtensions
{
    public static IServiceCollection AddEkomAnalytics(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthorization(options => options.AddPolicy(AnalyticsAuthorization.Policy,
            policy => policy.RequireAssertion(AnalyticsAuthorization.HasBackofficeUser)));
        services.AddOptions<AnalyticsOptions>().Configure<ILogger<AnalyticsOptions>>((options, logger) =>
        {
            try
            {
                configuration.GetSection("Ekom:Analytics").Bind(options);
                if (!options.IsValid(out var error))
                {
                    options.Enabled = false;
                    logger.LogError("Analytics configuration is invalid; processing is disabled. {Reason}", error);
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or FormatException)
            {
                options.Enabled = false;
                logger.LogError(ex, "Analytics configuration could not be read; processing is disabled.");
            }
        });
        services.TryAddSingleton<IAnalyticsCustomerIdentityResolver, ConfiguredAnalyticsCustomerIdentityResolver>();
        services.TryAddSingleton<AnalyticsSnapshotMapper>();
        services.TryAddSingleton<AnalyticsSchema>();
        services.TryAddSingleton<AnalyticsProjectionService>();
        services.TryAddSingleton<AnalyticsJobService>();
        services.TryAddTransient<AnalyticsReportRepository>();
        services.AddHostedService<AnalyticsWorker>();
        return services;
    }
}
