using Ekom.API;
using Ekom.AspNetCore.Services;
using Ekom.Cache;
using Ekom.Events;
using Ekom.Exceptions;
using Ekom.Factories;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Payments;
using Ekom.Repositories;
using Ekom.Services;
using Ekom.Tracking;
using Ekom.Utilities;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.RateLimiting;

namespace Ekom.AspNetCore;

static class Registrations
{
    public static IServiceCollection AddAspNetCoreEkom(this IServiceCollection services, IConfiguration config)
    {
        services.ConfigureOptions<EkomCultureRequestLocalizationOptions>();

        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                "UmbracoUser",
                policy => policy.Requirements.Add(new UmbracoUserAuthorization())
            );
        });

        services.AddSingleton(sp => new Configuration(config));
        services.AddSingleton<IStartupFilter, EkomAspNetCoreStartupFilter>();
        services.AddSingleton<IAuthorizationHandler, UmbracoUserAuthorizationHandler>();
        services.AddSingleton<PaymentsConfiguration>();

        services.AddSingleton<IStoreDomainCache, StoreDomainCache>();
        services.AddSingleton<IBaseCache<IStore>, StoreCache>();
        services.AddSingleton<IPerStoreIndexedCache<IVariant>, VariantCache>();
        services.AddSingleton<IPerStoreIndexedCache<IVariantGroup>, VariantGroupCache>();
        services.AddSingleton<IPerStoreIndexedCache<ICategory>, CategoryCache>();
        services.AddSingleton<IPerStoreCache<IProductDiscount>, ProductDiscountCache>();
        services.AddSingleton<DiscountEvents>();
        services.AddSingleton<IPerStoreIndexedCache<IProduct>, ProductCache>();
        services.AddSingleton<IBaseCache<IZone>, ZoneCache>();
        services.AddSingleton<IPerStoreCache<Models.IPaymentProvider>, PaymentProviderCache>();
        services.AddSingleton<IPerStoreCache<IShippingProvider>, ShippingProviderCache>();
        services.AddSingleton<IBaseCache<StockData>, StockCache>();
        services.AddSingleton<IPerStoreCache<StockData>, StockPerStoreCache>();

        // The following database based caches are not strictly related to the preceding ones
        services.AddSingleton<ICouponCache, CouponCache>();
        services.AddSingleton<DiscountCache>();
        services.AddSingleton<IPerStoreCache<IDiscount>>(f => f.GetService<DiscountCache>()); // Lifetime based on preceding line

        services.AddScoped<ApiExceptionFilter>();

        services.AddTransient<IStoreService, StoreService>();
        services.AddSingleton<OrderDiscountCalculationContextAccessor>();

        services.AddTransient<OrderService>();
        services.AddTransient<IOrderDiscountCalculationService>(f =>
            new OrderDiscountCalculationService(
                f.GetRequiredService<Catalog>(),
                f.GetRequiredService<ICouponCache>(),
                f.GetRequiredService<DiscountCache>(),
                f.GetRequiredService<INodeService>(),
                f.GetRequiredService<IStoreService>(),
                f.GetRequiredService<OrderDiscountCalculationContextAccessor>()
            )
        );
        services.AddScoped<RevalidateService>();
        services.AddScoped<ControllerRequestHelper>();
        services.AddTransient<CheckoutService>();
        services.AddSingleton<ITrackingConsentResolver, CookieHubTrackingConsentResolver>();
        services.AddSingleton<ITrackingConsentResolver, DefaultTrackingConsentResolver>();
        services.AddSingleton<ITrackingConsentService, TrackingConsentService>();
        services.AddSingleton<ITrackingCookieService, TrackingCookieService>();
        services.AddSingleton<IPreConsentTrackingSessionService, PreConsentTrackingSessionService>();
        services.AddTransient<IOrderTrackingService, OrderTrackingService>();
        services.AddTransient<IGa4TrackingService, Ga4TrackingService>();
        services.AddHttpClient<IMetaTrackingService, MetaTrackingService>(client =>
        {
            client.BaseAddress = new Uri("https://graph.facebook.com/v20.0/");
        });
        services.AddSingleton<Ga4TrackingDispatcher>();
        services.AddSingleton<IGa4TrackingDispatcher>(sp => sp.GetRequiredService<Ga4TrackingDispatcher>());
        services.AddHostedService(sp => sp.GetRequiredService<Ga4TrackingDispatcher>());
        services.AddSingleton<MetaTrackingDispatcher>();
        services.AddSingleton<IMetaTrackingDispatcher>(sp => sp.GetRequiredService<MetaTrackingDispatcher>());
        services.AddHostedService(sp => sp.GetRequiredService<MetaTrackingDispatcher>());
        services.AddTransient<Ekom.Services.IMailService, MailService>();
        services.AddTransient<EkomPayments>();
        services.AddTransient<DatabaseService>();

        services.AddTransient<CountriesRepository>();
        services.AddTransient<StockRepository>();
        services.AddTransient<DiscountStockRepository>();

        services.AddTransient<ManagerRepository>();
        services.AddTransient<OrderRepository>();
        services.AddTransient<CouponRepository>();
        services.AddTransient<ActivityLogRepository>();
        services.AddSingleton<OrderActivityLogDispatcher>();
        services.AddSingleton<IOrderActivityLogDispatcher>(sp => sp.GetRequiredService<OrderActivityLogDispatcher>());
        services.AddHostedService(sp => sp.GetRequiredService<OrderActivityLogDispatcher>());
        services.AddTransient<IOrderActivityLogService, OrderActivityLogService>();
        services.AddTransient<IOrderManagerActionService, OrderManagerActionService>();
        services.AddTransient<IProductFilterService, ProductFilterService>();

        services.AddSingleton<IObjectFactory<IStore>, StoreFactory>();
        services.AddSingleton<IObjectFactory<IZone>, ZoneFactory>();
        services.AddSingleton<IPerStoreFactory<ICategory>, CategoryFactory>();
        services.AddSingleton<IPerStoreFactory<IDiscount>, DiscountFactory>();
        services.AddSingleton<IPerStoreFactory<Models.IPaymentProvider>, PaymentProviderFactory>();
        services.AddSingleton<IPerStoreFactory<IShippingProvider>, ShippingProviderFactory>();
        services.AddSingleton<IPerStoreFactory<IProduct>, ProductFactory>();
        services.AddSingleton<IPerStoreFactory<IProductDiscount>, ProductDiscountFactory>();
        services.AddSingleton<IPerStoreFactory<IVariant>, VariantFactory>();
        services.AddSingleton<IPerStoreFactory<IVariantGroup>, VariantGroupFactory>();

        // What follows are explicit factory constructors for the API methods
        // This is needed since many of their dependencies are internal classes
        // However the API services are public, leaving their constructor public violates
        // C# visibility restrictions
        services.AddTransient<Catalog>(sp =>
            new Catalog(
                sp.GetRequiredService<ILogger<Catalog>>(),
                sp.GetRequiredService<Configuration>(),
                sp.GetRequiredService<IServiceScopeFactory>(),
                sp.GetRequiredService<IPerStoreIndexedCache<IProduct>>(),
                sp.GetRequiredService<IPerStoreIndexedCache<ICategory>>(),
                sp.GetRequiredService<IPerStoreCache<IProductDiscount>>(),
                sp.GetRequiredService<IPerStoreIndexedCache<IVariant>>(),
                sp.GetRequiredService<IPerStoreIndexedCache<IVariantGroup>>(),
                sp.GetRequiredService<IStoreService>(),
                sp.GetRequiredService<IHttpContextAccessor>(),
                sp.GetRequiredService<IProductFilterService>()
            )
        );

        services.AddTransient<ProductDiscountService>(f =>
            new ProductDiscountService(
                f.GetRequiredService<IPerStoreCache<IProductDiscount>>(),
                f.GetRequiredService<DiscountEvents>()
            )
        );

        services.AddTransient<CheckoutControllerService>(f =>
            new CheckoutControllerService(
                f.GetRequiredService<ILogger<CheckoutControllerService>>(),
                f.GetRequiredService<Configuration>(),
                f.GetRequiredService<DatabaseFactory>(),
                f.GetRequiredService<IMemberService>(),
                f.GetRequiredService<IHttpContextAccessor>(),
                f.GetRequiredService<EkomPayments>(),
                f.GetRequiredService<IServiceScopeFactory>(),
                f.GetRequiredService<IServiceProvider>()

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
                f.GetService<IStoreService>(),
                f.GetService<OrderRepository>(),
                f.GetService<CheckoutControllerService>(),
                f.GetService<IOrderActivityLogService>()
            )
        );
        services.AddTransient<Providers>(f =>
            new Providers(
                f.GetService<Configuration>(),
                f.GetService<ILogger<Providers>>(),
                f.GetService<IPerStoreCache<IShippingProvider>>(),
                f.GetService<IPerStoreCache<Models.IPaymentProvider>>(),
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
                f.GetService<IStoreService>(),
                f.GetService<Configuration>()
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
        services.AddMemoryCache();

        services.Configure<EkomOptions>(config.GetSection("Ekom"));
        services.Configure<TrackingOptions>(config.GetSection("Ekom:Tracking"));
        services.Configure<OrderDiscountCalculationOptions>(config.GetSection("Ekom:OrderDiscountCalculation"));

        services.Configure<MvcOptions>(mvcOptions =>
        {
            mvcOptions.Filters.Add<HttpResponseExceptionFilter>();
        });

        var connectionString = config.GetConnectionString("umbracoDbDSN");

        services.AddHangfire(config =>
        {
            config.UseSqlServerStorage(connectionString);
        });

        services.AddRateLimiter(options =>
        {
            options.AddPolicy("order-add", context =>
            {
                // Use client IP as the partition key
                var ip =
                    context.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: ip,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 30,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    });
            });

            options.AddPolicy("order-coupon", context =>
            {
                var ip =
                    context.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown";

                return RateLimitPartition.GetTokenBucketLimiter(
                    partitionKey: ip,
                    factory: _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = 13,
                        TokensPerPeriod = 10,
                        ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        });

        return services;
    }
}
