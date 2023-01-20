using Ekom;
using LightInject.Microsoft.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core;
using Umbraco.Core.Composing;

namespace Ekom.U8
{
    class StartupComposer : IUserComposer
    {
        /// <summary>
        /// Umbraco lifecycle method
        /// </summary>
        public void Compose(Composition composition)
        {
            composition.Components()
                //.Append<UmbracoStartup>()
                ;
            //composition.Sections().Append<UserManagementSection>();

            var container = composition.Concrete as LightInject.ServiceContainer;

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
            container.Register<IMemoryCache, MemoryCache>();
            Configuration.Resolver = container.CreateServiceProvider();
        }
    }

}
