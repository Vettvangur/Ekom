using Ekom.Brevo.Http;
using Ekom.Brevo.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Channels;

namespace Ekom.Brevo.Dispatching;

internal sealed record BrevoWork(string StoreAlias, string ApiKey, HttpMethod Method, string Path, string Payload,
    bool SafeToReplay, string CorrelationId, TaskCompletionSource<BrevoDeliveryResult> Completion, long Bytes);

internal abstract class BrevoDispatcher : BackgroundService
{
    private readonly Channel<BrevoWork> _queue;
    private readonly BrevoDispatcherOptions _options;
    private readonly BrevoHttpClient _client;
    private readonly ILogger _logger;
    private readonly object _gate = new();
    private long _bytes;

    protected BrevoDispatcher(BrevoDispatcherOptions options, BrevoHttpClient client, ILogger logger)
    {
        _options = options;
        _client = client;
        _logger = logger;
        _queue = Channel.CreateBounded<BrevoWork>(new BoundedChannelOptions(options.MaxQueueSize)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
        });
    }

    public BrevoSubmission Enqueue(string store, string apiKey, HttpMethod method, string path,
        string payload, bool safeToReplay, string? correlationId = null)
    {
        long bytes = Encoding.UTF8.GetByteCount(payload);
        if (bytes > _options.MaxMessageBytes)
            throw new ArgumentException("Brevo payload exceeds MaxMessageBytes.", nameof(payload));
        string id = correlationId ?? Guid.NewGuid().ToString("N");
        var completion = new TaskCompletionSource<BrevoDeliveryResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            if (bytes > _options.MaxQueueBytes - _bytes)
                return new(BrevoSubmissionStatus.QueueFull, id);
            _bytes += bytes;
            if (!_queue.Writer.TryWrite(new(store, apiKey, method, path, payload, safeToReplay, id, completion, bytes)))
            {
                _bytes -= bytes;
                return new(BrevoSubmissionStatus.QueueFull, id);
            }
        }
        return new(BrevoSubmissionStatus.Queued, id, completion.Task);
    }

    [SuppressMessage("Design", "CA1031", Justification = "Unexpected worker failures complete the submission as unknown without terminating the checkout host.")]
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var work in _queue.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
                try
                {
                    var result = stoppingToken.IsCancellationRequested
                        ? new BrevoDeliveryResult(BrevoDeliveryStatus.Cancelled)
                        : await DeliverAsync(work, stoppingToken).ConfigureAwait(false);
                    work.Completion.TrySetResult(result);
                    if (result.Status != BrevoDeliveryStatus.Accepted)
                        _logger.LogWarning("Brevo operation {CorrelationId} for store {StoreAlias} ended with {Status} (HTTP {HttpStatus})",
                            work.CorrelationId, work.StoreAlias, result.Status, result.HttpStatus);
                }
                catch (Exception ex)
                {
                    work.Completion.TrySetResult(new(BrevoDeliveryStatus.Unknown));
                    _logger.LogError("Brevo operation {CorrelationId} for store {StoreAlias} failed unexpectedly ({ErrorType})",
                        work.CorrelationId, work.StoreAlias, ex.GetType().Name);
                }
                finally
                {
                    lock (_gate) _bytes -= work.Bytes;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally
        {
            _queue.Writer.TryComplete();
            while (_queue.Reader.TryRead(out var pending))
            {
                pending.Completion.TrySetResult(new(BrevoDeliveryStatus.Cancelled));
                lock (_gate) _bytes -= pending.Bytes;
            }
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _queue.Writer.TryComplete();
        return base.StopAsync(cancellationToken);
    }

    private async Task<BrevoDeliveryResult> DeliverAsync(BrevoWork work, CancellationToken ct)
    {
        for (int attempt = 1; ; attempt++)
        {
            TimeSpan? retryAfter = null;
            try
            {
                string? response = await _client.SendAsync(work.ApiKey, work.Method, work.Path, work.Payload, ct).ConfigureAwait(false);
                // Contact responses contain id, while email responses contain messageId.
                return new(BrevoDeliveryStatus.Accepted, BrevoHttpClient.MessageId(response));
            }
            catch (BrevoApiException ex)
            {
                bool retryable = ex.StatusCode == HttpStatusCode.TooManyRequests
                    || (work.SafeToReplay && (int)ex.StatusCode >= 500);
                if (!retryable || attempt >= _options.MaxAttempts
                    || ex.RetryAfter > TimeSpan.FromSeconds(_options.MaxRetryDelaySeconds))
                    return new(!work.SafeToReplay && ((int)ex.StatusCode >= 500 || ex.StatusCode == HttpStatusCode.RequestTimeout)
                        ? BrevoDeliveryStatus.Unknown : BrevoDeliveryStatus.Failed, HttpStatus: (int)ex.StatusCode);
                retryAfter = ex.RetryAfter;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // A request may already have reached Brevo when shutdown cancels it.
                return new(BrevoDeliveryStatus.Unknown);
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
            {
                if (!work.SafeToReplay || attempt >= _options.MaxAttempts)
                    return new(BrevoDeliveryStatus.Unknown);
            }
            catch (System.Text.Json.JsonException)
            {
                // A successful HTTP response with an unexpected body is not safe to replay.
                return new(BrevoDeliveryStatus.Unknown);
            }
            var delay = TimeSpan.FromSeconds(Math.Min(_options.MaxRetryDelaySeconds,
                _options.InitialRetryDelaySeconds * Math.Pow(2, attempt - 1)));
            if (retryAfter > delay) delay = retryAfter.Value;
            try { await Task.Delay(delay, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return new(BrevoDeliveryStatus.Cancelled);
            }
        }
    }
}

internal sealed class BrevoMarketingDispatcher(IOptions<BrevoOptions> options, BrevoHttpClient client, ILogger<BrevoMarketingDispatcher> logger)
    : BrevoDispatcher(options.Value.Dispatching, client, logger);
