using Ekom.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;

namespace Ekom.Services;

internal sealed class StockReservationWorker : BackgroundService
{
    private static readonly Counter<long> Failures = StockReservationService.Meter.CreateCounter<long>("ekom.reservations.worker.failures");
    private static readonly Counter<long> Cleaned = StockReservationService.Meter.CreateCounter<long>("ekom.reservations.cleaned");
    private readonly IStockReservationService _reservations;
    private readonly StockReservationReadiness _readiness;
    private readonly StockReservationOptions _options;
    private readonly ILogger<StockReservationWorker> _logger;

    public StockReservationWorker(IStockReservationService reservations, StockReservationReadiness readiness,
        IOptions<StockReservationOptions> options, ILogger<StockReservationWorker> logger)
    {
        _reservations = reservations;
        _readiness = readiness;
        _options = options.Value;
        _options.Validate();
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.WorkerEnabled) return;
        // Yield on net8 as well: hosted-service startup must not block adapter initialization.
        await Task.Yield();
        int failures = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = _options.PollInterval;
            try
            {
                if (_readiness.IsReady)
                {
                    var expired = await _reservations.ExpireDueAsync(_options.BatchSize, stoppingToken).ConfigureAwait(false);
                    var cleaned = await _reservations.CleanupAsync(DateTime.UtcNow - _options.CompletedRetention,
                        _options.BatchSize, stoppingToken).ConfigureAwait(false);
                    Cleaned.Add(cleaned);
                    _logger.LogDebug("Reservation sweep expired {ExpiredCount}, cleaned {CleanedCount}", expired, cleaned);
                }
                failures = 0;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                Failures.Add(1);
                failures = Math.Min(failures + 1, 8);
                delay = TimeSpan.FromSeconds(Math.Min(300, Math.Max(_options.PollInterval.TotalSeconds, Math.Pow(2, failures))))
                    + TimeSpan.FromMilliseconds(RandomNumberGenerator.GetInt32(1000));
                _logger.LogWarning(ex, "Reservation sweep failed; retrying in {RetryDelay}", delay);
            }
            try { await Task.Delay(delay, stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
