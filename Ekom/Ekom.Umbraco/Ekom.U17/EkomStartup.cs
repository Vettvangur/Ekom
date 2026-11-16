using Ekom.Cache;
using Ekom.Payments;
using Ekom.Repositories;
using Ekom.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Services;

namespace Ekom.Umb;

#pragma warning disable CA1001 // Types that own disposable fields should be disposable
internal sealed class EkomStartup : IAsyncComponent
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
{
    private readonly ILogger<EkomStartup> _logger;
    private readonly IServiceProvider _factory;
    private readonly IMemoryCache _cache;
    private readonly IRuntimeState _runtimeState;
    private readonly IOptions<RequestLocalizationOptions> _requestLocalizationOptions;

    public EkomStartup(
        ILogger<EkomStartup> logger,
        IServiceProvider factory,
        IMemoryCache cache,
        IRuntimeState runtimeState,
        IOptions<RequestLocalizationOptions> requestLocalizationOptions)
    {
        _logger = logger;
        _factory = factory;
        _cache = cache;
        _runtimeState = runtimeState;
        _requestLocalizationOptions = requestLocalizationOptions;
    }

    public Task InitializeAsync(bool isRestarting, CancellationToken cancellationToken)
    {
        try
        {
            Configuration.Resolver = _factory;
            PriceCache.SetCache(_cache);

            if (_runtimeState.Level < RuntimeLevel.Run)
            {
                return Task.CompletedTask;
            }

            _logger.LogInformation("Initializing Ekom...");

            using var scope = _factory.CreateScope();
            EkomCultureRequestLocalizationOptions.ConfigureCultures(
                _requestLocalizationOptions.Value,
                scope.ServiceProvider.GetRequiredService<Ekom.Services.IUmbracoService>());

            var orderRepository = _factory.GetService<OrderRepository>();
            orderRepository?.MigrateOrderTableAsync();
            orderRepository?.MigrateStockToDecimalAsync();

            _logger.LogInformation("Ekom started");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ekom startup failed");
        }

        return Task.CompletedTask;
    }

    public Task TerminateAsync(bool isRestarting, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
