using Ekom.Printer.Configuration;
using Ekom.Printer.Migrations;
using Ekom.Printer.Rendering;
using Ekom.Printer.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Diagnostics.CodeAnalysis;

namespace Ekom.Printer.Services;

internal sealed class PrinterWorker(
    IOptions<PrinterOptions> options,
    PrinterMigration migration,
    PrinterJobStore store,
    PrinterDeliveryStore deliveryStore,
    CapturedStatusStore capturedStatuses,
    IServiceScopeFactory scopeFactory,
    ILogger<PrinterWorker> logger) : BackgroundService
{
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled) return;
        await migration.ApplyAsync(cancellationToken).ConfigureAwait(false);
        await base.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    [SuppressMessage("Design", "CA1031", Justification = "Worker isolates custom renderer failures and logs durable failed jobs; cancellation is rethrown.")]
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        var nextCleanup = DateTime.MinValue;
        do
        {
            try
            {
                await deliveryStore.ExpireClaimsAsync(stoppingToken).ConfigureAwait(false);
                foreach (var captured in await capturedStatuses.GetPendingAsync(stoppingToken).ConfigureAwait(false))
                {
                    try
                    {
                        await using var scope = scopeFactory.CreateAsyncScope();
                        await capturedStatuses.MaterializeAsync(captured,
                            scope.ServiceProvider.GetRequiredService<IPickingSlipContextFactory>(), stoppingToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Could not create picking-slip snapshot for captured job {JobId}", captured.Id);
                        await capturedStatuses.DeferAsync(captured.Id, stoppingToken).ConfigureAwait(false);
                    }
                }
                foreach (var job in await store.GetPendingAsync(stoppingToken).ConfigureAwait(false))
                {
                    try
                    {
                        var context = JsonSerializer.Deserialize<PickingSlipContext>(job.SnapshotJson ?? string.Empty)
                            ?? throw new InvalidOperationException("Missing picking-slip snapshot.");
                        await using var scope = scopeFactory.CreateAsyncScope();
                        var renderer = scope.ServiceProvider.GetRequiredService<IPickingSlipHtmlRenderer>();
                        var html = await renderer.RenderAsync(context, stoppingToken).ConfigureAwait(false);
                        await store.SaveHtmlAsync(job.Id, html, stoppingToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Picking-slip renderer failed for job {JobId}", job.Id);
                        await store.MarkRenderFailedAsync(job.Id, stoppingToken).ConfigureAwait(false);
                    }
                }
                if (DateTime.UtcNow >= nextCleanup)
                {
                    await store.CleanupAsync(options.Value, DateTime.UtcNow, stoppingToken).ConfigureAwait(false);
                    await deliveryStore.CleanupAsync(stoppingToken).ConfigureAwait(false);
                    nextCleanup = DateTime.UtcNow.AddHours(1);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ekom.Printer background processing failed; retrying on next cycle");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

}
