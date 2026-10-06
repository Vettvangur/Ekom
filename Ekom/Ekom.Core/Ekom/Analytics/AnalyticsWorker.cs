using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ekom.Analytics;

internal sealed class AnalyticsWorker : BackgroundService
{
    private readonly AnalyticsJobService _jobs;
    private readonly AnalyticsOptions _options;
    private readonly ILogger<AnalyticsWorker> _logger;

    public AnalyticsWorker(AnalyticsJobService jobs, IOptions<AnalyticsOptions> options, ILogger<AnalyticsWorker> logger)
    {
        _jobs = jobs;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        if (!_options.Enabled) return;
        if (!_options.IsValid(out var error))
        {
            _logger.LogWarning("Analytics worker disabled because configuration is invalid: {Error}", error);
            return;
        }

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var worked = await _jobs.TickAsync(stoppingToken).ConfigureAwait(false);
                    await Task.Delay(worked ? _options.DelayBetweenBatches : TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Analytics worker tick failed");
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        return;
                    }
                }
            }
        }
        finally
        {
            try
            {
                await _jobs.InterruptOwnedAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Analytics shutdown could not interrupt the owned job; lease expiry will fence it");
            }
        }
    }
}
