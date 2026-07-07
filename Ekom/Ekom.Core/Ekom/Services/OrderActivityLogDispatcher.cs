using Ekom.Models;
using Ekom.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace Ekom.Services;

public interface IOrderActivityLogDispatcher
{
    ValueTask EnqueueAsync(OrderActivityLogWrite work, CancellationToken ct = default);
}

public sealed record OrderActivityLogWrite(
    Guid OrderId,
    string Message,
    string UserName,
    DateTime Date,
    OrderActivityLogType LogType);

public sealed class OrderActivityLogDispatcher : BackgroundService, IOrderActivityLogDispatcher
{
    private static readonly TimeSpan FlushInterval = TimeSpan.FromMilliseconds(500);
    private const int MaxBatchSize = 50;
    private const int MaxQueueSize = 1000;

    private readonly Channel<OrderActivityLogWrite> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrderActivityLogDispatcher> _logger;

    public OrderActivityLogDispatcher(
        IServiceScopeFactory scopeFactory,
        ILogger<OrderActivityLogDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _channel = Channel.CreateBounded<OrderActivityLogWrite>(new BoundedChannelOptions(MaxQueueSize)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    public ValueTask EnqueueAsync(OrderActivityLogWrite work, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(work, ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                bool hasItem = await _channel.Reader.WaitToReadAsync(stoppingToken).ConfigureAwait(false);
                if (!hasItem)
                {
                    continue;
                }

                await Task.Delay(FlushInterval, stoppingToken).ConfigureAwait(false);

                List<OrderActivityLogWrite> batch = DrainBatch();
                if (batch.Count == 0)
                {
                    continue;
                }

                using IServiceScope scope = _scopeFactory.CreateScope();
                ActivityLogRepository repository = scope.ServiceProvider.GetRequiredService<ActivityLogRepository>();

                await repository.InsertAsync(batch, stoppingToken).ConfigureAwait(false);

                _logger.LogDebug("Inserted {Count} order activity logs in background batch.", batch.Count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Order activity log dispatcher failed processing a batch.");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private List<OrderActivityLogWrite> DrainBatch()
    {
        var batch = new List<OrderActivityLogWrite>(MaxBatchSize);

        while (batch.Count < MaxBatchSize && _channel.Reader.TryRead(out OrderActivityLogWrite? work))
        {
            batch.Add(work);
        }

        return batch;
    }
}
