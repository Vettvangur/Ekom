using Ekom.Mailchimp.Clients;
using Ekom.Mailchimp.Exceptions;
using Ekom.Mailchimp.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading.Channels;

namespace Ekom.Mailchimp.Dispatching;

internal interface IMailchimpTransactionalDispatcher
{
    bool TryEnqueue(MailchimpTransactionalWorkItem item);
}

internal abstract record MailchimpTransactionalWorkItem(
    string StoreAlias,
    string CorrelationId,
    long PayloadBytes);

internal sealed record TransactionalMessageWorkItem(MailchimpTransactionalMessage Message, long PayloadBytes)
    : MailchimpTransactionalWorkItem(Message.StoreAlias, Message.CorrelationId!, PayloadBytes);

internal sealed record TransactionalTemplateWorkItem(MailchimpTransactionalTemplateMessage Message, long PayloadBytes)
    : MailchimpTransactionalWorkItem(Message.StoreAlias, Message.CorrelationId!, PayloadBytes);

internal sealed class MailchimpTransactionalDispatcher : BackgroundService, IMailchimpTransactionalDispatcher
{
    private readonly IMailchimpTransactionalClient _client;
    private readonly ILogger<MailchimpTransactionalDispatcher> _logger;
    private readonly MailchimpTransactionalDispatcherOptions _options;
    private readonly Channel<MailchimpTransactionalWorkItem> _queue;
    private long _queuedBytes;

    public MailchimpTransactionalDispatcher(
        IOptions<MailchimpOptions> options,
        IMailchimpTransactionalClient client,
        ILogger<MailchimpTransactionalDispatcher> logger)
    {
        _options = options.Value.Transactional.Dispatching;
        _client = client;
        _logger = logger;
        _queue = Channel.CreateBounded<MailchimpTransactionalWorkItem>(new BoundedChannelOptions(_options.MaxQueueSize)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false,
        });
    }

    public bool TryEnqueue(MailchimpTransactionalWorkItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        bool queued = TryReserveBytes(item.PayloadBytes);
        if (queued && !_queue.Writer.TryWrite(item))
        {
            Interlocked.Add(ref _queuedBytes, -item.PayloadBytes);
            queued = false;
        }
        if (!queued)
        {
            _logger.LogWarning(
                "Mailchimp Transactional queue capacity is exhausted; dropping {WorkType} {CorrelationId} for store {StoreAlias}",
                item.GetType().Name,
                item.CorrelationId,
                item.StoreAlias);
        }
        return queued;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Task[] workers = Enumerable.Range(0, _options.MaxConcurrency)
            .Select(_ => ProcessQueueAsync(stoppingToken))
            .ToArray();
        return Task.WhenAll(workers);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _queue.Writer.TryComplete();
        return base.StopAsync(cancellationToken);
    }

    private async Task ProcessQueueAsync(CancellationToken stoppingToken)
    {
        await foreach (MailchimpTransactionalWorkItem item in _queue.Reader.ReadAllAsync(stoppingToken)
            .ConfigureAwait(false))
        {
            try
            {
                await ProcessWithRetryAsync(item, stoppingToken).ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Add(ref _queuedBytes, -item.PayloadBytes);
            }
        }
    }

    [SuppressMessage("Design", "CA1031", Justification = "A failed queued item must not terminate the hosted dispatcher.")]
    private async Task ProcessWithRetryAsync(MailchimpTransactionalWorkItem item, CancellationToken ct)
    {
        for (int attempt = 1; attempt <= _options.MaxAttempts; attempt++)
        {
            try
            {
                IReadOnlyList<MailchimpTransactionalSendResult> results = await ProcessAsync(item, ct)
                    .ConfigureAwait(false);
                LogResults(item, results);
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (attempt < _options.MaxAttempts && IsSafeToRetry(ex))
            {
                TimeSpan delay = GetRetryDelay(ex, attempt);
                _logger.LogWarning(
                    ex,
                    "Transient Mailchimp Transactional failure for {CorrelationId} in store {StoreAlias}; retrying in {Delay}",
                    item.CorrelationId,
                    item.StoreAlias,
                    delay);
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Mailchimp Transactional message {CorrelationId} in store {StoreAlias} failed",
                    item.CorrelationId,
                    item.StoreAlias);
                return;
            }
        }
    }

    private Task<IReadOnlyList<MailchimpTransactionalSendResult>> ProcessAsync(
        MailchimpTransactionalWorkItem item,
        CancellationToken ct)
        => item switch
        {
            TransactionalMessageWorkItem raw => _client.SendAsync(raw.Message, ct),
            TransactionalTemplateWorkItem template => _client.SendTemplateAsync(template.Message, ct),
            _ => throw new InvalidOperationException(
                $"Unsupported Mailchimp Transactional work item '{item.GetType().Name}'."),
        };

    private void LogResults(
        MailchimpTransactionalWorkItem item,
        IReadOnlyList<MailchimpTransactionalSendResult> results)
    {
        foreach (MailchimpTransactionalSendResult result in results)
        {
            if (result.Status is "rejected" or "invalid")
            {
                _logger.LogWarning(
                    "Mailchimp Transactional message {CorrelationId} in store {StoreAlias} returned {Status} ({RejectReason}); Mandrill ID {MandrillId}",
                    item.CorrelationId,
                    item.StoreAlias,
                    result.Status,
                    result.RejectReason,
                    result.Id);
            }
            else
            {
                _logger.LogInformation(
                    "Mailchimp Transactional message {CorrelationId} in store {StoreAlias} returned {Status}; Mandrill ID {MandrillId}",
                    item.CorrelationId,
                    item.StoreAlias,
                    result.Status,
                    result.Id);
            }
        }
    }

    private TimeSpan GetRetryDelay(Exception exception, int attempt)
    {
        double exponentialSeconds = _options.InitialRetryDelaySeconds * Math.Pow(2, attempt - 1);
        double jitterSeconds = RandomNumberGenerator.GetInt32(0, 1_001) / 1_000d;
        var calculated = TimeSpan.FromSeconds(exponentialSeconds + jitterSeconds);
        return exception is MailchimpTransactionalApiException { RetryAfter: { } retryAfter }
            && retryAfter > calculated
                ? retryAfter
                : calculated;
    }

    private static bool IsSafeToRetry(Exception exception)
        => exception is MailchimpTransactionalApiException { IsTransient: true }
            or HttpRequestException { HttpRequestError: HttpRequestError.NameResolutionError }
            || exception is HttpRequestException { HttpRequestError: HttpRequestError.ConnectionError } requestException
                && IsPreSendSocketFailure(requestException);

    private static bool IsPreSendSocketFailure(Exception exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            if (current is SocketException socketException)
            {
                return socketException.SocketErrorCode is SocketError.ConnectionRefused
                    or SocketError.HostUnreachable
                    or SocketError.NetworkUnreachable
                    or SocketError.AddressNotAvailable;
            }
        }
        return false;
    }

    private bool TryReserveBytes(long payloadBytes)
    {
        while (true)
        {
            long current = Interlocked.Read(ref _queuedBytes);
            if (payloadBytes <= 0 || payloadBytes > _options.MaxQueueBytes - current)
            {
                return false;
            }
            if (Interlocked.CompareExchange(ref _queuedBytes, current + payloadBytes, current) == current)
            {
                return true;
            }
        }
    }
}
