using Ekom.AspNetCore;
using Ekom.Services;
using Ekom.Tracking;
using Ekom.Umb.Services;
using Ekom.Umb.VariantApp.Services;
using EkomCore.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ekom.Umb;

public static class ApplicationBuilderExtensions
{
    public static IServiceCollection AddEkom(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<IStartupFilter, StartupFilter>();

        services.AddDistributedMemoryCache();
        services.AddSession();
        services.AddAspNetCoreEkom(config);
        services.AddHttpClient();

        services.AddTransient<IMemberService, MemberService>();
        services.AddSingleton<Umbraco17ContentCache>();
        services.AddTransient<INodeService, NodeService>();
        services.AddTransient<IImportService, ImportService>();
        services.AddTransient<ImportMediaService>();
        services.AddTransient<NodeService>();
        services.AddTransient<IMetafieldService, MetafieldService>();
        services.AddTransient<IUmbracoService, UmbracoService>();
        services.AddTransient<IVariantAppService, VariantAppService>();
        services.AddTransient<IUrlService, UrlService>();
        services.AddScoped<BackofficeUserAccessor>();
        services.AddScoped<ISecurityService, SecurityService>();
        services.AddScoped<IManagerAccessService, ManagerAccessService>();
        services.AddScoped<ICatalogSearchService, CatalogSearchService>();

        return services;
    }

    public static IApplicationBuilder UseEkomMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<EkomMiddleware>();
    }

    public static IApplicationBuilder UseEkomTrackingMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<EkomTrackingMiddleware>();
    }

    public static IApplicationBuilder UseEkomMalformedFormGuard(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<EkomMalformedFormGuardMiddleware>();
    }
}
