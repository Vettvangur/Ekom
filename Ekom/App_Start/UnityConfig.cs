using Ekom.Cache;
using Ekom.Domain.Repositories;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Models.Abstractions;
using Ekom.Models.Data;
using Ekom.Models.Discounts;
using Ekom.Repository;
using Ekom.Services;
using Examine;
using System;
using System.Web;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Web;
using Unity;
using Unity.Injection;
using Unity.Lifetime;
using Unity.ServiceLocation;

namespace Ekom.App_Start
{
    /// <summary>
    /// Specifies the Unity configuration for the main container.
    /// </summary>
    class UnityConfig
    {
        #region Unity Container
        private static Lazy<IUnityContainer> container = new Lazy<IUnityContainer>(() =>
        {
            var container = new UnityContainer();
            RegisterTypes(container);
            Configuration.container = new UnityServiceLocator(container);
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
            container.RegisterType<HttpContextBase>(new InjectionFactory(c => new HttpContextWrapper(HttpContext.Current)));

            container.RegisterType<ApplicationContext>(new InjectionFactory(c => ApplicationContext.Current));
            container.RegisterType<UmbracoContext>(new InjectionFactory(c => UmbracoContext.Current));
            container.RegisterType<ExamineManagerBase>(new InjectionFactory(c => new ExamineManagerWrapper(ExamineManager.Instance)));

            container.RegisterType<Configuration>(new ContainerControlledLifetimeManager());

            container.RegisterType<UmbracoHelper>(new InjectionConstructor(typeof(UmbracoContext)));

            container.RegisterType<IBaseCache<IDomain>, StoreDomainCache>(new ContainerControlledLifetimeManager());
            container.RegisterType<IBaseCache<Store>, StoreCache>(new ContainerControlledLifetimeManager());
            container.RegisterType<IPerStoreCache<Variant>, VariantCache>(new ContainerControlledLifetimeManager());
            container.RegisterType<IPerStoreCache<VariantGroup>, VariantGroupCache>(new ContainerControlledLifetimeManager());
            container.RegisterType<IPerStoreCache<Category>, CategoryCache>(new ContainerControlledLifetimeManager());
            container.RegisterType<IPerStoreCache<Product>, ProductCache>(new ContainerControlledLifetimeManager());
            container.RegisterType<IBaseCache<Zone>, ZoneCache>(new ContainerControlledLifetimeManager());
            container.RegisterType<IPerStoreCache<PaymentProvider>, PaymentProviderCache>(new ContainerControlledLifetimeManager());
            container.RegisterType<IPerStoreCache<ShippingProvider>, ShippingProviderCache>(new ContainerControlledLifetimeManager());
            container.RegisterType<IBaseCache<StockData>, StockCache>(new ContainerControlledLifetimeManager());
            container.RegisterType<IPerStoreCache<StockData>, StockPerStoreCache>(new ContainerControlledLifetimeManager());
            container.RegisterType<IPerStoreCache<Discount>, DiscountCache>(new ContainerControlledLifetimeManager());

            container.RegisterType<IStoreService, StoreService>();
            //container.RegisterType<IOrderService, OrderService>();

            container.RegisterType<ICountriesRepository, CountriesRepository>();
            container.RegisterType<IStockRepository, StockRepository>();
            container.RegisterType<IDiscountStockRepository, DiscountStockRepository>();

            container.RegisterType<ILogFactory, LogFactory>();
        }
    }
}
