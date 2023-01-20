using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using Umbraco.Cms.Web.Common.ApplicationBuilder;
using Umbraco.Extensions;

namespace Vettvangur.Shared
{
    public class VettvangurBuilderConfig
    {
        /// <summary>
        /// Configure Cors with AllowAny
        /// </summary>
        public bool SimpleCorsSetup { get; set; }

        public Action<IApplicationBuilder>? CorsConfiguration { get; set; }

        public Action<IUmbracoApplicationBuilderContext>? MiddlewareConfiguration { get; set; }
        public Action<IUmbracoEndpointBuilderContext>? EndpointsConfiguration { get; set; }

        /// <summary>
        /// Customize everything, overrides any custom cors/middlware/endpoints config <br />
        /// Always prefer the other customization options if possible
        /// </summary>
        public Action<IApplicationBuilder>? CustomUmbracoSetup { get; set; }
    }

    public static class AppBuilderExtensions
    {
        public static IApplicationBuilder UseVettvangurDefaults(
            this IApplicationBuilder app,
            IWebHostEnvironment env,
            IConfiguration config,
            VettvangurBuilderConfig? vvConfig = null)
        {
            if (vvConfig == null)
            {
                vvConfig = new VettvangurBuilderConfig();
            }

            app.UseResponseCompression();

            if (!env.IsProduction())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            var maxAge = config.GetSection("Cache").GetValue<long?>("MaxAge")
                ?? 356 * 24 * 60 * 60;


            app.Use(async (context, next) =>
            {
                context.Response.Headers.Add("Cache-Control", "public; max-age=" + maxAge);
                context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
                context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
                context.Response.Headers.Add("X-Frame-Options", "sameorigin");
                context.Response.Headers.Add("Referrer-Policy", "origin-when-cross-origin, strict-origin-when-cross-origin");
                await next.Invoke();
            });

            if (vvConfig.CustomUmbracoSetup == null)
            {
                app.UseUmbraco()
                    .WithMiddleware(u =>
                    {
                        u.UseBackOffice();
                        u.UseWebsite();

                        if (vvConfig.SimpleCorsSetup)
                        {
                            app.UseCors(builder => builder
                                .AllowAnyOrigin()
                                .AllowAnyMethod()
                                .AllowAnyHeader());
                        }
                        else
                        {
                            vvConfig.CorsConfiguration?.Invoke(app);
                        }
                    })
                    .WithEndpoints(u =>
                    {
                        vvConfig.EndpointsConfiguration?.Invoke(u);

                        u.UseInstallerEndpoints();
                        u.UseBackOfficeEndpoints();
                        u.UseWebsiteEndpoints();
                    });
            }
            else
            {
                vvConfig.CustomUmbracoSetup.Invoke(app);
            }

            return app;
        }
    }
}
