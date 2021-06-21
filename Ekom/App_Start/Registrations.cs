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
    // Public allows consumers to target type with ComposeAfter / ComposeBefore
    public class EkomRegistrations : ICoreComposer
    {
        /// <summary>Registers the Ekom type mappings with Umbraco IoC.</summary>
        public void Compose(Composition composition)
        {
            composition.RegisterUnique<Configuration>();

            composition.RegisterUnique<IStoreDomainCache, StoreDomainCache>();
            composition.RegisterUnique<IBaseCache<IStore>, StoreCache>();
            composition.RegisterUnique<IPerStoreCache<IVariant>, VariantCache>();
            composition.RegisterUnique<IPerStoreCache<IVariantGroup>, VariantGroupCache>();
            composition.RegisterUnique<IPerStoreCache<ICategory>, CategoryCache>();
            composition.RegisterUnique<IPerStoreCache<IProductDiscount>, ProductDiscountCache>();
            composition.RegisterUnique<IPerStoreCache<IProduct>, ProductCache>();
            composition.RegisterUnique<IBaseCache<IZone>, ZoneCache>();
            composition.RegisterUnique<IPerStoreCache<IPaymentProvider>, PaymentProviderCache>();
            composition.RegisterUnique<IPerStoreCache<IShippingProvider>, ShippingProviderCache>();
            composition.RegisterUnique<IBaseCache<StockData>, StockCache>();
            composition.RegisterUnique<IPerStoreCache<StockData>, StockPerStoreCache>();

            // The following database based caches are not strictly related to the preceding ones
            composition.RegisterUnique<ICouponCache, CouponCache>();
            composition.RegisterUnique<DiscountCache>();
            composition.RegisterUnique<IPerStoreCache<IDiscount>>(f => f.GetInstance<DiscountCache>()); // Lifetime based on preceding line

            composition.Register<IStoreService, StoreService>(Lifetime.Transient);
            composition.Register<OrderService>(Lifetime.Transient);
            composition.Register<IExamineService, ExamineService>(Lifetime.Transient);
            composition.Register<CheckoutService>(Lifetime.Transient);
            composition.Register<MailService>(Lifetime.Transient);

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
                    f.GetInstance<IBaseCache<IZone>>(),
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
                    f.GetInstance<IStoreService>(),
                    f.GetInstance<Configuration>()
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

            composition.RegisterUnique<IObjectFactory<IStore>, StoreFactory>();
            composition.RegisterUnique<IObjectFactory<IZone>, ZoneFactory>();
            composition.RegisterUnique<IPerStoreFactory<ICategory>, CategoryFactory>();
            composition.RegisterUnique<IPerStoreFactory<IDiscount>, DiscountFactory>();
            composition.RegisterUnique<IPerStoreFactory<IPaymentProvider>, PaymentProviderFactory>();
            composition.RegisterUnique<IPerStoreFactory<IShippingProvider>, ShippingProviderFactory>();
            composition.RegisterUnique<IPerStoreFactory<IProduct>, ProductFactory>();
            composition.RegisterUnique<IPerStoreFactory<IProductDiscount>, ProductDiscountFactory>();
            composition.RegisterUnique<IPerStoreFactory<IVariant>, VariantFactory>();
            composition.RegisterUnique<IPerStoreFactory<IVariantGroup>, VariantGroupFactory>();
        }
    }
}
