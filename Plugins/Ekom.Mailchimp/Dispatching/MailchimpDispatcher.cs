using Ekom.Mailchimp.Clients;
using Ekom.Mailchimp.Exceptions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Channels;

namespace Ekom.Mailchimp.Dispatching;

internal interface IMailchimpDispatcher
{
    ValueTask EnqueueAsync(MailchimpWorkItem item, CancellationToken ct);
}

internal sealed class MailchimpDispatcher : BackgroundService, IMailchimpDispatcher
{
    private readonly IMailchimpAudienceClient _audienceClient;
    private readonly IMailchimpEcommerceClient _ecommerceClient;
    private readonly ILogger<MailchimpDispatcher> _logger;
    private readonly MailchimpDispatcherOptions _options;
    private readonly Channel<MailchimpWorkItem> _queue;

    public MailchimpDispatcher(
        IOptions<MailchimpOptions> options,
        IMailchimpAudienceClient audienceClient,
        IMailchimpEcommerceClient ecommerceClient,
        ILogger<MailchimpDispatcher> logger)
    {
        _options = options.Value.Dispatching;
        _audienceClient = audienceClient;
        _ecommerceClient = ecommerceClient;
        _logger = logger;
        _queue = Channel.CreateBounded<MailchimpWorkItem>(new BoundedChannelOptions(_options.MaxQueueSize)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false,
        });
    }

    public ValueTask EnqueueAsync(MailchimpWorkItem item, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(item);
        ct.ThrowIfCancellationRequested();
        if (!_queue.Writer.TryWrite(item))
        {
            _logger.LogWarning(
                "Mailchimp queue is full; dropping {WorkType} for {Identifier} in store {StoreAlias}",
                item.GetType().Name,
                item.Identifier,
                item.StoreAlias);
        }

        return ValueTask.CompletedTask;
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
        await foreach (MailchimpWorkItem item in _queue.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            await ProcessWithRetryAsync(item, stoppingToken).ConfigureAwait(false);
        }
    }

    [SuppressMessage("Design", "CA1031", Justification = "A failed queued item must not terminate the hosted dispatcher.")]
    private async Task ProcessWithRetryAsync(MailchimpWorkItem item, CancellationToken ct)
    {
        for (int attempt = 1; attempt <= _options.MaxAttempts; attempt++)
        {
            try
            {
                await ProcessAsync(item, ct).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (attempt < _options.MaxAttempts && IsTransient(ex))
            {
                TimeSpan delay = GetRetryDelay(ex, attempt);
                _logger.LogWarning(
                    ex,
                    "Transient Mailchimp failure for {WorkType} {Identifier} in store {StoreAlias}; retrying in {Delay}",
                    item.GetType().Name,
                    item.Identifier,
                    item.StoreAlias,
                    delay);
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Mailchimp work {WorkType} for {Identifier} in store {StoreAlias} failed",
                    item.GetType().Name,
                    item.Identifier,
                    item.StoreAlias);
                return;
            }
        }
    }

    private Task ProcessAsync(MailchimpWorkItem item, CancellationToken ct) => item switch
    {
        SubscribeWorkItem subscribe => _audienceClient.SubscribeAsync(subscribe.Request, ct),
        UnsubscribeWorkItem unsubscribe => _audienceClient.UnsubscribeAsync(unsubscribe.Request, ct),
        PurchaseWorkItem purchase => _ecommerceClient.TrackPurchaseAsync(purchase.Purchase, ct),
        _ => throw new InvalidOperationException($"Unsupported Mailchimp work item '{item.GetType().Name}'."),
    };

    private TimeSpan GetRetryDelay(Exception exception, int attempt)
    {
        double exponentialSeconds = _options.InitialRetryDelaySeconds * Math.Pow(2, attempt - 1);
        double jitterSeconds = RandomNumberGenerator.GetInt32(0, 1_001) / 1_000d;
        var calculated = TimeSpan.FromSeconds(exponentialSeconds + jitterSeconds);
        return exception is MailchimpApiException { RetryAfter: { } retryAfter } && retryAfter > calculated
            ? retryAfter
            : calculated;
    }

    private static bool IsTransient(Exception exception)
        => exception is HttpRequestException
            or TimeoutException
            or OperationCanceledException
            or MailchimpApiException { StatusCode: HttpStatusCode.RequestTimeout }
            or MailchimpApiException { IsTransient: true };
}
