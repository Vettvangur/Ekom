using System;
using Microsoft.Practices.Unity;
using Microsoft.Practices.Unity.Configuration;
using uWebshop.Cache;
using uWebshop.Models;
using Umbraco.Core.Models;
using Umbraco.Core;
using Examine;
using System.Web;

namespace uWebshop.App_Start
{
    /// <summary>
    /// Specifies the Unity configuration for the main container.
    /// </summary>
    public class UnityConfig
    {
        #region Unity Container
        private static Lazy<IUnityContainer> container = new Lazy<IUnityContainer>(() =>
        {
            var container = new UnityContainer();
            RegisterTypes(container);
            return container;
        });

        /// <summary>
        /// Gets the configured Unity container.
        /// </summary>
        public static IUnityContainer GetConfiguredContainer()
        {
            return container.Value;
        }
        #endregion

        /// <summary>Registers the type mappings with the Unity container.</summary>
        /// <param name="container">The unity container to configure.</param>
        /// <remarks>There is no need to register concrete types such as controllers or API controllers (unless you want to 
        /// change the defaults), as Unity allows resolving a concrete type even if it was not previously registered.</remarks>
        public static void RegisterTypes(IUnityContainer container)
        {
            container.RegisterTypes(
                AllClasses.FromAssemblies(typeof(UnityConfig).Assembly),
                WithMappings.FromMatchingInterface,
                WithName.Default
            );

            // NOTE: To load from web.config uncomment the line below. Make sure to add a Microsoft.Practices.Unity.Configuration to the using statements.
            // container.LoadConfiguration();

            // TODO: Register your types here

            container.RegisterType<HttpContextBase>(new InjectionFactory(c => new HttpContextWrapper(HttpContext.Current)));

            container.RegisterType<ApplicationContext>(new InjectionFactory(c => ApplicationContext.Current));
            container.RegisterType<DatabaseContext>(new InjectionFactory(c => c.Resolve<ApplicationContext>().DatabaseContext));
            container.RegisterType<ExamineManager>(new InjectionFactory(c => ExamineManager.Instance));

            container.RegisterType<Configuration>(new ContainerControlledLifetimeManager());

            container.RegisterType<IBaseCache<IDomain>, StoreDomainCache>(new ContainerControlledLifetimeManager());
            container.RegisterType<IBaseCache<Store>, StoreCache>(new ContainerControlledLifetimeManager());
            container.RegisterType<IPerStoreCache<Variant>, VariantCache>(new ContainerControlledLifetimeManager());
            container.RegisterType<IPerStoreCache<VariantGroup> , VariantGroupCache>(new ContainerControlledLifetimeManager());
            container.RegisterType<IPerStoreCache<Category>, CategoryCache>(new ContainerControlledLifetimeManager());
            container.RegisterType<IPerStoreCache<Product>, ProductCache>(new ContainerControlledLifetimeManager());
            container.RegisterType<IBaseCache<Zone>, ZoneCache>(new ContainerControlledLifetimeManager());
            container.RegisterType<IPerStoreCache<PaymentProvider>, PaymentProviderCache>(new ContainerControlledLifetimeManager());
        }
    }
}