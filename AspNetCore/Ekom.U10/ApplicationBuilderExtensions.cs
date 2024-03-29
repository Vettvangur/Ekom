using Ekom.AspNetCore;
using Ekom.Services;
using Ekom.Umb.Services;
using EkomCore.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Serialization;
using System.Text.Json;

namespace Ekom.Umb;

static class ApplicationBuilderExtensions
{
    public static IServiceCollection AddEkom(this IServiceCollection services)
    {
        services.AddSingleton<IStartupFilter, StartupFilter>();

        services.AddAspNetCoreEkom();

        services.AddControllers()
            .AddNewtonsoftJson(option => 
            option.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver()
            )
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;

            });

        services.AddTransient<IMemberService, MemberService>();
        services.AddTransient<INodeService, NodeService>();
        services.AddTransient<IImportService, ImportService>();
        services.AddTransient<NodeService>();
        services.AddTransient<IMetafieldService, MetafieldService>();
        services.AddTransient<IUmbracoService, UmbracoService>();
        services.AddTransient<IUrlService, UrlService>();
        services.AddScoped<BackofficeUserAccessor>();
        services.AddScoped<ISecurityService, SecurityService>();
        services.AddScoped<ICatalogSearchService, CatalogSearchService>();

        return services;
    }

    public static IApplicationBuilder UseEkomMiddleware(this IApplicationBuilder builder)
    {

        return builder.UseMiddleware<EkomMiddleware>();
    }
}
