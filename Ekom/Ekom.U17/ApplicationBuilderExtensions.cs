using Ekom.AspNetCore;
using Ekom.Services;
using Ekom.Umb.Services;
using EkomCore.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ekom.Umb;

public static class ApplicationBuilderExtensions
{
    public static IServiceCollection AddEkom(this IServiceCollection services, IConfiguration config)
    {
        services.AddDistributedMemoryCache();
        services.AddSession();
        services.AddAspNetCoreEkom(config);
        services.AddHttpClient();

        services.AddTransient<IMemberService, MemberService>();
        services.AddTransient<INodeService, NodeService>();
        services.AddTransient<IImportService, ImportService>();
        services.AddTransient<IMetafieldService, MetafieldService>();
        services.AddTransient<IUmbracoService, UmbracoService>();
        services.AddTransient<IUrlService, UrlService>();
        services.AddScoped<BackofficeUserAccessor>();
        services.AddScoped<ISecurityService, SecurityService>();
        services.AddScoped<IManagerAccessService, ManagerAccessService>();
        services.AddScoped<ICatalogSearchService, CatalogSearchService>();

        return services;
    }
}
