using Ekom.API;
using Ekom.AspNetCore.Services;
using Ekom.Cache;
using Ekom.Domain.Repositories;
using Ekom.Exceptions;
using Ekom.Factories;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Repositories;
using Ekom.Services;
using EkomCore.Services;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vettvangur.OrganisationManagement.AspNetCore;

namespace Ekom.AspNetCore
{
    static class Registrations
    {
        public static IServiceCollection AddAspNetCoreEkom(this IServiceCollection services)
        {
            services.AddAuthorization(options =>
            {
                options.AddPolicy(
                    "UmbracoUser",
                    policy => policy.Requirements.Add(new UmbracoUserAuthorization())
                );
            });

            services.AddSingleton<Configuration>();
            services.AddSingleton<IStartupFilter, EkomAspNetCoreStartupFilter>();
            services.AddSingleton<IAuthorizationHandler, UmbracoUserAuthorizationHandler>();

            services.AddSingleton<IStoreDomainCache, StoreDomainCache>();
            services.AddSingleton<IBaseCache<IStore>, StoreCache>();
            services.AddSingleton<IPerStoreCache<IVariant>, VariantCache>();
            services.AddSingleton<IPerStoreCache<IVariantGroup>, VariantGroupCache>();
            services.AddSingleton<IPerStoreCache<ICategory>, CategoryCache>();
            services.AddSingleton<IPerStoreCache<IProductDiscount>, ProductDiscountCache>();
            services.AddSingleton<IPerStoreCache<IProduct>, ProductCache>();
            services.AddSingleton<IBaseCache<IZone>, ZoneCache>();
            services.AddSingleton<IPerStoreCache<IPaymentProvider>, PaymentProviderCache>();
            services.AddSingleton<IPerStoreCache<IShippingProvider>, ShippingProviderCache>();
            services.AddSingleton<IBaseCache<StockData>, StockCache>();
            services.AddSingleton<IPerStoreCache<StockData>, StockPerStoreCache>();

            // The following database based caches are not strictly related to the preceding ones
            services.AddSingleton<ICouponCache, CouponCache>();
            services.AddSingleton<DiscountCache>();
            services.AddSingleton<IPerStoreCache<IDiscount>>(f => f.GetService<DiscountCache>()); // Lifetime based on preceding line


            services.AddTransient<IStoreService, StoreService>();
            services.AddTransient<OrderService>();
            services.AddTransient<CheckoutService>();
            services.AddTransient<IMailService, MailService>();
            services.AddTransient<DatabaseService>();

            services.AddTransient<CountriesRepository>();
            services.AddTransient<StockRepository>();
            services.AddTransient<DiscountStockRepository>();

            services.AddTransient<OrderRepository>();
            services.AddTransient<CouponRepository>();
            services.AddTransient<ActivityLogRepository>();

            services.AddSingleton<IObjectFactory<IStore>, StoreFactory>();
            services.AddSingleton<IObjectFactory<IZone>, ZoneFactory>();
            services.AddSingleton<IPerStoreFactory<ICategory>, CategoryFactory>();
            services.AddSingleton<IPerStoreFactory<IDiscount>, DiscountFactory>();
            services.AddSingleton<IPerStoreFactory<IPaymentProvider>, PaymentProviderFactory>();
            services.AddSingleton<IPerStoreFactory<IShippingProvider>, ShippingProviderFactory>();
            services.AddSingleton<IPerStoreFactory<IProduct>, ProductFactory>();
            services.AddSingleton<IPerStoreFactory<IProductDiscount>, ProductDiscountFactory>();
            services.AddSingleton<IPerStoreFactory<IVariant>, VariantFactory>();
            services.AddSingleton<IPerStoreFactory<IVariantGroup>, VariantGroupFactory>();

            // What follows are explicit factory constructors for the API methods
            // This is needed since many of their dependencies are internal classes
            // However the API services are public, leaving their constructor public violates
            // C# visibility restrictions
            services.AddTransient<Catalog>(f =>
                new Catalog(
                    f.GetService<ILogger<Catalog>>(),
                    f.GetService<Configuration>(),
                    f.GetService<IMetafieldService>(),
                    f.GetService<IPerStoreCache<IProduct>>(),
                    f.GetService<IPerStoreCache<ICategory>>(),
                    f.GetService<IPerStoreCache<IProductDiscount>>(),
                    f.GetService<IPerStoreCache<IVariant>>(),
                    f.GetService<IPerStoreCache<IVariantGroup>>(),
                    f.GetService<IStoreService>(),
                    f.GetService<IHttpContextAccessor>()
                )
            );
            services.AddTransient<ProductDiscountService>(f =>
                new ProductDiscountService(
                    f.GetService<IPerStoreCache<IProductDiscount>>()
                )
            );

            services.AddTransient<Order>(f =>
                new Order(
                    f.GetService<Configuration>(),
                    f.GetService<ILogger<Order>>(),
                    f.GetService<DiscountCache>(),
                    f.GetService<ICouponCache>(),
                    f.GetService<OrderService>(),
                    f.GetService<CheckoutService>(),
                    f.GetService<IStoreService>()
                )
            );
            services.AddTransient<Providers>(f =>
                new Providers(
                    f.GetService<Configuration>(),
                    f.GetService<ILogger<Providers>>(),
                    f.GetService<IPerStoreCache<IShippingProvider>>(),
                    f.GetService<IPerStoreCache<IPaymentProvider>>(),
                    f.GetService<IBaseCache<IZone>>(),
                    f.GetService<IStoreService>(),
                    f.GetService<CountriesRepository>()
                )
            );
            services.AddTransient<Stock>(f =>
                new Stock(
                    f.GetService<Configuration>(),
                    f.GetService<ILogger<Stock>>(),
                    f.GetService<IBaseCache<StockData>>(),
                    f.GetService<StockRepository>(),
                    f.GetService<DiscountStockRepository>(),
                    f.GetService<IStoreService>(),
                    f.GetService<IPerStoreCache<StockData>>()
                )
            );
            services.AddTransient<Ekom.API.Store>(f =>
                new Ekom.API.Store(
                    f.GetService<ILogger<Ekom.API.Store>>(),
                    f.GetService<IStoreService>(),
                    f.GetService<Configuration>(),
                    f.GetService<INodeService>()
                )
            );
            services.AddTransient<Discounts>(f =>
                 new Discounts(
                    f.GetService<Configuration>(),
                    f.GetService<ILogger<Discounts>>(),
                    f.GetService<IPerStoreCache<IDiscount>>(),
                    f.GetService<IStoreService>()
                 )
             );

            services.AddSingleton<DatabaseFactory>();

            services.AddHttpContextAccessor();
            //services.AddMemoryCache();

            services.Configure<MvcOptions>(mvcOptions =>
            {
                mvcOptions.Filters.Add<HttpResponseExceptionFilter>();
            });
            
            services.AddHangfire(config =>
            {
                config.UseSqlServerStorage("umbracoDbDSN");
            });

            return services;
        }
    }
}
