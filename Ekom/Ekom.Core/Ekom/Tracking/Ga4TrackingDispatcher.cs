using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Channels;

namespace Ekom.Tracking;

public interface IGa4TrackingDispatcher
{
    ValueTask EnqueueAsync(Ga4PurchaseRequest request, CancellationToken ct = default);
}

internal sealed class Ga4TrackingDispatcher : BackgroundService, IGa4TrackingDispatcher
{
    private readonly Channel<Ga4PurchaseRequest> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TrackingOptions _options;
    private readonly ILogger<Ga4TrackingDispatcher> _logger;

    public Ga4TrackingDispatcher(IServiceScopeFactory scopeFactory, IOptions<TrackingOptions> options, ILogger<Ga4TrackingDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
        _channel = Channel.CreateBounded<Ga4PurchaseRequest>(new BoundedChannelOptions(Math.Max(1, _options.Ga4.Dispatching.Capacity))
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public ValueTask EnqueueAsync(Ga4PurchaseRequest request, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(request, ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var request in _channel.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IGa4TrackingService>();
                await service.SendPurchaseAsync(request, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GA4 background tracking send failed.");
            }
        }
    }
}
