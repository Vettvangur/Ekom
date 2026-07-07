using Ekom.Cache;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Payments;
using Ekom.Repositories;
using Ekom.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Composing;

namespace Ekom.Umb;

#pragma warning disable CA1001 // Types that own disposable fields should be disposable
internal sealed class EkomStartup : IComponent
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
{
    private readonly Configuration _config;
    private readonly ILogger<EkomStartup> _logger;
    private readonly IServiceProvider _factory;
    private readonly IMemoryCache _cache;

    public EkomStartup(
        Configuration config,
        ILogger<EkomStartup> logger,
        IServiceProvider factory,
        IMemoryCache cache)
    {
        _config = config;
        _logger = logger;
        _factory = factory;
        _cache = cache;
    }

    public void Initialize()
    {
        try
        {
            _logger.LogInformation("Initializing Ekom...");

            Configuration.Resolver = _factory;
            PriceCache.SetCache(_cache);

            var orderRepository = _factory.GetService<OrderRepository>();
            orderRepository?.MigrateOrderTableAsync();
            orderRepository?.MigrateStockToDecimalAsync();

            foreach (var cacheEntry in _config.CacheList.Value)
            {
                cacheEntry.FillCache();
            }

            var stockCache = _config.PerStoreStock
                ? _factory.GetService<IPerStoreCache<StockData>>()
                : _factory.GetService<IBaseCache<StockData>>() as ICache;

            stockCache?.FillCache();

            _factory.GetService<ICouponCache>()?.FillCache();

            Payments.Events.SuccessAsync += CompleteCheckoutAsync;

            _logger.LogInformation("Ekom started");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ekom startup failed");
        }
    }

    public void Terminate()
    {
        Payments.Events.SuccessAsync -= CompleteCheckoutAsync;
    }

    private async Task CompleteCheckoutAsync(object sender, SuccessEventArgs args)
    {
        var orderStatus = args.OrderStatus;

        if (orderStatus.EkomPaymentSettings.OrderCustomData.TryGetValue("ekomOrderUniqueId", out var value) &&
            Guid.TryParse(value, out var orderId))
        {
            var checkoutService = _factory.GetRequiredService<CheckoutService>();

            await checkoutService.CompleteAsync(orderId);
        }
    }
}
