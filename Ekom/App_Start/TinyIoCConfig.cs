using CommonServiceLocator.TinyIoCAdapter;
using Ekom.API;
using Ekom.Cache;
using Ekom.Domain.Repositories;
using Ekom.Factories;
using Ekom.Interfaces;
using Ekom.IoC;
using Ekom.Models.Abstractions;
using Ekom.Models.Data;
using Ekom.Repository;
using Ekom.Services;
using Examine;
using System.Collections.Generic;
using System.Web;
using TinyIoC;
using Umbraco.Core;
using Umbraco.Core.Configuration;
using Umbraco.Core.Models;
using Umbraco.Web;
using Catalog = Ekom.API.Catalog;

namespace Ekom.App_Start
{
    /// <summary>
    /// Specifies the TinyIoC configuration for the main container.
    /// </summary>
    static class TinyIoCConfig
    {
        /// <summary>Registers the type mappings with the TinyIoC container.</summary>
        /// <param name="container">The TinyIoC container to configure.</param>
        public static void RegisterTypes(this TinyIoCContainer container)
        {
            container.Register<HttpContextBase>((c, p) => new HttpContextWrapper(HttpContext.Current));
            container.Register<HttpRequestBase>((c, p) => c.Resolve<HttpContextBase>().Request);
            container.Register<HttpResponseBase>((c, p) => c.Resolve<HttpContextBase>().Response);
            container.Register<HttpServerUtilityBase>((c, p) => c.Resolve<HttpContextBase>().Server);

            container.Register<ApplicationContext>((c, p) => ApplicationContext.Current);
            container.Register<ExamineManagerBase>((c, p) => new ExamineManagerWrapper(ExamineManager.Instance));
            container.Register<UmbracoConfig>((c, p) => UmbracoConfig.For);

            container.Register<UmbracoContext>((c, p) => UmbracoContext.Current);
            container.Register<UmbracoHelper>((c, p) => new UmbracoHelper(c.Resolve<UmbracoContext>()));

            container.Register<Configuration>().AsSingleton();

            container.Register<IBaseCache<IDomain>, StoreDomainCache>().AsSingleton();
            container.Register<IBaseCache<IStore>, StoreCache>().AsSingleton();
            container.Register<IPerStoreCache<IVariant>, VariantCache>().AsSingleton();
            container.Register<IPerStoreCache<IVariantGroup>, VariantGroupCache>().AsSingleton();
            container.Register<IPerStoreCache<ICategory>, CategoryCache>().AsSingleton();
            container.Register<IPerStoreCache<IProduct>, ProductCache>().AsSingleton();
            container.Register<IBaseCache<IZone>, ZoneCache>().AsSingleton();
            container.Register<IPerStoreCache<IPaymentProvider>, PaymentProviderCache>().AsSingleton();
            container.Register<IPerStoreCache<IShippingProvider>, ShippingProviderCache>().AsSingleton();
            container.Register<IBaseCache<StockData>, StockCache>().AsSingleton();
            container.Register<IPerStoreCache<StockData>, StockPerStoreCache>().AsSingleton();

            container.Register<IStoreService, StoreService>().AsMultiInstance();
            //container.Register<IOrderService, OrderService>();

            container.Register<ICountriesRepository, CountriesRepository>().AsMultiInstance();
            container.Register<IStockRepository, StockRepository>().AsMultiInstance();
            container.Register<IDiscountStockRepository, DiscountStockRepository>().AsMultiInstance();
            container.Register<IOrderRepository, OrderRepository>();

            container.Register<ILogFactory, LogFactory>();

            // What follows are explicit factory constructors for the API methods
            // This is needed since many of their dependencies are internal classes
            // However the API services are public, leaving their constructor public violates
            // C# visibility restrictions
            // Modifying the constructor to be internal hides it from the default TinyIoC discovery methods.
            container.Register<Catalog>((c, p) =>
                new Catalog(
                    c.Resolve<ApplicationContext>(),
                    c.Resolve<Configuration>(),
                    c.Resolve<ILogFactory>(),
                    c.Resolve<IPerStoreCache<IProduct>>(),
                    c.Resolve<IPerStoreCache<ICategory>>(),
                    c.Resolve<IPerStoreCache<IVariant>>(),
                    c.Resolve<IPerStoreCache<IVariantGroup>>(),
                    c.Resolve<IStoreService>()
                )
            );
            container.Register<Order>((c, p) =>
                new Order(
                    c.Resolve<Configuration>(),
                    c.Resolve<ILogFactory>(),
                    c.Resolve<DiscountCache>(),
                    c.Resolve<OrderService>(),
                    c.Resolve<CheckoutService>(),
                    c.Resolve<IStoreService>()
                )
            );
            container.Register<Providers>((c, p) =>
                new Providers(
                    c.Resolve<Configuration>(),
                    c.Resolve<ILogFactory>(),
                    c.Resolve<IPerStoreCache<IShippingProvider>>(),
                    c.Resolve<IPerStoreCache<IPaymentProvider>>(),
                    c.Resolve<IStoreService>(),
                    c.Resolve<ICountriesRepository>()
                )
            );
            container.Register<Stock>((c, p) =>
                new Stock(
                    c.Resolve<Configuration>(),
                    c.Resolve<ILogFactory>(),
                    c.Resolve<IBaseCache<StockData>>(),
                    c.Resolve<IStockRepository>(),
                    c.Resolve<IDiscountStockRepository>(),
                    c.Resolve<IStoreService>(),
                    c.Resolve<IPerStoreCache<StockData>>()
                )
            );
            container.Register<Store>((c, p) =>
                new Store(
                    c.Resolve<ApplicationContext>(),
                    c.Resolve<ILogFactory>(),
                    c.Resolve<IStoreService>()
                )
            );
            container.Register<Discounts>((c, p) =>
                 new Discounts(
                    c.Resolve<Configuration>(),
                    c.Resolve<ILogFactory>(),
                    c.Resolve<IPerStoreCache<IDiscount>>(),
                    c.Resolve<IStoreService>()
                 )
             );

            container.RegisterTypes(containerRegistrations);

            // Resolve last
            var discountCache = container.Resolve<DiscountCache>();
            container.Register<IPerStoreCache<IDiscount>, DiscountCache>(discountCache);
            container.Register<DiscountCache>(discountCache);
        }

        public static void RegisterTypes(
            this TinyIoCContainer container,
            IEnumerable<IContainerRegistration> typeOverrides)
        {
            foreach (var registration in typeOverrides)
            {
                if (registration is IActivatorContainerRegistration activatorRegistration)
                {
                    var registered = container.Register(registration.Type, (c, p) => activatorRegistration.Activator(new TinyIoCServiceLocator(c)));
                    SetLifetime(registered, registration);
                }
                else
                {
                    var registered = container.Register(registration.Type);
                    SetLifetime(registered, registration);
                }
            }
        }

        /// <summary>
        /// Not working for TinyIoC
        /// </summary>
        private static TinyIoCContainer.RegisterOptions SetLifetime(TinyIoCContainer.RegisterOptions options, IContainerRegistration reg)
        {
            //switch (reg.Lifetime)
            //{
            //    case Lifetime.Transient:
            //        return options.AsMultiInstance();

            //    case Lifetime.ExternallyOwned:
            //        return options.AsSingleton();

            //    case Lifetime.Request:
            //        return options.AsPerRequestSingleton();

            //    case Lifetime.Singleton:
            //        return options.AsSingleton();

            //    default:
            //        return options.AsMultiInstance();
            //}

            return options;
        }

        /// <summary>
        /// WIP - migrate all to here.
        /// Abstracts dependency on containers during registration
        /// </summary>
        internal static List<IContainerRegistration> containerRegistrations = new List<IContainerRegistration>
        {
            new ContainerRegistration<IObjectFactory<IStore>>(Lifetime.Transient, c => c.GetInstance<StoreFactory>()),
            new ContainerRegistration<IObjectFactory<IZone>>(Lifetime.Transient, c => c.GetInstance<ZoneFactory>()),
            new ContainerRegistration<IPerStoreFactory<ICategory>>(Lifetime.Transient, c => c.GetInstance<CategoryFactory>()),
            new ContainerRegistration<IPerStoreFactory<IDiscount>>(Lifetime.Transient, c => c.GetInstance<DiscountFactory>()),
            new ContainerRegistration<IPerStoreFactory<IPaymentProvider>>(Lifetime.Transient, c => c.GetInstance<PaymentProviderFactory>()),
            new ContainerRegistration<IPerStoreFactory<IShippingProvider>>(Lifetime.Transient, c => c.GetInstance<ShippingProviderFactory>()),
            new ContainerRegistration<IPerStoreFactory<IProduct>>(Lifetime.Transient, c => c.GetInstance<ProductFactory>()),
            new ContainerRegistration<IPerStoreFactory<IVariant>>(Lifetime.Transient, c => c.GetInstance<VariantFactory>()),
            new ContainerRegistration<IPerStoreFactory<IVariantGroup>>(Lifetime.Transient, c => c.GetInstance<VariantGroupFactory>()),
        };
    }
}
