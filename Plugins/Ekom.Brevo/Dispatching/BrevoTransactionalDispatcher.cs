using Ekom.Brevo.Http;
using Ekom.Brevo.Models;
using Ekom.Brevo.Persistence;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace Ekom.Brevo.Dispatching;

internal sealed class BrevoTransactionalDispatcher(BrevoConfiguration configuration, BrevoEmailStore store,
    BrevoHttpClient client, ILogger<BrevoTransactionalDispatcher> logger) : BackgroundService
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<BrevoDeliveryResult>> _completions = new();
    private readonly SemaphoreSlim _admission = new(1, 1);
    private bool _stopped;

    public async ValueTask<BrevoSubmission> EnqueueAsync(BrevoTransactionalMessage message, string id, string payload, CancellationToken ct)
    {
        await _admission.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_stopped) return new(BrevoSubmissionStatus.QueueFull, id);
            var submission = await store.EnqueueAsync(message, id, payload, ct).ConfigureAwait(false);
            if (submission.Status != BrevoSubmissionStatus.Queued) return submission;
            var completion = new TaskCompletionSource<BrevoDeliveryResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            _completions[id] = completion;
            return submission with { Completion = completion.Task };
        }
        finally { _admission.Release(); }
    }

    [SuppressMessage("Design", "CA1031", Justification = "Database outages must retain queued work and be retried without stopping the host; only error type is logged.")]
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        DateTime nextPurge = DateTime.MinValue;
        while (!stoppingToken.IsCancellationRequested)
        {
            bool idle = true;
            try
            {
                if (configuration.Options.Enabled)
                {
                    await CompleteLocalAsync(stoppingToken).ConfigureAwait(false);
                    if (DateTime.UtcNow >= nextPurge)
                    {
                        await store.PurgeAsync(stoppingToken).ConfigureAwait(false);
                        nextPurge = DateTime.UtcNow.AddHours(1);
                    }
                    var row = await store.ClaimAsync(stoppingToken).ConfigureAwait(false);
                    if (row != null)
                    {
                        idle = false;
                        var (result, retry) = await DeliverAsync(row, stoppingToken).ConfigureAwait(false);
                        // Persist the response even during graceful shutdown. A crash before commit leaves a lease that becomes Unknown.
                        using var persistTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                        await store.FinishAsync(row, result, retry, persistTimeout.Token).ConfigureAwait(false);
                        if (retry == null && _completions.TryRemove(row.Id, out var completion)) completion.TrySetResult(result);
                        if (retry == null && result.Status != BrevoDeliveryStatus.Accepted)
                            logger.LogWarning("Brevo email {CorrelationId} for store {StoreAlias} ended with {Status} (HTTP {HttpStatus})",
                                row.Id, row.Store, result.Status, result.HttpStatus);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                idle = true;
                logger.LogError("Brevo persistent email worker failed ({ErrorType}); durable work remains available", ex.GetType().Name);
            }
            if (idle)
            {
                try { await Task.Delay(TimeSpan.FromSeconds(configuration.Options.Transactional.Persistence.PollIntervalSeconds), stoppingToken).ConfigureAwait(false); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            }
        }
    }

    private async Task CompleteLocalAsync(CancellationToken ct)
    {
        // Another node can finish work admitted here. Keep SQL parameter counts bounded.
        foreach (var ids in _completions.Keys.Chunk(100))
        {
            foreach (var row in await store.GetPendingCompletionsAsync(ids, ct).ConfigureAwait(false))
            {
                if (_completions.TryRemove(row.Id, out var completion))
                    completion.TrySetResult(new(row.State switch
                    {
                        BrevoQueueState.Accepted => BrevoDeliveryStatus.Accepted,
                        BrevoQueueState.Failed => BrevoDeliveryStatus.Failed,
                        _ => BrevoDeliveryStatus.Unknown,
                    }, row.MessageId, row.HttpStatus));
            }
        }
    }

    private async Task<(BrevoDeliveryResult Result, DateTime? RetryUtc)> DeliverAsync(BrevoEmailRow row, CancellationToken ct)
    {
        var limits = configuration.Options.Transactional.Dispatching;
        if (!configuration.Enabled(row.Store, transactional: true))
            return (new(BrevoDeliveryStatus.Failed), null);
        string? key = configuration.ApiKey(row.Store, transactional: true);
        if (string.IsNullOrWhiteSpace(key)) return (new(BrevoDeliveryStatus.Failed), null);
        try
        {
            string? response = await client.SendAsync(key, HttpMethod.Post, "smtp/email", row.Payload, ct).ConfigureAwait(false);
            return (new(BrevoDeliveryStatus.Accepted, BrevoHttpClient.MessageId(response)), null);
        }
        catch (BrevoApiException ex)
        {
            if (ex.StatusCode == HttpStatusCode.TooManyRequests && row.Attempts < limits.MaxAttempts
                && !(ex.RetryAfter > TimeSpan.FromSeconds(limits.MaxRetryDelaySeconds)))
            {
                double seconds = Math.Min(limits.MaxRetryDelaySeconds, limits.InitialRetryDelaySeconds * Math.Pow(2, row.Attempts - 1));
                seconds = Math.Max(seconds, ex.RetryAfter?.TotalSeconds ?? 0);
                return (new(BrevoDeliveryStatus.Failed, HttpStatus: 429), DateTime.UtcNow.AddSeconds(seconds));
            }
            return (new((int)ex.StatusCode >= 500 || ex.StatusCode == HttpStatusCode.RequestTimeout
                ? BrevoDeliveryStatus.Unknown : BrevoDeliveryStatus.Failed, HttpStatus: (int)ex.StatusCode), null);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
        {
            return (new(BrevoDeliveryStatus.Unknown), null);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _admission.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { _stopped = true; }
        finally { _admission.Release(); }
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        foreach (var pair in _completions)
            if (_completions.TryRemove(pair.Key, out var completion)) completion.TrySetResult(new(BrevoDeliveryStatus.Cancelled));
    }

    public override void Dispose()
    {
        base.Dispose();
        _admission.Dispose();
    }
}
