using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ekom.Services;

/// <summary>
/// Copies old payments onto their orders in the background after startup, so a large history
/// does not delay the site. A failure is logged and tried again at the next startup.
/// </summary>
internal sealed class LegacyPaymentMigrationHostedService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<LegacyPaymentMigrationHostedService> _logger;

    public LegacyPaymentMigrationHostedService(IServiceProvider services, IHostApplicationLifetime lifetime,
        ILogger<LegacyPaymentMigrationHostedService> logger)
    {
        _services = services;
        _lifetime = lifetime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Wait until the site has started: Ekom's tables are created during startup.
            var started = new TaskCompletionSource();
            await using var registration = _lifetime.ApplicationStarted.Register(() => started.TrySetResult());
            await started.Task.WaitAsync(stoppingToken).ConfigureAwait(false);

            _services.GetRequiredService<DatabaseService>().EnsureOrderPaymentTables();
            await _services.GetRequiredService<LegacyPaymentMigrationService>().MigrateAsync(ct: stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
#pragma warning disable CA1031 // Old payment history must never stop the site; the copy resumes at the next startup.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Copying old payments onto their orders failed. It continues at the next startup.");
        }
    }
}
