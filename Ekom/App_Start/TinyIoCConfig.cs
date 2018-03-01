using CommonServiceLocator.TinyIoCAdapter;
using Ekom.Cache;
using Ekom.Domain.Repositories;
using Ekom.Interfaces;
using Ekom.IoC;
using Ekom.Models;
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
            container.Register<IBaseCache<Store>, StoreCache>().AsSingleton();
            container.Register<IPerStoreCache<IVariant>, VariantCache>().AsSingleton();
            container.Register<IPerStoreCache<IVariantGroup>, VariantGroupCache>().AsSingleton();
            container.Register<IPerStoreCache<ICategory>, CategoryCache>().AsSingleton();
            container.Register<IPerStoreCache<IProduct>, ProductCache>().AsSingleton();
            container.Register<IBaseCache<Zone>, ZoneCache>().AsSingleton();
            container.Register<IPerStoreCache<IPaymentProvider>, PaymentProviderCache>().AsSingleton();
            container.Register<IPerStoreCache<IShippingProvider>, ShippingProviderCache>().AsSingleton();
            container.Register<IBaseCache<StockData>, StockCache>().AsSingleton();
            container.Register<IPerStoreCache<StockData>, StockPerStoreCache>().AsSingleton();

            container.Register<IStoreService, StoreService>().AsMultiInstance();
            //container.Register<IOrderService, OrderService>();

            container.Register<ICountriesRepository, CountriesRepository>().AsMultiInstance();
            container.Register<IStockRepository, StockRepository>().AsMultiInstance();
            container.Register<IDiscountStockRepository, DiscountStockRepository>().AsMultiInstance();

            container.Register<ILogFactory, LogFactory>();

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

        private static TinyIoCContainer.RegisterOptions SetLifetime(TinyIoCContainer.RegisterOptions options, IContainerRegistration reg)
        {
            return reg.Lifetime == Lifetime.Transient
                ? options.AsMultiInstance()
                : reg.Lifetime == Lifetime.ExternallyOwned
                    ? options.AsSingleton()
                    : reg.Lifetime == Lifetime.Request
                        ? options.AsPerRequestSingleton()
                        : options.AsMultiInstance();
        }

        /// <summary>
        /// Perhaps one day...
        /// </summary>
        internal static List<IContainerRegistration> containerRegistrations = new List<IContainerRegistration>
        {
        };
    }
}
