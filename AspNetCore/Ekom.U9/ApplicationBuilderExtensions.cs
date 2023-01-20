using Ekom.Exceptions;
using Ekom.Umb;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ekom.Core.Utilities
{
    public static class ApplicationBuilderExtensions
    {
        public static IServiceCollection AddEkom(this IServiceCollection services)
        {
            //composition.Components()
                //.Append<UmbracoStartup>()
                //;
            //composition.Sections().Append<UserManagementSection>();

            //var smtpSection = (SmtpSection)System.Configuration.ConfigurationManager.GetSection("system.net/mailSettings/smtp");

            //container.Register<IMailService>(
            //    fac => new MailService(
            //        fac.GetInstance<ILogFactory>(),
            //        smtpSection.From,
            //        fac.GetInstance<IContentSection>().NotificationEmailAddress)
            //);
            //container.Register<ILogFactory, LogFactory>();
            //container.Register<IUmbracoService, UmbracoService>();
            //container.Register<ISessionHelper, UmbracoSessionHelper>();
            //container.Register<IAuditService, AuditService>();
            //container.Register<IMemberService, MemberService>();
            //container.Register<IConfiguration>(fac => LegacyConfigurationProvider.Create());
            //container.Register<UserManagementConfig>();
            //container.Register<InitialSeeding>();
            //container.Register<IUserManagementRepository, UserManagementRepository>();
            //container.Register<UserManagementService>();
            //container.Register<UserManagementAuthorizationService>();
            //container.Register<IDatabaseFactory, DatabaseFactory>();
            services.AddMemoryCache();

            services.Configure<MvcOptions>(mvcOptions =>
            {
                mvcOptions.Filters.Add<HttpResponseExceptionFilter>();
            });

            return services;
        }

        public static IApplicationBuilder UseEkom(this IApplicationBuilder app)
        {
            Configuration.Resolver = app.ApplicationServices;

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    "Organisation Management Controller",
                    "api/{controller}/{action}/{id?}",
                    new { controller = "EkomApi" });
                endpoints.MapControllerRoute(
                    "Organisation Management Controller",
                    "api/{controller}/{action}/{id?}",
                    new { controller = "EkomCatalog" });
                endpoints.MapControllerRoute(
                    "Organisation Management Controller",
                    "api/{controller}/{action}/{id?}",
                    new { controller = "EkomOrder" });
            });

            app.UseMiddleware<EkomMiddleware>();

            return app;
        }
    }
}
