using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ekom.Printer.Agent;

internal sealed class PrintWorker(AgentRuntime runtime, ILogger<PrintWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeSpan.FromSeconds(5);
            try { await runtime.PollAsync(stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                // Exception messages from renderers may contain document content. Log only the type.
                logger.LogWarning("Printer cycle failed ({ErrorType}). Pending reports will be retried without reprinting.", ex.GetType().Name);
                delay = TimeSpan.FromSeconds(30);
            }
            await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
        }
    }
}
