using Ekom.API;
using Ekom.Cache;
using Ekom.Domain.Repositories;
using Ekom.Factories;
using Ekom.Interfaces;
using Ekom.Models.Data;
using Ekom.Repository;
using Ekom.Services;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Composing;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Catalog = Ekom.API.Catalog;

namespace Ekom.App_Start
{
    /// <summary>
    /// Registers the Ekom type mappings with Umbraco IoC.
    /// </summary>
    static class Registrations
    {
        /// <summary>Registers the Ekom type mappings with Umbraco IoC.</summary>
        public static void Register(Composition composition)
        {
            composition.Register<Configuration>(Lifetime.Singleton);

            composition.Register<IStoreDomainCache, StoreDomainCache>(Lifetime.Singleton);
            composition.Register<IBaseCache<IStore>, StoreCache>(Lifetime.Singleton);
            composition.Register<IPerStoreCache<IVariant>, VariantCache>(Lifetime.Singleton);
            composition.Register<IPerStoreCache<IVariantGroup>, VariantGroupCache>(Lifetime.Singleton);
            composition.Register<IPerStoreCache<ICategory>, CategoryCache>(Lifetime.Singleton);
            composition.Register<IPerStoreCache<IProductDiscount>, ProductDiscountCache>(Lifetime.Singleton);
            composition.Register<IPerStoreCache<IProduct>, ProductCache>(Lifetime.Singleton);
            composition.Register<IBaseCache<IZone>, ZoneCache>(Lifetime.Singleton);
            composition.Register<IPerStoreCache<IPaymentProvider>, PaymentProviderCache>(Lifetime.Singleton);
            composition.Register<IPerStoreCache<IShippingProvider>, ShippingProviderCache>(Lifetime.Singleton);
            composition.Register<IBaseCache<StockData>, StockCache>(Lifetime.Singleton);
            composition.Register<IPerStoreCache<StockData>, StockPerStoreCache>(Lifetime.Singleton);

            // The following database based caches are not strictly related to the preceding ones
            composition.Register<ICouponCache, CouponCache>(Lifetime.Singleton);
            composition.Register<DiscountCache>(Lifetime.Singleton);
            composition.Register<IPerStoreCache<IDiscount>>(f => f.GetInstance<DiscountCache>()); // Lifetime based on preceding line

            composition.Register<IStoreService, StoreService>(Lifetime.Transient);
            composition.Register<OrderService>(Lifetime.Transient);
            composition.Register<ExamineService>(Lifetime.Transient);
            composition.Register<CheckoutService>(Lifetime.Transient);

            composition.Register<ICountriesRepository, CountriesRepository>(Lifetime.Transient);
            composition.Register<IStockRepository, StockRepository>(Lifetime.Transient);
            composition.Register<IDiscountStockRepository, DiscountStockRepository>(Lifetime.Transient);

            composition.Register<IOrderRepository, OrderRepository>(Lifetime.Transient);
            composition.Register<ICouponRepository, CouponRepository>(Lifetime.Transient);
            composition.Register<IActivityLogRepository, ActivityLogRepository>(Lifetime.Transient);

            // What follows are explicit factory constructors for the API methods
            // This is needed since many of their dependencies are internal classes
            // However the API services are public, leaving their constructor public violates
            // C# visibility restrictions
            composition.Register<Catalog>(f =>
                new Catalog(
                    f.GetInstance<ILogger>(),
                    f.GetInstance<AppCaches>(),
                    f.GetInstance<Configuration>(),
                    f.GetInstance<IPerStoreCache<IProduct>>(),
                    f.GetInstance<IPerStoreCache<ICategory>>(),
                    f.GetInstance<IPerStoreCache<IProductDiscount>>(),
                    f.GetInstance<IPerStoreCache<IVariant>>(),
                    f.GetInstance<IPerStoreCache<IVariantGroup>>(),
                    f.GetInstance<IStoreService>()
                )
            );
            composition.Register<IProductDiscountService>(f =>
                new ProductDiscountService(
                    f.GetInstance<IPerStoreCache<IProductDiscount>>()
                )
            );

            composition.Register<Order>(f =>
                new Order(
                    f.GetInstance<Configuration>(),
                    f.GetInstance<ILogger>(),
                    f.GetInstance<DiscountCache>(),
                    f.GetInstance<ICouponCache>(),
                    f.GetInstance<OrderService>(),
                    f.GetInstance<CheckoutService>(),
                    f.GetInstance<IStoreService>()
                )
            );
            composition.Register<Providers>(f =>
                new Providers(
                    f.GetInstance<Configuration>(),
                    f.GetInstance<ILogger>(),
                    f.GetInstance<IPerStoreCache<IShippingProvider>>(),
                    f.GetInstance<IPerStoreCache<IPaymentProvider>>(),
                    f.GetInstance<IStoreService>(),
                    f.GetInstance<ICountriesRepository>()
                )
            );
            composition.Register<Stock>(f =>
                new Stock(
                    f.GetInstance<Configuration>(),
                    f.GetInstance<ILogger>(),
                    f.GetInstance<IBaseCache<StockData>>(),
                    f.GetInstance<IStockRepository>(),
                    f.GetInstance<IDiscountStockRepository>(),
                    f.GetInstance<IStoreService>(),
                    f.GetInstance<IPerStoreCache<StockData>>()
                )
            );
            composition.Register<Store>(f =>
                new Store(
                    f.GetInstance<AppCaches>(),
                    f.GetInstance<ILogger>(),
                    f.GetInstance<IStoreService>()
                )
            );
            composition.Register<Discounts>(f =>
                 new Discounts(
                    f.GetInstance<Configuration>(),
                    f.GetInstance<ILogger>(),
                    f.GetInstance<IPerStoreCache<IDiscount>>(),
                    f.GetInstance<IStoreService>()
                 )
             );

            composition.Register<IObjectFactory<IStore>, StoreFactory>(Lifetime.Transient);
            composition.Register<IObjectFactory<IZone>, ZoneFactory>(Lifetime.Transient);
            composition.Register<IPerStoreFactory<ICategory>, CategoryFactory>(Lifetime.Transient);
            composition.Register<IPerStoreFactory<IDiscount>, DiscountFactory>(Lifetime.Transient);
            composition.Register<IPerStoreFactory<IPaymentProvider>, PaymentProviderFactory>(Lifetime.Transient);
            composition.Register<IPerStoreFactory<IShippingProvider>, ShippingProviderFactory>(Lifetime.Transient);
            composition.Register<IPerStoreFactory<IProduct>, ProductFactory>(Lifetime.Transient);
            composition.Register<IPerStoreFactory<IProductDiscount>, ProductDiscountFactory>(Lifetime.Transient);
            composition.Register<IPerStoreFactory<IVariant>, VariantFactory>(Lifetime.Transient);
            composition.Register<IPerStoreFactory<IVariantGroup>, VariantGroupFactory>(Lifetime.Transient);
        }
    }
}
