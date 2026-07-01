using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Channels;

namespace Ekom.Tracking;

public interface IMetaTrackingDispatcher
{
    ValueTask EnqueueAsync(MetaPurchaseRequest request, CancellationToken ct = default);
}

internal sealed class MetaTrackingDispatcher : BackgroundService, IMetaTrackingDispatcher
{
    private readonly Channel<MetaPurchaseRequest> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TrackingOptions _options;
    private readonly ILogger<MetaTrackingDispatcher> _logger;

    public MetaTrackingDispatcher(IServiceScopeFactory scopeFactory, IOptions<TrackingOptions> options, ILogger<MetaTrackingDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
        _channel = Channel.CreateBounded<MetaPurchaseRequest>(new BoundedChannelOptions(Math.Max(1, _options.Meta.Dispatching.Capacity))
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public ValueTask EnqueueAsync(MetaPurchaseRequest request, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(request, ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var request in _channel.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IMetaTrackingService>();
                await service.SendPurchaseAsync(request, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Meta background tracking send failed.");
            }
        }
    }
}
